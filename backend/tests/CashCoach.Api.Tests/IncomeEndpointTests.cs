using System.Net;
using System.Net.Http.Json;
using CashCoach.Api.Contracts;
using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Api.Tests;

public class IncomeEndpointTests
{
    [Theory]
    [InlineData("student", "Stypendium", IncomeKind.Stipend, 10, 1650)]
    [InlineData("first_job", "Wynagrodzenie", IncomeKind.Salary, 28, 5600)]
    [InlineData("bnpl_heavy", "Wynagrodzenie", IncomeKind.Salary, 15, 4300)]
    public async Task Detection_finds_each_personas_income(string persona, string source, IncomeKind kind, int day, int amount)
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync(persona);

        var detection = await (await client.GetAsync("/api/income/detection")).ReadAsync<IncomeDetectionResponse>();

        detection.Guess.Should().BeEquivalentTo(new { Source = source, Kind = kind, Day = day, Amount = (decimal)amount, MonthsSeen = 3, Confidence = DetectionConfidence.High });
        detection.Guess!.Evidence.TransactionIds.Should().HaveCount(3);
        detection.Confirmed.Should().BeNull();
        if (persona == "student")
        {
            detection.Others.Should().ContainSingle(o => o.Source == "Kieszonkowe" && o.Amount == 1300m);
        }
    }

    [Fact]
    public async Task Confirmed_income_drives_the_forecast_and_survives_new_imports()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("first_job");

        var me = await (await client.PutAsJsonAsync("/api/me/income", new { has_income = true, day = 25, amount = 5000m, source = "ACME" })).ReadAsync<UserProfileResponse>();
        await client.PostAsJsonAsync("/api/import/demo", new { persona = "bnpl_heavy" });
        var forecast = await (await client.GetAsync("/api/forecast")).ReadAsync<ForecastResponse>();
        var detection = await (await client.GetAsync("/api/income/detection")).ReadAsync<IncomeDetectionResponse>();

        me.Income.Should().Be(new IncomeDto(IncomeStatus.Confirmed, 25, PaydayRule.FixedDay, 5000m, "ACME"));
        forecast.NextPayday.Should().Be(new DateOnly(2026, 10, 25), "a later import must not overwrite a confirmed payday");
        detection.Confirmed!.Amount.Should().Be(5000m);
    }

    [Fact]
    public async Task No_regular_income_forecasts_to_the_end_of_the_month_and_last_working_day_is_supported()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var none = await (await client.PutAsJsonAsync("/api/me/income", new { has_income = false })).ReadAsync<UserProfileResponse>();
        var noneForecast = await (await client.GetAsync("/api/forecast")).ReadAsync<ForecastResponse>();
        await client.PutAsJsonAsync("/api/me/income", new { has_income = true, day_rule = "last_working_day", amount = 3000m });
        var lastWorkingDay = await (await client.GetAsync("/api/forecast")).ReadAsync<ForecastResponse>();

        none.Income.Status.Should().Be(IncomeStatus.None);
        none.Payday.Should().BeNull();
        noneForecast.NextPayday.Should().Be(new DateOnly(2026, 10, 1));
        lastWorkingDay.NextPayday.Should().Be(new DateOnly(2026, 10, 30));
    }

    [Fact]
    public async Task Invalid_income_answers_are_rejected()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        (await client.PutAsJsonAsync("/api/me/income", new { day = 10, amount = 100m })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/me/income", new { has_income = true, day = 0, amount = 100m })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/me/income", new { has_income = true, day = 10 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/me/income", new { has_income = true, day = 10, amount = 100m, day_rule = "weekly" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_balance_column_in_the_csv_becomes_the_users_balance()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var user = await (await client.PostAsJsonAsync("/api/users", new { language = "pl" })).ReadAsync<UserProfileResponse>();
        client.DefaultRequestHeaders.Add("X-User-Id", user.UserId.ToString());
        user.BalanceIsEstimate.Should().BeTrue();

        // Newest first, as mBank exports it: the first row's balance is the current one.
        var csv = "date;amount;description;currency;balance\n" +
                  "2026-09-12;-20,00;ZABKA Z1234 KRAKOW;PLN;1 580,50\n" +
                  "2026-09-12;-30,00;LIDL POLSKA 1234;PLN;1 600,50\n" +
                  "2026-09-10;4500,00;PRZELEW PRZYCHODZACY WYNAGRODZENIE;PLN;1 630,50\n";
        var import = await (await client.UploadCsvAsync(csv)).ReadAsync<ImportResponse>();
        var me = await (await client.GetAsync("/api/me")).ReadAsync<UserProfileResponse>();

        import.DetectedBalance.Should().Be(1580.50m);
        me.Balance.Should().Be(1580.50m);
        me.BalanceIsEstimate.Should().BeFalse();
        me.Income.Status.Should().Be(IncomeStatus.Unknown);
    }
}
