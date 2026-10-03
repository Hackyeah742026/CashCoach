using CashCoach.Api.Configuration;
using CashCoach.Api.Endpoints;
using CashCoach.Api.Users;
using CashCoach.Infrastructure;
using CashCoach.Infrastructure.SyntheticData;
using Scalar.AspNetCore;

// `dotnet run -- --generate-samples [dir]` rewrites the synthetic CSVs in data/samples and exits.
if (args is ["--generate-samples", .. var rest])
{
    var directory = rest.Length > 0 ? rest[0] : SampleCsvWriter.FindSamplesDirectory(Directory.GetCurrentDirectory());
    foreach (var path in SampleCsvWriter.WriteAll(directory))
    {
        Console.WriteLine($"Wrote {path}");
    }

    return;
}

// Load the repo-root .env (gitignored) before configuration is built.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCashCoachApi(builder.Configuration);
builder.Services.AddCashCoachDatabase();
builder.Services.AddCashCoachDataPipeline();

var app = builder.Build();
await app.EnsureDatabaseCreatedAsync();
app.UseCashCoachApi();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

var api = app.MapGroup("/api");
api.MapHealthEndpoints();
api.MapDemoEndpoints();

var userApi = api.MapGroup("").RequireCurrentUser();
userApi.MapMeEndpoints();
userApi.MapImportEndpoints();
userApi.MapTransactionEndpoints();
userApi.MapInsightEndpoints();
userApi.MapAnalyticsEndpoints();
userApi.MapHomeEndpoints();
userApi.MapWrappedEndpoints();
userApi.MapGoalEndpoints();
userApi.MapChallengeEndpoints();
userApi.MapChatEndpoints();

app.Run();

public partial class Program;
