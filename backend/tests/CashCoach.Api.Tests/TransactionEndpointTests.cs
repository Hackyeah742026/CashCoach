using System.Net;
using CashCoach.Api.Contracts;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.SyntheticData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Api.Tests;

public class TransactionEndpointTests
{
    [Fact]
    public async Task Transactions_are_filtered_by_category_date_and_text_with_paging()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var groceries = await (await client.GetAsync("/api/transactions?category=groceries&limit=500")).ReadAsync<TransactionListResponse>();
        var august = await (await client.GetAsync("/api/transactions?from=2026-08-01&to=2026-08-31&limit=500")).ReadAsync<TransactionListResponse>();
        var zabka = await (await client.GetAsync("/api/transactions?q=zabka&limit=5&offset=2")).ReadAsync<TransactionListResponse>();

        groceries.Total.Should().BePositive();
        groceries.Items.Should().HaveCount(groceries.Total).And.OnlyContain(t => t.Category == Category.Groceries);
        august.Items.Should().OnlyContain(t => t.Date.Month == 8);
        zabka.Items.Should().HaveCount(5).And.OnlyContain(t => t.Merchant == "Żabka");
        zabka.Total.Should().BeGreaterThan(5);
        zabka.Items.Should().BeInDescendingOrder(t => t.Date);
    }

    [Fact]
    public async Task Transactions_expose_amounts_in_zloty_and_pipeline_flags()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var rent = (await (await client.GetAsync("/api/transactions?category=rent")).ReadAsync<TransactionListResponse>()).Items;
        var bnpl = (await (await client.GetAsync("/api/transactions?category=bnpl")).ReadAsync<TransactionListResponse>()).Items;

        rent.Should().NotBeEmpty().And.OnlyContain(t => t.Amount == -1100.00m && t.IsRecurring && t.Channel == Channel.Transfer);
        bnpl.Should().NotBeEmpty().And.OnlyContain(t => t.IsBnpl && t.Merchant == "Modivo");
    }

    [Fact]
    public async Task Invalid_category_filter_returns_400()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var response = await client.GetAsync("/api/transactions?category=cats");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_category");
    }

    [Fact]
    public async Task Patch_with_apply_to_merchant_recategorizes_every_transaction_of_the_merchant_and_future_imports()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        var starbucks = await (await client.GetAsync("/api/transactions?q=starbucks&limit=500")).ReadAsync<TransactionListResponse>();

        var result = await (await client.PatchJsonAsync($"/api/transactions/{starbucks.Items[0].Id}", new { category = "food_delivery", apply_to_merchant = true }))
            .ReadAsync<UpdateTransactionCategoryResponse>();

        result.Should().Be(new UpdateTransactionCategoryResponse(starbucks.Items[0].Id, Category.FoodDelivery, starbucks.Total));
        var after = await (await client.GetAsync("/api/transactions?q=starbucks&limit=500")).ReadAsync<TransactionListResponse>();
        after.Items.Should().OnlyContain(t => t.Category == Category.FoodDelivery);

        var import = await (await client.UploadCsvAsync("date;amount;description;currency\n2026-10-01;-19,90;STARBUCKS 101 KRAKOW;PLN\n"))
            .ReadAsync<ImportResponse>();
        import.Imported.Should().Be(1);
        var imported = await factory.WithDbAsync(db => db.Transactions.SingleAsync(t => t.Date == new DateOnly(2026, 10, 1)));
        imported.Category.Should().Be(Category.FoodDelivery);
    }

    [Fact]
    public async Task Patch_without_apply_to_merchant_updates_only_that_transaction()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        var zabka = await (await client.GetAsync("/api/transactions?q=zabka&limit=2")).ReadAsync<TransactionListResponse>();

        var result = await (await client.PatchJsonAsync($"/api/transactions/{zabka.Items[0].Id}", new { category = "restaurants" }))
            .ReadAsync<UpdateTransactionCategoryResponse>();

        result.UpdatedCount.Should().Be(1);
        var other = await factory.WithDbAsync(db => db.Transactions.SingleAsync(t => t.Id == zabka.Items[1].Id));
        other.Category.Should().Be(Category.Groceries);
    }

    [Fact]
    public async Task Patch_rejects_unknown_ids_and_categories()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        var any = await (await client.GetAsync("/api/transactions?limit=1")).ReadAsync<TransactionListResponse>();

        var unknownId = await client.PatchJsonAsync($"/api/transactions/{Guid.NewGuid()}", new { category = "groceries" });
        var badCategory = await client.PatchJsonAsync($"/api/transactions/{any.Items[0].Id}", new { category = "cats" });

        unknownId.StatusCode.Should().Be(HttpStatusCode.NotFound);
        badCategory.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reimporting_the_same_csv_skips_every_row_as_a_duplicate()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("bnpl_heavy");
        var csv = SyntheticDataGenerator.GenerateCsv(Persona.BnplHeavy);
        var countBefore = await factory.WithDbAsync(db => db.Transactions.CountAsync());

        var result = await (await client.UploadCsvAsync(csv)).ReadAsync<ImportResponse>();

        result.Imported.Should().Be(0);
        result.SkippedDuplicates.Should().Be(SyntheticDataGenerator.Generate(Persona.BnplHeavy).Count);
        (await factory.WithDbAsync(db => db.Transactions.CountAsync())).Should().Be(countBefore);
    }

    [Fact]
    public async Task Import_into_an_empty_account_reports_categorization_recurring_and_period()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var user = new User { Id = Guid.NewGuid(), Name = "Test", Persona = Persona.FirstJob, CreatedAt = DateTime.UtcNow };
        await factory.WithDbAsync(async db => { db.Users.Add(user); return await db.SaveChangesAsync(); });
        client.DefaultRequestHeaders.Add("X-User-Id", user.Id.ToString());

        var result = await (await client.UploadCsvAsync(SyntheticDataGenerator.GenerateCsv(Persona.FirstJob))).ReadAsync<ImportResponse>();

        var rows = SyntheticDataGenerator.Generate(Persona.FirstJob).Count;
        result.Imported.Should().Be(rows);
        result.SkippedDuplicates.Should().Be(0);
        var categorized = result.Categorized;
        (categorized.Dictionary + categorized.Fuzzy + categorized.Llm + categorized.Other).Should().Be(rows);
        categorized.Llm.Should().Be(0);
        categorized.Other.Should().BePositive();
        result.RecurringFound.Should().Be(new ImportRecurringFound(4, 1, 28));
        result.Period.Should().Be(new PeriodDto(SyntheticDataGenerator.PeriodStart, SyntheticDataGenerator.PeriodEnd));
    }

    [Fact]
    public async Task Import_rejects_a_malformed_csv()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var response = await client.UploadCsvAsync("date;amount;description;currency\nyesterday;-10,00;ZABKA;PLN\n");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_csv");
    }
}
