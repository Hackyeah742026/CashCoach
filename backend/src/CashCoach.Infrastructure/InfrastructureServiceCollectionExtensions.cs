using CashCoach.Core.Abstractions;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Ai;
using CashCoach.Infrastructure.Ai.Tools;
using CashCoach.Infrastructure.Analytics;
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
        services.AddMemoryCache();
        services.AddSingleton<ILlmClient, GeminiClient>();
        services.AddSingleton<ILlmCategorizer, GeminiCategorizer>();
        services.AddSingleton<AiCopywriter>();
        services.AddSingleton<WrappedCaptionService>();
        services.AddScoped<ToolRegistry>();
        services.AddScoped<ChatAgent>();
        services.AddSingleton<Categorizer>();

        services.AddScoped<RecurringSyncService>();
        services.AddScoped<TransactionImportService>();
        services.AddScoped<UserProfileService>();
        services.AddScoped<DemoLoginService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<InsightService>();
        services.AddScoped<UserDataService>();

        services.AddScoped<SnapshotLoader>();
        services.AddScoped<DismissalService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<GoalService>();
        services.AddScoped<ChallengeService>();

        return services;
    }
}
