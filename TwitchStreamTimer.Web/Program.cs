using TwitchStreamTimer.Web.Components;
using TwitchStreamTimer.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true; 
    });
builder.Services.AddControllers();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddMudServices();
builder.Services.AddSingleton<ITimerManager, TimerManager>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<TwitchIntegrationService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TwitchIntegrationService>());
builder.Services.AddScoped<IUserContext, UserContext>();
builder.Services.AddSingleton<ISessionTracker, SessionTracker>();

var app = builder.Build(); 


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
     app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseSession();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers(); 
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
