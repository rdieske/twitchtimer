using System.Text.Json.Serialization;

namespace TwitchStreamTimer.Web.Services;

public class TimerConfig
{
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset InitialStartTime { get; set; } = DateTimeOffset.UtcNow;
    public long MinDurationSeconds { get; set; } = 86400; // Default 24 hours
    public long MaxDurationSeconds { get; set; } = 7776000; // Default 60 days(optional cap)
    
    public string TwitchClientId { get; set; } = "";
    public string TwitchClientSecret { get; set; } = "";
    public string TwitchRedirectUri { get; set; } = "http://localhost:8080/auth/twitch/callback";

    public long SecondsPerSubTier1 { get; set; } = 60; // 60 seconds
    public long SecondsPerSubTier2 { get; set; } = 120; // 120 seconds
    public long SecondsPerSubTier3 { get; set; } = 180; // 180 seconds
    public long SecondsPerPrimeSub { get; set; } = 60; // 60 seconds

    public long SecondsPerBit { get; set; } = 0; // If 0, use SecondsPerBits logic
    public long SecondsPerBits { get; set; } = 60; // seconds per bits
    public int MinBitsToTrigger { get; set; } = 1000;

    public string BackgroundColor { get; set; } = "#1e1e1e"; 
    public string TextColor { get; set; } = "#00e676";

}

public class TimerState
{
    public long TotalAddedSeconds { get; set; } = 0;
    public bool IsPaused { get; set; } = false;
    public bool IsStopped { get; set; } = false;
    
    public DateTimeOffset? PausedAt { get; set; } = null;
    public long TotalPausedSeconds { get; set; } = 0;
    
    public List<ProcessedEvent> EventLog { get; set; } = new();
}

public class ProcessedEvent
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public long SecondsAdded { get; set; }
    public string UserDisplay { get; set; } = string.Empty;
}
