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
        RecordCall(
            stage,
            model,
            inputTokens: (int)usage.InputTokens,
            outputTokens: (int)usage.OutputTokens,
            cacheCreationInputTokens: (int)(usage.CacheCreationInputTokens ?? 0),
            cacheReadInputTokens: (int)(usage.CacheReadInputTokens ?? 0));
    }

    /// <summary>
    /// Records a single LLM call from a provider whose SDK doesn't expose Anthropic's
    /// <see cref="Usage"/> shape (Gemini, GitHub Models). Cache-token fields default to zero
    /// since those providers don't report them today.
    /// </summary>
    public void RecordCall(
        string stage,
        string model,
        int inputTokens,
        int outputTokens,
        int cacheCreationInputTokens = 0,
        int cacheReadInputTokens = 0)
    {
        _callCounter++;
        _calls.Add(new CallRecord
        {
            CallNumber = _callCounter,
            Stage = stage,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            Timestamp = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Returns per-call records for a single build.
    /// </summary>
    public IReadOnlyList<CallRecord> GetCalls() => _calls.AsReadOnly();

    /// <summary>
    /// Computes aggregate statistics across all calls in the build. Cost is summed per-call
    /// using each call's own model rate — a mixed-provider build (Anthropic classification +
    /// Gemini selection, or similar) totals correctly.
    /// </summary>
    public UsageSummary GetSummary()
    {
        var totalInputTokens = _calls.Sum(c => c.InputTokens);
        var totalOutputTokens = _calls.Sum(c => c.OutputTokens);
        var totalCacheCreation = _calls.Sum(c => c.CacheCreationInputTokens);
        var totalCacheRead = _calls.Sum(c => c.CacheReadInputTokens);
        var estimatedCost = _calls.Sum(c =>
            ModelPricing.EstimateCost(c.Model, c.InputTokens, c.OutputTokens));

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
            var cost = ModelPricing.EstimateCost(call.Model, call.InputTokens, call.OutputTokens);
            sb.AppendLine($"{call.CallNumber,-6} {call.Stage,-30} {call.Model,-30} {call.InputTokens,-8} {call.OutputTokens,-8} {call.CacheCreationInputTokens,-12} {call.CacheReadInputTokens,-10} ${cost,-9:F4}");
        }

        sb.AppendLine(new string('─', 114));
        var summary = GetSummary();
        sb.AppendLine($"{"TOTAL",-6} {"",-30} {"",-30} {summary.TotalInputTokens,-8} {summary.TotalOutputTokens,-8} {summary.CacheCreationTokens,-12} {summary.CacheReadTokens,-10} ${summary.EstimatedCostUsd,-9:F4}");

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
