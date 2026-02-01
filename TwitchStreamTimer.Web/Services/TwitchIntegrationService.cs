using TwitchLib.EventSub.Websockets;
using Microsoft.Extensions.Options;

namespace TwitchStreamTimer.Web.Services;

public class TwitchIntegrationService : IHostedService
{
    private readonly ILogger<TwitchIntegrationService> _logger;
    
    public bool IsConnected { get; private set; }
    public string? CurrentUserId { get; private set; }

    public TwitchIntegrationService(ILogger<TwitchIntegrationService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task ConnectAsync(string userId, string clientId, string accessToken)
    {
        IsConnected = true;
        CurrentUserId = userId;
        _logger.LogInformation("Simulated connection for User {User}", userId);
        return Task.CompletedTask;
    }
}
