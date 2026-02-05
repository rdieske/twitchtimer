namespace TwitchStreamTimer.Web.Services;

public class SessionInfo
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public string ConnectionType { get; set; } = ""; // "EventSub", "Chat", "Simulation"
    public string MonitoredChannel { get; set; } = "";
    public bool IsConnected { get; set; }
}

public interface ISessionTracker
{
    void RegisterSession(string userId, string username, string connectionType, string monitoredChannel);
    void UpdateActivity(string userId);
    void RemoveSession(string userId);
    List<SessionInfo> GetActiveSessions();
    SessionInfo? GetSession(string userId);
}

public class SessionTracker : ISessionTracker
{
    private readonly Dictionary<string, SessionInfo> _sessions = new();
    private readonly object _lock = new();

    public void RegisterSession(string userId, string username, string connectionType, string monitoredChannel)
    {
        lock (_lock)
        {
            _sessions[userId] = new SessionInfo
            {
                UserId = userId,
                Username = username,
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                ConnectionType = connectionType,
                MonitoredChannel = monitoredChannel,
                IsConnected = true
            };
        }
    }

    public void UpdateActivity(string userId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(userId, out var session))
            {
                session.LastActivity = DateTime.UtcNow;
            }
        }
    }

    public void RemoveSession(string userId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(userId, out var session))
            {
                session.IsConnected = false;
            }
        }
    }

    public List<SessionInfo> GetActiveSessions()
    {
        lock (_lock)
        {
            return _sessions.Values.ToList();
        }
    }

    public SessionInfo? GetSession(string userId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(userId, out var session) ? session : null;
        }
    }
}
