using Anthropic.Models.Messages;

namespace EdhDeckBuilder.Agent.Instrumentation;

/// <summary>
/// Tracks token usage across a single build invocation.
/// Captures per-call and aggregate statistics for cost analysis.
/// </summary>
public sealed class UsageTracker
{
    private readonly List<CallRecord> _calls = [];
    private int _callCounter = 0;

    /// <summary>
    /// Records a single LLM call with its usage metadata.
    /// </summary>
    public void RecordCall(string stage, string model, Usage usage)
    {
        _callCounter++;
        _calls.Add(new CallRecord
        {
            CallNumber = _callCounter,
            Stage = stage,
            Model = model,
            InputTokens = (int)usage.InputTokens,
            OutputTokens = (int)usage.OutputTokens,
            CacheCreationInputTokens = (int)(usage.CacheCreationInputTokens ?? 0),
            CacheReadInputTokens = (int)(usage.CacheReadInputTokens ?? 0),
            Timestamp = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Returns per-call records for a single build.
    /// </summary>
    public IReadOnlyList<CallRecord> GetCalls() => _calls.AsReadOnly();

    /// <summary>
    /// Computes aggregate statistics across all calls in the build.
    /// </summary>
    public UsageSummary GetSummary()
    {
        var totalInputTokens = _calls.Sum(c => c.InputTokens);
        var totalOutputTokens = _calls.Sum(c => c.OutputTokens);
        var totalCacheCreation = _calls.Sum(c => c.CacheCreationInputTokens);
        var totalCacheRead = _calls.Sum(c => c.CacheReadInputTokens);

        // Haiku pricing: $1 per MTok input, $5 per MTok output
        var inputCost = (totalInputTokens * 1m) / 1_000_000m;
        var outputCost = (totalOutputTokens * 5m) / 1_000_000m;
        var estimatedCost = inputCost + outputCost;

        return new UsageSummary
        {
            CallCount = _calls.Count,
            TotalInputTokens = totalInputTokens,
            TotalOutputTokens = totalOutputTokens,
            CacheCreationTokens = totalCacheCreation,
            CacheReadTokens = totalCacheRead,
            EstimatedCostUsd = estimatedCost,
        };
    }

    /// <summary>
    /// Formats a detailed table of per-call usage for logging.
    /// </summary>
    public string FormatTable()
    {
        if (_calls.Count == 0)
            return "(no calls recorded)";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{"Call",-6} {"Stage",-30} {"Model",-30} {"Input",-8} {"Output",-8} {"CacheCreate",-12} {"CacheRead",-10} {"EstCost",-10}");
        sb.AppendLine(new string('─', 114));

        foreach (var call in _calls)
        {
            var cost = (call.InputTokens * 1m + call.OutputTokens * 5m) / 1_000_000m;
            sb.AppendLine($"{call.CallNumber,-6} {call.Stage,-30} {call.Model,-30} {call.InputTokens,-8} {call.OutputTokens,-8} {call.CacheCreationInputTokens,-12} {call.CacheReadInputTokens,-10} ${cost:F4,-9}");
        }

        sb.AppendLine(new string('─', 114));
        var summary = GetSummary();
        sb.AppendLine($"{"TOTAL",-6} {"",-30} {"",-30} {summary.TotalInputTokens,-8} {summary.TotalOutputTokens,-8} {summary.CacheCreationTokens,-12} {summary.CacheReadTokens,-10} ${summary.EstimatedCostUsd:F4,-9}");

        return sb.ToString();
    }

    public sealed class CallRecord
    {
        public required int CallNumber { get; init; }
        public required string Stage { get; init; }
        public required string Model { get; init; }
        public required int InputTokens { get; init; }
        public required int OutputTokens { get; init; }
        public required int CacheCreationInputTokens { get; init; }
        public required int CacheReadInputTokens { get; init; }
        public required DateTime Timestamp { get; init; }
    }
}

public sealed class UsageSummary
{
    public required int CallCount { get; init; }
    public required int TotalInputTokens { get; init; }
    public required int TotalOutputTokens { get; init; }
    public required int CacheCreationTokens { get; init; }
    public required int CacheReadTokens { get; init; }
    public required decimal EstimatedCostUsd { get; init; }
}
