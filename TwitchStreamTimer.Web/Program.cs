using TwitchStreamTimer.Web.Components;
using TwitchStreamTimer.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = true; // Enable detailed error messages
    });
builder.Services.AddControllers();

// Session support for user authentication
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

app.UseSession(); // Enable session middleware

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers(); // Enable API Controllers
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
