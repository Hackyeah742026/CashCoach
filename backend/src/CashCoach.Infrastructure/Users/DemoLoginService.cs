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
}
