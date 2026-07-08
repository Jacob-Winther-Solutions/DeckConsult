namespace EdhDeckBuilder.Agent.Authentication;

public readonly record struct KeyTestResult(bool Ok, string? Error);

public interface IClaudeKeyTester
{
    Task<KeyTestResult> TestAsync(string apiKey, AiProvider provider, CancellationToken ct = default);
}
