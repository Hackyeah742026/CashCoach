using CashCoach.Core.Abstractions;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Categorization;
using CashCoach.Infrastructure.Import;
using CashCoach.Infrastructure.Insights;
using CashCoach.Infrastructure.Recurring;
using CashCoach.Infrastructure.Transactions;
using CashCoach.Infrastructure.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CashCoach.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Import pipeline (dictionary, categorizer, recurring detection) and the user-scoped data services.</summary>
    public static IServiceCollection AddCashCoachDataPipeline(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IMerchantDictionary, JsonMerchantDictionary>();
        services.AddSingleton<ILlmCategorizer, NoOpLlmCategorizer>();
        services.AddSingleton<Categorizer>();

        services.AddScoped<RecurringSyncService>();
        services.AddScoped<TransactionImportService>();
        services.AddScoped<UserProfileService>();
        services.AddScoped<DemoLoginService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<InsightService>();

        return services;
    }
}
