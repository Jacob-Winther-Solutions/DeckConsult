using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;

namespace EdhDeckBuilder.Web.Components.Shared;

public partial class ApiKeySettings : ComponentBase
{
    private const string CookieNameAnthropic = "edh_apikey";
    private const string CookieNameGitHub    = "edh_apikey_gh";
    private const int    CookieDays          = 30;

    [Inject] private SessionApiKeyProvider   Keys       { get; set; } = default!;
    [Inject] private IClaudeKeyTester        Tester     { get; set; } = default!;
    [Inject] private IDataProtectionProvider DpProvider { get; set; } = default!;
    [Inject] private IJSRuntime              JS         { get; set; } = default!;
    [Inject] private IApiKeyStateService     ApiKeyState { get; set; } = default!;

    private string       _keyInput      = "";
    private bool         _connected;
    private bool         _showForm;
    private bool         _remember      = true;
    private bool         _busy;
    private bool         _error;
    private string?      _message;
    private string       _selectedModel = ClaudeModels.Haiku;
    private AiProvider   _selectedProvider = AiProvider.Anthropic;

    private IDataProtector Protector =>
        DpProvider.CreateProtector("EdhDeckBuilder.ApiKey");

    protected override void OnInitialized()
    {
        _connected         = Keys.GetApiKey() is not null;
        _selectedProvider  = Keys.ActiveProvider;

        // If provider doesn't have the currently selected model, pick the first valid model for this provider
        var validModels = ClaudeModels.GetSelectionModels(_selectedProvider);
        var currentModel = Keys.SelectedModel;
        if (validModels.Any(m => m.Id == currentModel))
        {
            _selectedModel = currentModel;
        }
        else
        {
            _selectedModel = validModels.First().Id;
            Keys.SelectedModel = _selectedModel;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _connected)
            return;

        // Try to restore keys from encrypted cookies (JS interop only works after render).
        try
        {
            // Try Anthropic cookie
            var anthropicCookie = await JS.InvokeAsync<string?>("getCookie", CookieNameAnthropic);
            if (!string.IsNullOrEmpty(anthropicCookie))
            {
                try
                {
                    var key = Protector.Unprotect(anthropicCookie);
                    Keys.ActiveProvider = AiProvider.Anthropic;
                    Keys.Set(key);
                }
                catch { }
            }

            // Try GitHub Models cookie
            var gitHubCookie = await JS.InvokeAsync<string?>("getCookie", CookieNameGitHub);
            if (!string.IsNullOrEmpty(gitHubCookie))
            {
                try
                {
                    var key = Protector.Unprotect(gitHubCookie);
                    Keys.ActiveProvider = AiProvider.GitHubModels;
                    Keys.Set(key);
                }
                catch { }
            }

            if (Keys.GetApiKey() is not null)
            {
                _connected     = true;
                _selectedModel = Keys.SelectedModel;
                _selectedProvider = Keys.ActiveProvider;
                await InvokeAsync(StateHasChanged);
                ApiKeyState.NotifyChanged();
            }
        }
        catch
        {
            // Stale or tampered cookies — silently ignore.
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            Keys.ActiveProvider = _selectedProvider;
            Keys.Set(_keyInput);
            _keyInput  = "";
            _connected = true;
            _showForm  = false;
            _error     = false;
            _message   = null;

            if (_remember)
                await WriteCookieAsync(Keys.GetApiKey()!, _selectedProvider);

            ApiKeyState.NotifyChanged();
        }
        catch (ArgumentException ex)
        {
            _error   = true;
            _message = ex.Message;
        }
    }

    private async Task TestAsync()
    {
        _busy = true; _message = null;
        StateHasChanged();

        var result = await Tester.TestAsync(_keyInput, _selectedProvider);
        _busy  = false;
        _error = !result.Ok;
        _message = result.Ok ? "Key works." : $"Key rejected: {result.Error}";
    }

    private async Task DisconnectAsync()
    {
        Keys.Clear();
        _connected = false;
        _message   = null;
        var cookieName = _selectedProvider == AiProvider.GitHubModels ? CookieNameGitHub : CookieNameAnthropic;
        await JS.InvokeVoidAsync("deleteCookie", cookieName);
        ApiKeyState.NotifyChanged();
    }

    private void OnProviderChanged(ChangeEventArgs e)
    {
        if (Enum.TryParse<AiProvider>(e.Value?.ToString(), out var provider))
        {
            _selectedProvider = provider;
            Keys.ActiveProvider = provider;
            // Reset model to default for the new provider
            var models = ClaudeModels.GetSelectionModels(provider);
            _selectedModel = models.First().Id;
            Keys.SelectedModel = _selectedModel;
        }
    }

    private void OnModelChanged(ChangeEventArgs e)
    {
        var model = e.Value?.ToString() ?? ClaudeModels.Haiku;
        var validModels = ClaudeModels.GetSelectionModels(_selectedProvider);
        if (validModels.Any(m => m.Id == model))
        {
            Keys.SelectedModel = model;
            _selectedModel     = model;
        }
    }

    private async Task WriteCookieAsync(string apiKey, AiProvider provider)
    {
        var encrypted = Protector.Protect(apiKey);
        var cookieName = provider == AiProvider.GitHubModels ? CookieNameGitHub : CookieNameAnthropic;
        await JS.InvokeVoidAsync("setCookie", cookieName, encrypted, CookieDays);
    }
}
