using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Api.Tests.Persistence;

public sealed class AppDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;

    public AppDbContextTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public void EnsureCreated_creates_all_tables()
    {
        var tables = _db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'")
            .ToList();

        tables.Should().BeEquivalentTo(
            "users", "transactions", "recurring_groups", "goals", "challenges", "chat_messages", "user_merchant_rules", "dismissals");
    }

    [Fact]
    public async Task Enums_are_stored_as_snake_case_strings_and_round_trip()
    {
        var user = new User { Id = Guid.NewGuid(), Name = "Ola", Persona = Persona.FirstJob, CreatedAt = DateTime.UtcNow };
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Date = new DateOnly(2026, 8, 14),
            AmountGr = -4_250,
            RawDescription = "GLOVO*ORDER 123",
            Merchant = "Glovo",
            Category = Category.FoodDelivery,
            Channel = Channel.Blik,
        };
        _db.AddRange(user, transaction);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.Database
            .SqlQueryRaw<string>("SELECT category || '|' || channel AS Value FROM transactions")
            .SingleAsync();
        var persona = await _db.Database.SqlQueryRaw<string>("SELECT persona AS Value FROM users").SingleAsync();
        var loaded = await _db.Transactions.SingleAsync();

        stored.Should().Be("food_delivery|blik");
        persona.Should().Be("first_job");
        loaded.Category.Should().Be(Category.FoodDelivery);
        loaded.AmountGr.Should().Be(-4_250);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
