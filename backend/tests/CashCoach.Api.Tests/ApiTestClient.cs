using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CashCoach.Api.Contracts;
using CashCoach.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CashCoach.Api.Tests;

/// <summary>Helpers for calling the real API with the production JSON conventions.</summary>
public static class ApiTestClient
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static async Task<UserProfileResponse> LoginAsync(this HttpClient client, string persona)
    {
        var response = await client.PostAsJsonAsync("/api/demo/login", new { persona });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserProfileResponse>(Json))!;
    }

    /// <summary>A client that sends <c>X-User-Id</c> for the persona's demo user, logging in first.</summary>
    public static async Task<HttpClient> CreateUserClientAsync(this ApiFactory factory, string persona)
    {
        var client = factory.CreateClient();
        var user = await client.LoginAsync(persona);
        client.DefaultRequestHeaders.Add("X-User-Id", user.UserId.ToString());
        return client;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object body) =>
        client.PatchAsync(url, new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json"));

    public static Task<HttpResponseMessage> UploadCsvAsync(this HttpClient client, string csv)
    {
        var content = new MultipartFormDataContent { { new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "statement.csv" } };
        return client.PostAsync("/api/import", content);
    }

    public static async Task<T> WithDbAsync<T>(this ApiFactory factory, Func<AppDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
