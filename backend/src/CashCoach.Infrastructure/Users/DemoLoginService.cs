using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Import;
using CashCoach.Infrastructure.Persistence;
using CashCoach.Infrastructure.SyntheticData;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Users;

/// <summary>Demo users have fixed ids per persona, so logging in again reuses the same user.</summary>
public static class DemoPersonas
{
    public static Guid IdOf(Persona persona) => persona switch
    {
        Persona.Student => new Guid("de000000-0000-0000-0000-000000000001"),
        Persona.FirstJob => new Guid("de000000-0000-0000-0000-000000000002"),
        Persona.BnplHeavy => new Guid("de000000-0000-0000-0000-000000000003"),
        _ => throw new ArgumentOutOfRangeException(nameof(persona), persona, null),
    };

    public static string NameOf(Persona persona) => persona switch
    {
        Persona.Student => "Ola",
        Persona.FirstJob => "Kuba",
        Persona.BnplHeavy => "Maja",
        _ => throw new ArgumentOutOfRangeException(nameof(persona), persona, null),
    };

    /// <summary>
    /// Account balance on the last day of the synthetic history (CSV exports carry no balance).
    /// Chosen so the forecast tells each persona's story: student tight, first job fine, BNPL-heavy runs out before payday.
    /// </summary>
    public static long BalanceOf(Persona persona) => persona switch
    {
        Persona.Student => 182_000,
        Persona.FirstJob => 610_000,
        Persona.BnplHeavy => 210_000,
        _ => throw new ArgumentOutOfRangeException(nameof(persona), persona, null),
    };
}

public sealed class DemoLoginService(
    AppDbContext db, TransactionImportService importService, UserProfileService profiles, TimeProvider timeProvider)
{
    /// <summary>Creates the persona's demo user on first login and imports its synthetic CSV while the user has no transactions.</summary>
    public async Task<UserProfile> LoginAsync(Persona persona, CancellationToken cancellationToken)
    {
        var userId = DemoPersonas.IdOf(persona);
        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Id = userId,
                Name = DemoPersonas.NameOf(persona),
                Persona = persona,
                Language = "pl",
                BalanceGr = DemoPersonas.BalanceOf(persona),
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Transactions.AnyAsync(t => t.UserId == userId, cancellationToken))
        {
            await using var csv = SyntheticDataGenerator.GenerateCsvStream(persona);
            await importService.ImportAsync(userId, csv, cancellationToken);
        }

        return await profiles.GetAsync(user, cancellationToken);
    }

    /// <summary>A new user without data, for people who import their own CSV.</summary>
    public async Task<UserProfile> CreateUserAsync(string? name, string language, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? (language == "en" ? "You" : "Ty") : name.Trim(),
            Persona = Persona.Custom,
            Language = language,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return await profiles.GetAsync(user, cancellationToken);
    }

    /// <summary>
    /// Imports a persona's synthetic history into an existing user ("try it with demo data").
    /// The import guesses the payday (unless the user already confirmed their income); the persona's balance is used unless the user already set one.
    /// </summary>
    public async Task<ImportResult> ImportDemoAsync(Guid userId, Persona persona, CancellationToken cancellationToken)
    {
        await using var csv = SyntheticDataGenerator.GenerateCsvStream(persona);
        var result = await importService.ImportAsync(userId, csv, cancellationToken);

        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        user.BalanceGr ??= DemoPersonas.BalanceOf(persona);
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }
}
