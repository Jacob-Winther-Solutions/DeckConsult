using EdhDeckBuilder.Agent.Authentication;

namespace EdhDeckBuilder.Web.Services;

/// <summary>
/// Translates agent-layer exceptions to concise, user-facing error strings.
/// </summary>
internal static class LlmErrorMessages
{
    /// <summary>
    /// Returns a friendly message for a <see cref="QuotaExceededException"/>.
    /// Distinguishes between a per-minute rate limit (wait and retry) and a billing
    /// quota exhaustion (add credits), based on keywords in the raw API message.
    /// </summary>
    internal static string ForQuotaException(QuotaExceededException ex)
    {
        var raw = ex.Message ?? "";

        // Google free-tier per-minute rate limit (quotaId contains "FreeTier", limit > 0)
        if (raw.Contains("FreeTier", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("free_tier", StringComparison.OrdinalIgnoreCase))
        {
            // Try to extract the retry delay if Google gave one
            var retryHint = "";
            var retryMatch = System.Text.RegularExpressions.Regex.Match(raw, @"""retryDelay"":\s*""(\d+)s""");
            if (retryMatch.Success && int.TryParse(retryMatch.Groups[1].Value, out var delaySec))
                retryHint = $" Please wait {delaySec} seconds and try again.";
            else
                retryHint = " Please wait a minute and try again.";

            return "You've hit the Google AI free-tier request limit." + retryHint
                 + " To remove this limit, add billing to your Google AI Studio account.";
        }

        // Billing quota exhausted (limit: 0 in the details, or generic RESOURCE_EXHAUSTED)
        if (raw.Contains("billing", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("\"limit\": 0", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("\"limit\":0", StringComparison.OrdinalIgnoreCase))
        {
            return "Your AI provider billing quota is exhausted. "
                 + "Please add credits to your account to continue.";
        }

        // OpenAI / Anthropic quota — generic
        if (raw.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
        {
            return "Your AI provider quota has been exceeded. "
                 + "Please check your usage limits or wait and try again.";
        }

        // Fallback — still better than the raw API dump
        return "Your AI provider request limit was reached. Please wait a moment and try again.";
    }
}
