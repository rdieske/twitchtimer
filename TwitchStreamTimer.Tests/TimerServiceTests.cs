using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TwitchStreamTimer.Web.Services;

namespace TwitchStreamTimer.Tests;

public class TimerServiceTests
{
    private readonly TimerService _service;

    public TimerServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string> {
            {"DataDirectory", "Test_Data_" + Guid.NewGuid()}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _service = new TimerService(NullLogger<TimerService>.Instance, configuration);
        
        // Setup default config
        var config = new TimerConfig
        {
            StartTime = DateTimeOffset.UtcNow,
            MinDurationSeconds = 600, // 10 minutes base
            SecondsPerSubTier1 = 60,
            SecondsPerSubTier2 = 120,
            SecondsPerBits = 60,
            MinBitsToTrigger = 100
        };
        _service.UpdateConfig(config);
        _service.StartAsync(CancellationToken.None);
    }

    [Fact]
    public void TestSubAddsTime()
    {
        _service.QueueSub("u1", "User1", "1000", false);
        
        // Wait for queue processing
        Thread.Sleep(100);
        
        Assert.Equal(60, _service.State.TotalAddedSeconds);
    }
    
    [Fact]
    public void TestSubTier2AddsMoreTime()
    {
        _service.QueueSub("u2", "User2", "2000", false);
        Thread.Sleep(50);
        Assert.Equal(120, _service.State.TotalAddedSeconds);
    }

    [Fact]
    public void TestBitsLogic_ExactMultiple()
    {
        // 200 bits / 100 min = 2. 2 * 60s = 120s
        _service.QueueBits("u3", "Cheerer", 200);
        Thread.Sleep(50);
        Assert.Equal(120, _service.State.TotalAddedSeconds);
    }

    [Fact]
    public void TestBitsLogic_FloorRounding()
    {
        // 150 bits / 100 min = 1. 1 * 60s = 60s (User rule: "abgerundete zahl")
        _service.QueueBits("u4", "CheererLow", 150);
        Thread.Sleep(50);
        Assert.Equal(60, _service.State.TotalAddedSeconds);
    }

    [Fact]
    public void TestBitsBelowMin_Exclusion()
    {
        // 99 bits / 100 min = 0.
        _service.QueueBits("u5", "CheapSkate", 99);
        Thread.Sleep(50);
        Assert.Equal(0, _service.State.TotalAddedSeconds);
    }

    [Fact]
    public void TestManualAdd()
    {
        _service.AddManualTime(500, "Admin abuse");
        Thread.Sleep(50);
        Assert.Equal(500, _service.State.TotalAddedSeconds);
    }
}
