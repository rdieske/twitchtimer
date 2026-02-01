using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TwitchStreamTimer.Web.Services;

namespace TwitchStreamTimer.Web.Controllers;

[ApiController]
[Route("auth")]
public class TwitchAuthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TwitchIntegrationService _twitchService;
    private readonly ITimerManager _timerManager;
    private readonly IUserContext _userContext;
    private readonly ILogger<TwitchAuthController> _logger;
    private readonly IConfiguration _configuration;

    public TwitchAuthController(
        IHttpClientFactory httpClientFactory, 
        TwitchIntegrationService twitchService, 
        ITimerManager timerManager,
        IUserContext userContext,
        ILogger<TwitchAuthController> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _twitchService = twitchService;
        _timerManager = timerManager;
        _userContext = userContext;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("start")]
    public IActionResult Start()
    {
        var clientId = _configuration["TWITCH_CLIENT_ID"] ?? throw new InvalidOperationException("TWITCH_CLIENT_ID not configured");
        var redirectUri = _configuration["TWITCH_REDIRECT_URI"] ?? "http://localhost:7283/auth/callback";
        
        var scope = "channel:read:subscriptions bits:read"; 
        
        var url = $"https://id.twitch.tv/oauth2/authorize?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope={scope}";
        return Redirect(url);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string scope)
    {
        if (string.IsNullOrEmpty(code)) return BadRequest("No code returned");

        var clientId = _configuration["TWITCH_CLIENT_ID"] ?? throw new InvalidOperationException("TWITCH_CLIENT_ID not configured");
        var clientSecret = _configuration["TWITCH_CLIENT_SECRET"] ?? throw new InvalidOperationException("TWITCH_CLIENT_SECRET not configured");
        var redirectUri = _configuration["TWITCH_REDIRECT_URI"] ?? "http://localhost:7283/auth/callback";

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            {"client_id", clientId},
            {"client_secret", clientSecret},
            {"code", code},
            {"grant_type", "authorization_code"},
            {"redirect_uri", redirectUri}
        }));

        if (!response.IsSuccessStatusCode)
        {
             return Content($"Error exchanging token: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString();
        
        // Validate Token to get User ID
        var validateReq = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
        validateReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", accessToken);
        var validateResp = await client.SendAsync(validateReq);
        
        if (!validateResp.IsSuccessStatusCode) return Content("Failed to validate token.");
        
        var valJson = await validateResp.Content.ReadAsStringAsync();
        using var valDoc = JsonDocument.Parse(valJson);
        var userId = valDoc.RootElement.GetProperty("user_id").GetString();
        var login = valDoc.RootElement.GetProperty("login").GetString();

        // Set user session
        _userContext.SetUser(userId, login);
        _logger.LogInformation("User {Login} ({UserId}) logged in", login, userId);

        // Connect Service
        await _twitchService.ConnectAsync(userId, clientId, accessToken);

        return Content($"<h1>Success!</h1><p>Connected as {login}. Window closing...</p><script>if(window.opener){{window.opener.postMessage(\"twitch-auth-success\", \"*\");}} window.close();</script>", "text/html");
    }
    
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _userContext.ClearUser();
        return Ok();
    }
}
