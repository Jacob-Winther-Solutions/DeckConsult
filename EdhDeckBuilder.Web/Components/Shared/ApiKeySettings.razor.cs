using EdhDeckBuilder.Agent.Authentication;
using EdhDeckBuilder.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;

namespace EdhDeckBuilder.Web.Components.Shared;

public partial class ApiKeySettings : ComponentBase
{
    private const string CookieName = "edh_apikey";
    private const int    CookieDays = 30;

    [Inject] private SessionApiKeyProvider   Keys       { get; set; } = default!;
    [Inject] private IClaudeKeyTester        Tester     { get; set; } = default!;
    [Inject] private IDataProtectionProvider DpProvider { get; set; } = default!;
    [Inject] private IJSRuntime              JS         { get; set; } = default!;
    [Inject] private IApiKeyStateService     ApiKeyState { get; set; } = default!;

    private string  _keyInput      = "";
    private bool    _connected;
    private bool    _showForm;
    private bool    _remember      = true;
    private bool    _busy;
    private bool    _error;
    private string? _message;
    private string  _selectedModel = ClaudeModels.Haiku;

    private IDataProtector Protector =>
        DpProvider.CreateProtector("EdhDeckBuilder.ApiKey");

    protected override void OnInitialized()
    {
        _connected     = Keys.GetApiKey() is not null;
        _selectedModel = Keys.SelectedModel;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _connected)
            return;

        // Try to restore key from the encrypted cookie (JS interop only works after render).
        try
        {
            var cookie = await JS.InvokeAsync<string?>("getCookie", CookieName);
            if (!string.IsNullOrEmpty(cookie))
            {
                var key = Protector.Unprotect(cookie);
                Keys.Set(key);
                _connected     = true;
                _selectedModel = Keys.SelectedModel;
                await InvokeAsync(StateHasChanged);
                ApiKeyState.NotifyChanged();
            }
        }
        catch
        {
            // Stale or tampered cookie — silently ignore and delete it.
            await JS.InvokeVoidAsync("deleteCookie", CookieName);
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            Keys.Set(_keyInput);
            _keyInput  = "";
            _connected = true;
            _showForm  = false;
            _error     = false;
            _message   = null;

            if (_remember)
                await WriteCookieAsync(Keys.GetApiKey()!);

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

        var result = await Tester.TestAsync(_keyInput);
        _busy  = false;
        _error = !result.Ok;
        _message = result.Ok ? "Key works." : $"Key rejected: {result.Error}";
    }

    private async Task DisconnectAsync()
    {
        Keys.Clear();
        _connected = false;
        _message   = null;
        await JS.InvokeVoidAsync("deleteCookie", CookieName);
        ApiKeyState.NotifyChanged();
    }

    private void OnModelChanged(ChangeEventArgs e)
    {
        var model = e.Value?.ToString() ?? ClaudeModels.Haiku;
        if (ClaudeModels.SelectionModels.Any(m => m.Id == model))
        {
            Keys.SelectedModel = model;
            _selectedModel     = model;
        }
    }

    private async Task WriteCookieAsync(string apiKey)
    {
        var encrypted = Protector.Protect(apiKey);
        await JS.InvokeVoidAsync("setCookie", CookieName, encrypted, CookieDays);
    }
}
