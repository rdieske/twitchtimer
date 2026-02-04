using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace TwitchStreamTimer.Web.Services;

public interface ITimerService
{
    /// <summary>
    /// Gets the current remaining time on the timer.
    /// </summary>
    TimeSpan RemainingTime { get; }
    
    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    TimerConfig Config { get; }
    
    /// <summary>
    /// Gets the current state (logs, totals, etc.).
    /// </summary>
    TimerState State { get; }
    
    /// <summary>
    /// Indicates if the timer is currently running (not paused and not stopped).
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts or resumes the timer.
    /// </summary>
    void Start();
    
    /// <summary>
    /// Pauses the timer.
    /// </summary>
    void Pause();
    
    /// <summary>
    /// Stops the timer completely.
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Resets the timer to initial state.
    /// </summary>
    void Reset();
    
    /// <summary>
    /// Updates the timer configuration.
    /// </summary>
    void UpdateConfig(TimerConfig newConfig);
    
    /// <summary>
    /// Queues a subscription event to add time.
    /// </summary>
    Task QueueSub(string userId, string userDisplay, string tier, bool isGift, int count = 1);
    
    /// <summary>
    /// Queues a bits event to add time.
    /// </summary>
    Task QueueBits(string userId, string userDisplay, int bits, int count = 1);
    
    /// <summary>
    /// Queues a manual time adjustment.
    /// </summary>
    Task AddManualTime(long seconds, string reason);
    
    /// <summary>
    /// Deletes a previously processed event and reverts its time addition.
    /// </summary>
    void DeleteEvent(string eventId);
}

public class TimerService : BackgroundService, ITimerService, IDisposable
{
    private readonly ILogger<TimerService> _logger;
    private readonly string _userId;
    private readonly string _stateFilePath;
    private readonly string _configFilePath;
    
    public TimerConfig Config { get; private set; } = new();
    public TimerState State { get; private set; } = new();
    private readonly HashSet<string> _processedMessageIds = new(); 

    private readonly Channel<TimerEvent> _eventQueue;
    private readonly System.Timers.Timer _tickTimer;

    public TimeSpan RemainingTime
    {
        get
        {
            if (State.IsStopped) return TimeSpan.Zero;
            if (State.IsPaused) return _cachedRemaining;

            var now = DateTimeOffset.UtcNow;
            
            if (now < Config.StartTime)
            {
               return Config.StartTime - now; 
            }

            var elapsedSinceStart = now - Config.StartTime;
            var effectiveElapsed = elapsedSinceStart - TimeSpan.FromSeconds(State.TotalPausedSeconds);
            var totalDuration = TimeSpan.FromSeconds(Config.MinDurationSeconds + State.TotalAddedSeconds);
            
            if (Config.MaxDurationSeconds > 0 && totalDuration.TotalSeconds > Config.MaxDurationSeconds)
            {
                totalDuration = TimeSpan.FromSeconds(Config.MaxDurationSeconds);
            }

            var left = totalDuration - effectiveElapsed;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            
            _cachedRemaining = left;
            return left;
        }
    }
    private TimeSpan _cachedRemaining = TimeSpan.Zero;
    public bool IsRunning => !State.IsStopped && !State.IsPaused && (RemainingTime > TimeSpan.Zero || DateTimeOffset.UtcNow < Config.StartTime);

