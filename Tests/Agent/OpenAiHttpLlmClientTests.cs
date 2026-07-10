using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Agent.Llm.OpenAI;
using EdhDeckBuilder.Agent.Llm.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EdhDeckBuilder.Tests.Agent;

public sealed class OpenAiHttpLlmClientTests
{
    // ── BuildRequestJson ──────────────────────────────────────────────────────

    [Fact]
    public void BuildRequestJson_StandardModel_UsesMaxTokensAndTemperature()
    {
        var request = new LlmRequest
        {
            Model       = "gpt-4o-mini",
            MaxTokens   = 8192,
            Temperature = 0.6,
            Messages    = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = "hi" }] }],
        };

        var json = OpenAiHttpLlmClient.BuildRequestJson(request);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(8192, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.6,  root.GetProperty("temperature").GetDouble(), precision: 9);
        Assert.False(root.TryGetProperty("max_completion_tokens", out _));
    }

    [Theory]
    [InlineData("o4-mini")]
    [InlineData("o3")]
    [InlineData("o1")]
    public void BuildRequestJson_ReasoningModel_UsesMaxCompletionTokensAndOmitsTemperature(string model)
    {
        var request = new LlmRequest
        {
            Model       = model,
            MaxTokens   = 4096,
            Temperature = 0.6,
            Messages    = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = "hi" }] }],
        };

        var json = OpenAiHttpLlmClient.BuildRequestJson(request);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(4096, root.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(root.TryGetProperty("max_tokens",   out _));
        Assert.False(root.TryGetProperty("temperature",  out _));
    }

    [Fact]
    public void BuildRequestJson_SystemPrompt_AppearsAsSystemRoleMessage()
    {
        var request = new LlmRequest
        {
            Model        = "gpt-4o-mini",
            MaxTokens    = 100,
            SystemPrompt = "You are a helpful assistant.",
            Messages     = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = "hi" }] }],
        };

        var json = OpenAiHttpLlmClient.BuildRequestJson(request);
        using var doc = JsonDocument.Parse(json);
        var messages = doc.RootElement.GetProperty("messages");

        Assert.Equal("system",                      messages[0].GetProperty("role").GetString());
        Assert.Equal("You are a helpful assistant.", messages[0].GetProperty("content").GetString());
        Assert.Equal("user",                         messages[1].GetProperty("role").GetString());
    }

    [Fact]
    public void BuildRequestJson_ForcedTool_EmitsCorrectToolChoiceShape()
    {
        var schema = JsonNode.Parse("""{"type":"object","properties":{}}""")!;
        var request = new LlmRequest
        {
            Model          = "gpt-4o-mini",
            MaxTokens      = 100,
            Messages       = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = "hi" }] }],
            Tools          = [new LlmToolDefinition { Name = "my_tool", Description = "desc", InputSchema = schema }],
            ForcedToolName = "my_tool",
        };

        var json = OpenAiHttpLlmClient.BuildRequestJson(request);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Tool definition wrapped in {type:function, function:{...}}
        var tool = root.GetProperty("tools")[0];
        Assert.Equal("function",  tool.GetProperty("type").GetString());
        Assert.Equal("my_tool",   tool.GetProperty("function").GetProperty("name").GetString());

        // tool_choice: {type:function, function:{name:my_tool}}
        var tc = root.GetProperty("tool_choice");
        Assert.Equal("function", tc.GetProperty("type").GetString());
        Assert.Equal("my_tool",  tc.GetProperty("function").GetProperty("name").GetString());
    }

    // ── ParseResponse ─────────────────────────────────────────────────────────

    [Fact]
    public void ParseResponse_ToolCallResponse_ExtractsToolUseBlock()
    {
        var responseJson = """
            {
              "choices": [{
                "finish_reason": "tool_calls",
                "message": {
                  "role": "assistant",
                  "tool_calls": [{
                    "id": "call_abc",
                    "type": "function",
                    "function": {
                      "name": "classify_cards",
                      "arguments": "{\"classifications\": []}"
                    }
                  }]
                }
              }],
              "usage": { "prompt_tokens": 100, "completion_tokens": 50 }
            }
            """;

        var response = OpenAiHttpLlmClient.ParseResponse(responseJson);

        Assert.Equal("tool_use", response.StopReason);
        Assert.Equal(100, response.Usage.InputTokens);
        Assert.Equal(50,  response.Usage.OutputTokens);

        var toolBlock = Assert.IsType<LlmToolUseBlock>(response.Content.Single());
        Assert.Equal("call_abc",       toolBlock.Id);
        Assert.Equal("classify_cards", toolBlock.ToolName);
        Assert.NotNull(toolBlock.Input["classifications"]);
    }

    [Fact]
    public void ParseResponse_LengthFinishReason_MapsToMaxTokens()
    {
        var responseJson = """
            {
              "choices": [{ "finish_reason": "length", "message": { "content": "partial" } }],
              "usage": { "prompt_tokens": 10, "completion_tokens": 5 }
            }
            """;

        var response = OpenAiHttpLlmClient.ParseResponse(responseJson);

        Assert.Equal("max_tokens", response.StopReason);
    }

    [Fact]
    public void ParseResponse_StopFinishReason_MapsToEndTurn()
    {
        var responseJson = """
            {
              "choices": [{ "finish_reason": "stop", "message": { "content": "hello" } }],
              "usage": { "prompt_tokens": 5, "completion_tokens": 3 }
            }
            """;

        var response = OpenAiHttpLlmClient.ParseResponse(responseJson);

        Assert.Equal("end_turn", response.StopReason);
        var text = Assert.IsType<LlmTextBlock>(response.Content.Single());
        Assert.Equal("hello", text.Text);
    }

    // ── 401 → ApiKeyRejectedException ────────────────────────────────────────

    [Fact]
    public async Task SendAsync_401Response_ThrowsApiKeyRejectedException()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.Unauthorized,
            """{"error":{"message":"Incorrect API key provided."}}""");

        using var http   = new HttpClient(handler);
        var client = new OpenAiHttpLlmClient(http, "sk-bad", NullLogger<OpenAiHttpLlmClient>.Instance);

        await Assert.ThrowsAsync<ApiKeyRejectedException>(() =>
            client.SendAsync(new LlmRequest
            {
                Model     = "gpt-4o-mini",
                MaxTokens = 1,
                Messages  = [new LlmMessage { Role = LlmRole.User, Content = [new LlmTextBlock { Text = "hi" }] }],
            }));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class FakeHttpHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
