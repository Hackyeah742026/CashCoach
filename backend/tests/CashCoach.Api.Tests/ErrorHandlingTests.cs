using System.Net;
using System.Text;
using System.Text.Json;
using CashCoach.Api.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CashCoach.Api.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public async Task ApiException_returns_documented_error_body_and_status()
    {
        await using var host = await TestApiHost.StartAsync(api =>
            api.MapGet("/goal", IResult () => throw new ApiException("goal_not_found", "Goal not found.", StatusCodes.Status404NotFound)));

        var response = await host.Client.GetAsync("/api/goal");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync())
            .Should().Be("""{"error":{"code":"goal_not_found","message":"Goal not found."}}""");
    }

    [Fact]
    public async Task Unhandled_exception_returns_generic_internal_error_without_leaking_details()
    {
        await using var host = await TestApiHost.StartAsync(api =>
            api.MapGet("/crash", IResult () => throw new InvalidOperationException("secret connection detail")));

        var response = await host.Client.GetAsync("/api/crash");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("secret connection detail");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be("internal_error");
    }

    [Fact]
    public async Task Malformed_json_body_returns_documented_bad_request_error()
    {
        await using var host = await TestApiHost.StartAsync(api =>
            api.MapPost("/echo", (EchoRequest request) => Results.Ok(request)));

        using var content = new StringContent("{", Encoding.UTF8, "application/json");
        var response = await host.Client.PostAsync("/api/echo", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should().Be("""{"error":{"code":"bad_request","message":"The request is malformed."}}""");
    }

    public sealed record EchoRequest(string Name);
}
