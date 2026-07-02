namespace EdhDeckBuilder.Web.Services;

/// <summary>
/// Cross-component notification channel for API key connect/disconnect events.
/// Scoped per Blazor Server circuit so only components in the same circuit are notified.
/// </summary>
public interface IApiKeyStateService
{
    event Action OnChange;
    void NotifyChanged();
}

public sealed class ApiKeyStateService : IApiKeyStateService
{
    public event Action OnChange = delegate { };
    public void NotifyChanged() => OnChange.Invoke();
}
