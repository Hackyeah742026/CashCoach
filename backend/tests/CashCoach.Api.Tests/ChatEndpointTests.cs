using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CashCoach.Api.Contracts;
using CashCoach.Core.Abstractions;
using FluentAssertions;

namespace CashCoach.Api.Tests;

public class ChatEndpointTests
{
    [Fact]
    public async Task Answer_built_from_tool_results_passes_the_fact_check_and_carries_evidence()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        factory.Llm
            .ThenToolCall("get_balance")
            .ThenText("Masz 1 820,00 zł, a do wypłaty możesz bezpiecznie wydać ok. 302 zł.");

        var reply = await (await client.PostAsJsonAsync("/api/chat", new { message = "Ile mogę wydać do wypłaty?" })).ReadAsync<ChatResponse>();

        reply.FactCheck.Should().Be("passed");
        reply.Fallback.Should().BeFalse();
        reply.ToolsUsed.Should().Equal("get_balance");
        reply.Evidence.Figures.Should().Contain(f => f.Key == "forecast.safe_to_spend" && f.Amount == 302.27m);
        factory.Llm.ChatRequests[0].Tools.Should().HaveCount(10);
        factory.Llm.ChatRequests[1].Messages.Should().Contain(m => m.ToolResults != null && m.ToolResults[0].ResultJson.Contains("\"safe_to_spend\":302.27"));
    }

    [Fact]
    public async Task Invented_numbers_are_retried_twice_and_then_replaced_by_a_template()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        factory.Llm.ThenText("Masz 9 999 zł.").ThenText("Masz 8 888 zł.").ThenText("Masz 7 777 zł.");

        var reply = await (await client.PostAsJsonAsync("/api/chat", new { message = "Ile mam pieniędzy?" })).ReadAsync<ChatResponse>();

        factory.Llm.ChatRequests.Should().HaveCount(3);
        factory.Llm.ChatRequests[1].Messages[^1].Text.Should().Contain("9 999");
        reply.Fallback.Should().BeTrue();
        reply.FactCheck.Should().Be("fallback");
        reply.Answer.Should().Contain("1\u00A0820,00 zł", "pl-PL groups thousands with a non-breaking space").And.NotContain("9 999");
    }

    [Fact]
    public async Task Without_a_model_the_template_answer_still_works_in_english()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("bnpl_heavy");

        var reply = await (await client.PostAsJsonAsync("/api/chat", new { message = "Will my money last?", language = "en" })).ReadAsync<ChatResponse>();

        reply.Fallback.Should().BeTrue();
        reply.Answer.Should().Contain("runs out");
        reply.Evidence.Figures.Should().HaveCount(4);
    }

    [Fact]
    public async Task Conversation_is_stored_and_continues_with_its_history()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        factory.Llm.ThenText("Cześć! W czym mogę pomóc?").ThenText("Jasne.");

        var first = await (await client.PostAsJsonAsync("/api/chat", new { message = "Cześć" })).ReadAsync<ChatResponse>();
        await client.PostAsJsonAsync("/api/chat", new { conversation_id = first.ConversationId, message = "Dzięki" });
        var conversation = await (await client.GetAsync($"/api/chat/{first.ConversationId}")).ReadAsync<ConversationResponse>();

        conversation.Messages.Select(m => m.Role).Should().Equal("user", "assistant", "user", "assistant");
        conversation.Messages[1].FactCheck.Should().Be("passed");
        factory.Llm.ChatRequests[1].Messages.Select(m => m.Text).Should().Equal("Cześć", "Cześć! W czym mogę pomóc?", "Dzięki");
        (await client.GetAsync($"/api/chat/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Account_numbers_are_scrubbed_before_reaching_the_model()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        factory.Llm.ThenText("Nie potrzebuję numeru konta.");

        await client.PostAsJsonAsync("/api/chat", new { message = "Mój numer to PL61 1090 1014 0000 0712 1981 2874" });

        factory.Llm.ChatRequests[0].Messages[^1].Text.Should().Be("Mój numer to [konto]");
    }

    [Fact]
    public async Task Server_sent_events_stream_tools_text_evidence_and_done()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        factory.Llm.ThenToolCall("forecast_until_payday").ThenText("Do wypłaty zostało 10 dni.");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new { messages = new[] { new { role = "user", content = "Czy wystarczy mi?" } }, language = "pl" }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        body.Should().Contain("event: tool\ndata: {\"name\":\"forecast_until_payday\"}");
        body.Should().Contain("event: delta");
        body.Should().Contain("event: evidence");
        body.Should().Contain("\"fact_check\":\"passed\"");
    }

    [Fact]
    public async Task Suggestions_are_personalized()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("first_job");

        var suggestions = await (await client.GetAsync("/api/chat/suggestions")).ReadAsync<ChatSuggestionsResponse>();

        suggestions.Items.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task Chat_is_rate_limited_per_user()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 21; i++)
        {
            statuses.Add((await client.GetAsync("/api/chat/suggestions")).StatusCode);
        }

        statuses.Take(20).Should().OnlyContain(s => s == HttpStatusCode.OK);
        statuses[^1].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Empty_messages_are_rejected()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        (await client.PostAsJsonAsync("/api/chat", new { message = "  " })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
