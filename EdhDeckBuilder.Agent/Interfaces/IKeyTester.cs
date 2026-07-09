using EdhDeckBuilder.Agent.Authentication;

namespace EdhDeckBuilder.Agent.Interfaces;

public readonly record struct KeyTestResult(bool Ok, string? Error);

public interface IKeyTester
{
    Task<KeyTestResult> TestAsync(string apiKey, AiProvider provider, CancellationToken ct = default);
}
