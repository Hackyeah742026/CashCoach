using System.Text.Json;
using CashCoach.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<RecurringGroup> RecurringGroups => Set<RecurringGroup>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<UserMerchantRule> UserMerchantRules => Set<UserMerchantRule>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Category>().HaveConversion<SnakeCaseEnumConverter<Category>>();
        configurationBuilder.Properties<Channel>().HaveConversion<SnakeCaseEnumConverter<Channel>>();
        configurationBuilder.Properties<RecurringType>().HaveConversion<SnakeCaseEnumConverter<RecurringType>>();
        configurationBuilder.Properties<Persona>().HaveConversion<SnakeCaseEnumConverter<Persona>>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.Name).IsRequired();
            user.Property(u => u.Language).HasMaxLength(2);
        });

        modelBuilder.Entity<Transaction>(transaction =>
        {
            transaction.ToTable("transactions");
            transaction.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            transaction.HasOne<RecurringGroup>().WithMany().HasForeignKey(t => t.RecurringGroupId).OnDelete(DeleteBehavior.SetNull);
            transaction.HasIndex(t => new { t.UserId, t.Date });
        });

        modelBuilder.Entity<RecurringGroup>(group =>
        {
            group.ToTable("recurring_groups");
            group.HasOne<User>().WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Goal>(goal =>
        {
            goal.ToTable("goals");
            goal.HasOne<User>().WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Challenge>(challenge =>
        {
            challenge.ToTable("challenges");
            challenge.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(message =>
        {
            message.ToTable("chat_messages");
            message.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
            message.HasIndex(m => new { m.UserId, m.ConversationId, m.CreatedAt });
        });

        modelBuilder.Entity<UserMerchantRule>(rule =>
        {
            rule.ToTable("user_merchant_rules");
            rule.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            rule.HasIndex(r => new { r.UserId, r.Merchant }).IsUnique();
        });

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            property.SetColumnName(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
        }
    }
}
