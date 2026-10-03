using System.Net;
using System.Net.Http.Json;
using CashCoach.Api.Contracts;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Api.Tests;

public class UserEndpointTests
{
    [Fact]
    public async Task Demo_login_creates_the_persona_user_with_imported_transactions()
    {
        await using var factory = new ApiFactory();

        var profile = await factory.CreateClient().LoginAsync("first_job");

        profile.Should().BeEquivalentTo(new
        {
            UserId = DemoPersonas.IdOf(Persona.FirstJob),
            Name = "Kuba",
            Persona = Persona.FirstJob,
            Language = "pl",
            HasConsent = false,
            HasData = true,
            Payday = 28,
            Balance = 6100.00m,
            BalanceIsEstimate = false,
            AsOf = new DateOnly(2026, 9, 30),
            AvailableMonths = new[] { "2026-07", "2026-08", "2026-09" },
        });
        var count = await factory.WithDbAsync(db => db.Transactions.CountAsync(t => t.UserId == profile.UserId));
        count.Should().BeInRange(100, 250);
    }

    [Fact]
    public async Task Second_demo_login_reuses_the_user_without_duplicating_transactions()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var first = await client.LoginAsync("student");
        var countAfterFirst = await factory.WithDbAsync(db => db.Transactions.CountAsync());
        var second = await client.LoginAsync("student");

        second.UserId.Should().Be(first.UserId);
        (await factory.WithDbAsync(db => db.Transactions.CountAsync())).Should().Be(countAfterFirst);
        (await factory.WithDbAsync(db => db.Users.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Demo_login_rejects_an_unknown_persona()
    {
        await using var factory = new ApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/demo/login", new { persona = "millionaire" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_persona");
    }

    [Fact]
    public async Task Me_without_user_header_returns_401()
    {
        await using var factory = new ApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("missing_user_id");
    }

    [Fact]
    public async Task Me_can_be_read_and_updated()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var me = await (await client.GetAsync("/api/me")).ReadAsync<UserProfileResponse>();
        var updated = await (await client.PatchJsonAsync("/api/me", new { language = "en", name = "Aleksandra" })).ReadAsync<UserProfileResponse>();

        me.Name.Should().Be("Ola");
        updated.Should().BeEquivalentTo(me with { Name = "Aleksandra", Language = "en" });
        (await client.PatchJsonAsync("/api/me", new { language = "de" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var settings = await (await client.PatchJsonAsync("/api/me", new { payday = 12, safety_buffer = 150.50m, balance = 999.99m })).ReadAsync<UserProfileResponse>();
        settings.Payday.Should().Be(12);
        settings.SafetyBuffer.Should().Be(150.50m);
        settings.Balance.Should().Be(999.99m);
        (await client.PatchJsonAsync("/api/me", new { payday = 32 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PatchJsonAsync("/api/me", new { safety_buffer = 1.005m })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Consent_must_be_accepted_and_is_then_reflected_in_the_profile()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var rejected = await client.PostAsJsonAsync("/api/me/consent", new { accepted = false, scopes = new[] { "transactions" } });
        var accepted = await (await client.PostAsJsonAsync("/api/me/consent", new { accepted = true, scopes = new[] { "transactions", "ai_coach" } }))
            .ReadAsync<ConsentResponse>();
        var me = await (await client.GetAsync("/api/me")).ReadAsync<UserProfileResponse>();

        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        accepted.Scopes.Should().Equal("transactions", "ai_coach");
        accepted.ConsentAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        me.HasConsent.Should().BeTrue();
    }
}