    public TimerService(string userId, ILogger<TimerService> logger)
    {
        _userId = userId;
        _logger = logger;
        
        var dataDir = Path.Combine("Data", userId);
        Directory.CreateDirectory(dataDir);
        
        _stateFilePath = Path.Combine(dataDir, "timer_state.json");
        _configFilePath = Path.Combine(dataDir, "timer_config.json");

        _eventQueue = Channel.CreateUnbounded<TimerEvent>();
        
        LoadDiskState();

        _tickTimer = new System.Timers.Timer(1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
         await RecoverLostEvents();
         
         _tickTimer.Start();
         
         var queueTask = ProcessQueue(stoppingToken);
         var recoveryTask = RunRecoveryLoop(stoppingToken);
         
         await Task.WhenAll(queueTask, recoveryTask);
         
         _tickTimer.Stop();
    }

    private async Task ProcessQueue(CancellationToken stoppingToken)
    {
         await foreach (var evt in _eventQueue.Reader.ReadAllAsync(stoppingToken))
         {
            try
            {
                ProcessEvent(evt);
                SaveDiskState();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing timer event");
            }
         }
    }

    private async Task RunRecoveryLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            await RecoverLostEvents();
        }
    }
    
    private async Task RecoverLostEvents()
    {
        var backupFile = Path.Combine("Data", _userId, "emergency_events.jsonl");
        if (!File.Exists(backupFile)) return;

        _logger.LogWarning("Found emergency backup file. Attempting recovery...");
        
        var lines = await File.ReadAllLinesAsync(backupFile);
        if (lines.Length == 0) return;
        
        var recoveredCount = 0;
        var failedCount = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try 
            {
                var evt = JsonSerializer.Deserialize<TimerEvent>(line);
                if (evt != null)
                {
                    await SafeQueueEvent(evt);
                    recoveredCount++;
                }
            }
            catch 
            {
                failedCount++;
            }
        }
        
        _logger.LogInformation("Recovery: Queued {Count} events. Failed to parse {Failed}. Deleting backup file.", recoveredCount, failedCount);
        
        try 
        {
            File.Move(backupFile, backupFile + $".recovered.{DateTime.UtcNow.Ticks}.bak");
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Could not rename backup file after recovery");
        }
    }

    private void ProcessEvent(TimerEvent evt)
    {
        if (!string.IsNullOrEmpty(evt.MessageId) && _processedMessageIds.Contains(evt.MessageId))
        {
            _logger.LogInformation("Duplicate event skipped: {Id}", evt.MessageId);
            return;
        }

        long secondsToAdd = 0;
        string logDesc = "";

        switch (evt.Type)
        {
            case EventType.Sub:
                logDesc = evt.Count > 1 ? $"{evt.Count}x Sub ({evt.Tier}) by {evt.UserDisplay}" : $"Sub ({evt.Tier}) by {evt.UserDisplay}";
                if (evt.Tier == "1000") secondsToAdd = Config.SecondsPerSubTier1 * evt.Count;
                else if (evt.Tier == "2000") secondsToAdd = Config.SecondsPerSubTier2 * evt.Count;
                else if (evt.Tier == "3000") secondsToAdd = Config.SecondsPerSubTier3 * evt.Count;
                else if (evt.Tier == "Prime") secondsToAdd = Config.SecondsPerPrimeSub * evt.Count;
                else secondsToAdd = Config.SecondsPerSubTier1 * evt.Count;
                break;

            case EventType.GiftSub:
                logDesc = evt.Count > 1 ? $"{evt.Count}x Gift Sub ({evt.Tier}) to {evt.UserDisplay}" : $"Gift Sub ({evt.Tier}) to {evt.UserDisplay}";
                if (evt.Tier == "1000") secondsToAdd = Config.SecondsPerSubTier1 * evt.Count;
                else if (evt.Tier == "2000") secondsToAdd = Config.SecondsPerSubTier2 * evt.Count;
                else if (evt.Tier == "3000") secondsToAdd = Config.SecondsPerSubTier3 * evt.Count;
                break;

            case EventType.Bits:
                int totalBits = evt.Bits * evt.Count;
                logDesc = evt.Count > 1 ? $"{evt.Count}x Cheer {evt.Bits} bits ({totalBits} total) by {evt.UserDisplay}" : $"Cheer {evt.Bits} bits by {evt.UserDisplay}";
                if (totalBits < Config.MinBitsToTrigger) 
                {
                    secondsToAdd = 0;
                }
                else
                {
                    if (Config.SecondsPerBit > 0)
                    {
                        secondsToAdd = totalBits * Config.SecondsPerBit;
                    }
                    else if (Config.SecondsPerBits > 0 && Config.MinBitsToTrigger > 0)
                    {
                        var factor = totalBits / Config.MinBitsToTrigger; 
                        secondsToAdd = factor * Config.SecondsPerBits;
                    }
                }
                break;

            case EventType.Manual:
                logDesc = evt.Reason;
                secondsToAdd = evt.ManualSeconds;
                break;
        }

        if (secondsToAdd > 0)
        {
            State.TotalAddedSeconds += secondsToAdd;
            State.EventLog.Add(new ProcessedEvent
            {
                Id = evt.MessageId ?? Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                Description = logDesc,
                SecondsAdded = secondsToAdd,
                UserDisplay = evt.UserDisplay
            });

            if (State.EventLog.Count > 5000) State.EventLog.RemoveRange(0, 1000);

            if (!string.IsNullOrEmpty(evt.MessageId)) _processedMessageIds.Add(evt.MessageId);
            
            _logger.LogInformation("Added {Sec}s. Reason: {Reason}. Total Added: {Total}", secondsToAdd, logDesc, State.TotalAddedSeconds);
        }
    }

    public async Task QueueSub(string userId, string userDisplay, string tier, bool isGift, int count = 1)
    {
        var evt = new TimerEvent 
        { 
            Type = isGift ? EventType.GiftSub : EventType.Sub, 
            Tier = tier, 
            UserDisplay = userDisplay,
            Count = count,
            MessageId = $"sub-{userId}-{DateTime.UtcNow.Ticks}"
        };
        _logger.LogInformation("Queuing Sub event: {Count}x {Tier} by {User}", count, tier, userDisplay);
        await SafeQueueEvent(evt);
    }

    public async Task QueueBits(string userId, string userDisplay, int bits, int count = 1)
    {
        var evt = new TimerEvent 
        { 
            Type = EventType.Bits, 
            Bits = bits,
            Count = count, 
            UserDisplay = userDisplay,
            MessageId = $"bits-{userId}-{DateTime.UtcNow.Ticks}"
        };
        _logger.LogInformation("Queuing Bits event: {Count}x {Bits} bits by {User}", count, bits, userDisplay);
        await SafeQueueEvent(evt);
    }

    public async Task AddManualTime(long seconds, string reason)
    {
        var evt = new TimerEvent 
        { 
            Type = EventType.Manual, 
            ManualSeconds = seconds, 
            Reason = reason 
        };
        _logger.LogInformation("Queuing Manual event: {Seconds}s - {Reason}", seconds, reason);
        await SafeQueueEvent(evt);
    }

    private async Task SafeQueueEvent(TimerEvent evt)
    {
        try
        {
            await _eventQueue.Writer.WriteAsync(evt);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "CRITICAL: Failed to write event to queue! Attempting emergency backup.");
            await EmergencyBackup(evt);
        }
    }

    private async Task EmergencyBackup(TimerEvent evt)
    {
        try 
        {
             var backupFile = Path.Combine("Data", _userId, "emergency_events.jsonl");
             var json = JsonSerializer.Serialize(evt);
             await File.AppendAllTextAsync(backupFile, json + Environment.NewLine);
             _logger.LogInformation("Emergency backup successful for event {Id}", evt.MessageId);
        }
        catch (Exception driveEx)
        {
             _logger.LogCritical(driveEx, "CATASTROPHIC FAILURE: Could not write to emergency backup file. Event LOST: {Json}", JsonSerializer.Serialize(evt));
        }
    }
    
    public void DeleteEvent(string eventId)
    {
        var evt = State.EventLog.FirstOrDefault(e => e.Id == eventId);
        if (evt != null)
        {
            State.TotalAddedSeconds -= evt.SecondsAdded;
            State.EventLog.Remove(evt);
            _logger.LogInformation("Deleted event {Id}, removed {Seconds}s", eventId, evt.SecondsAdded);
            SaveDiskState();
        }
    }

    public void Start() 
    { 
        if (State.IsStopped)
        {
             State.IsStopped = false;
        }
        
        if (State.IsPaused && State.PausedAt.HasValue)
        {
            var pausedDuration = DateTimeOffset.UtcNow - State.PausedAt.Value;
            State.TotalPausedSeconds += (long)pausedDuration.TotalSeconds;
            State.PausedAt = null;
            _logger.LogInformation("Resuming from pause. Total paused: {Seconds}s", State.TotalPausedSeconds);
        }
        
        State.IsPaused = false; 
        SaveDiskState(); 
    }

    public void Pause() { 
        State.IsPaused = true;
        State.PausedAt = DateTimeOffset.UtcNow;
        _cachedRemaining = RemainingTime;
        SaveDiskState(); 
    }

    public void Stop()
    {
        State.IsStopped = true;
        State.IsPaused = false;
        SaveDiskState();
    }

    public void Reset() { 
        Config.StartTime = Config.InitialStartTime;
        State.TotalAddedSeconds = 0; 
        State.EventLog.Clear(); 
        State.IsStopped = false;
        State.IsPaused = true;
        State.PausedAt = null;
        State.TotalPausedSeconds = 0;
        SaveDiskState(); 
    }

    public void UpdateConfig(TimerConfig newConfig)
    {
        Config = newConfig;
        SaveConfig();
    }

    private void LoadDiskState()
    {
        if (File.Exists(_configFilePath))
        {
            var json = File.ReadAllText(_configFilePath);
            Config = JsonSerializer.Deserialize<TimerConfig>(json) ?? new();
        }
        if (File.Exists(_stateFilePath))
        {
            var json = File.ReadAllText(_stateFilePath);
            State = JsonSerializer.Deserialize<TimerState>(json) ?? new();
        }
    }

    private void SaveDiskState()
    {
        var dir = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var json = JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_stateFilePath, json);
    }
    private void SaveConfig()
    {
        var dir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configFilePath, json);
    }

    public override void Dispose()
    {
        _tickTimer.Dispose();
        base.Dispose();
    }
}

public class TimerEvent
{
    public EventType Type { get; set; }
    public string MessageId { get; set; }
    public string UserDisplay { get; set; }
    public int Count { get; set; } = 1;
    
    public string Tier { get; set; } 
    public int Bits { get; set; }
    public long ManualSeconds { get; set; }
    public string Reason { get; set; }
}

public enum EventType { Sub, GiftSub, Bits, Manual }
