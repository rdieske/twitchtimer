using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace TwitchStreamTimer.Web.Services;

public interface ITimerManager
{
    ITimerService GetUserTimer(string userId);
    void RemoveUserTimer(string userId);
}

public class TimerManager : ITimerManager
{
    private readonly ConcurrentDictionary<string, TimerService> _userTimers = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;

    public TimerManager(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
    {
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
    }

    public ITimerService GetUserTimer(string userId)
    {
        return _userTimers.GetOrAdd(userId, uid =>
        {
            var logger = _loggerFactory.CreateLogger<TimerService>();
            var timerService = new TimerService(uid, logger);
            
            _ = timerService.StartAsync(CancellationToken.None);
            
            return timerService;
        });
    }

    public void RemoveUserTimer(string userId)
    {
        if (_userTimers.TryRemove(userId, out var timer))
        {
            timer.Dispose();
        }
    }
}
