using EdhDeckBuilder.Agent;
using EdhDeckBuilder.Infrastructure;
using EdhDeckBuilder.Web.Components;
using EdhDeckBuilder.Web.Components.Pages;
using EdhDeckBuilder.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Raise SignalR receive limit so localStorage fallback (page-reload path) can transfer deck JSON.
// Same-session navigations use DeckResultStore and skip JS interop entirely.
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 5 * 1024 * 1024);

builder.Services.AddDataProtection();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAgent(builder.Configuration);
builder.Services.AddScoped<IApiKeyStateService, ApiKeyStateService>();
builder.Services.AddSingleton<DeckResultStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
