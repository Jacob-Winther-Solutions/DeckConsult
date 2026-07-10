namespace EdhDeckBuilder.Agent.Authentication;

/// <summary>
/// Thrown when a provider returns a quota or billing limit error (HTTP 429 with a
/// non-retriable quota body). Distinct from <see cref="ApiKeyRejectedException"/> (auth
/// failure) and transient rate-limit 429s (which are retried automatically).
/// </summary>
public sealed class QuotaExceededException(string message, Exception? inner = null)
    : Exception(message, inner);
