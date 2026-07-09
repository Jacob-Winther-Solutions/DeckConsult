namespace EdhDeckBuilder.Agent.Llm.Shared;

public interface ILlmClient
{
    Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default);
}
