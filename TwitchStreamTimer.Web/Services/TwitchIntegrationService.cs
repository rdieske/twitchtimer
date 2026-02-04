using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.Client;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Microsoft.Extensions.Options;

namespace TwitchStreamTimer.Web.Services;

public class TwitchIntegrationService : IHostedService, IDisposable
{
    private readonly ILogger<TwitchIntegrationService> _logger;
    private readonly ITimerManager _timerManager;
    private readonly IConfiguration _configuration;
    
    private EventSubWebsocketClient? _eventSubClient;
    public bool IsEventSubConnected { get; private set; }
    
    private ITwitchClient? _chatClient;
    public bool IsChatConnected { get; private set; }

    public string? CurrentMonitoredChannel { get; private set; }
    private string? _currentUserId;

    public TwitchIntegrationService(
        ILogger<TwitchIntegrationService> logger, 
        ITimerManager timerManager,
        IConfiguration configuration)
    {
        _logger = logger;
        _timerManager = timerManager;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = DisconnectAsync();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disconnects both EventSub and Chat clients, ensuring resources are released.
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (_eventSubClient != null)
            {
                await _eventSubClient.DisconnectAsync();
                _eventSubClient = null;
            }
            if (_chatClient != null)
            {
                await ((TwitchClient)_chatClient).DisconnectAsync();
                _chatClient = null;
            }
            IsEventSubConnected = false;
            IsChatConnected = false;
            CurrentMonitoredChannel = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting Twitch services");
        }
    }

    /// <summary>
    /// Connects to the user's channel using EventSub for official monitoring.
    /// Note: Actual EventSub subscriptions require a separate API call to register them using the Session ID.
    /// </summary>
    public async Task ConnectEventSubAsync(string userId, string accessToken)
    {
        await DisconnectAsync();
        _currentUserId = userId;
        CurrentMonitoredChannel = "My Channel (EventSub)";

        try 
        {
            _eventSubClient = new EventSubWebsocketClient(_loggerFactory);
            _eventSubClient.WebsocketConnected += OnEventSubConnected;
            _eventSubClient.WebsocketDisconnected += OnEventSubDisconnected;

            await _eventSubClient.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws"));
            
            IsEventSubConnected = true; 
            _logger.LogInformation("EventSub Connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect EventSub");
            throw;
        }
    }

    /// <summary>
    /// Connects to a target channel using the IRC Chat Client. This works for both the user's own channel and other channels.
    /// </summary>
    public async Task ConnectChat(string botUsername, string accessToken, string targetChannel)
    {
        if (_chatClient != null) await ((TwitchClient)_chatClient).DisconnectAsync();

        _currentUserId = "chat-user"; 
        CurrentMonitoredChannel = targetChannel;

        var credentials = new ConnectionCredentials(botUsername, accessToken);
        var clientOptions = new ClientOptions(); 
        var customClient = new WebSocketClient(clientOptions);
        _chatClient = new TwitchClient(customClient);
        
        _chatClient.Initialize(credentials, targetChannel);

        _chatClient.OnConnected += (s, e) => 
        {
            IsChatConnected = true;
            _logger.LogInformation("Connected to Chat: {Channel}", targetChannel);
            return Task.CompletedTask;
        };
        
        _chatClient.OnNewSubscriber += async (s, e) => { await ProcessSub(e.Subscriber.DisplayName, "1000", false, 1); };
        _chatClient.OnReSubscriber += async (s, e) => { await ProcessSub(e.ReSubscriber.DisplayName, "1000", false, 1); };
        _chatClient.OnGiftedSubscription += async (s, e) => { await ProcessSub(e.GiftedSubscription.DisplayName, "1000", true, 1); };
        _chatClient.OnCommunitySubscription += async (s, e) => { await ProcessSub(e.GiftedSubscription.DisplayName, "1000", true, e.GiftedSubscription.MsgParamMassGiftCount); };
        
        _chatClient.OnMessageReceived += async (s, e) => 
        {
             if (e.ChatMessage.Bits > 0)
             {
                 await ProcessBits(e.ChatMessage.DisplayName, e.ChatMessage.Bits);
             }
        };

        await _chatClient.ConnectAsync();
    }

    /// <summary>
    /// The UserID of the currently active timer session to route events to.
    /// </summary>
    public string? ActiveTimerUserId { get; set; }

    private async Task ProcessSub(string userDisplay, string tier, bool isGift, int count)
    {
        if (ActiveTimerUserId == null) return;
        var timer = _timerManager.GetUserTimer(ActiveTimerUserId);
        
        var effectiveTier = tier switch 
        {
            "Prime" => "Prime",
            "1000" => "1000",
            "2000" => "2000",
            "3000" => "3000",
            _ => "1000" 
        };

        await timer.QueueSub(ActiveTimerUserId, userDisplay, effectiveTier, isGift, count);
    }

    private async Task ProcessBits(string userDisplay, int bits)
    {
        if (ActiveTimerUserId == null) return;
        var timer = _timerManager.GetUserTimer(ActiveTimerUserId);
        await timer.QueueBits(ActiveTimerUserId, userDisplay, bits);
    }

    private Task OnEventSubConnected(object? sender, WebsocketConnectedArgs e) 
    {
        IsEventSubConnected = true;
        return Task.CompletedTask;
    } 
    private Task OnEventSubDisconnected(object? sender, EventArgs e) 
    {
        IsEventSubConnected = false;
        return Task.CompletedTask;
    }

    private ILoggerFactory _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

    public void Dispose()
    {
        DisconnectAsync().Wait();
    }
}
