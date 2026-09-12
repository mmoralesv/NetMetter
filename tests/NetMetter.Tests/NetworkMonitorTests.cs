using NetMetter;

namespace NetMetter.Tests;

public class NetworkMonitorTests
{
    [Fact]
    public void Rate_is_delta_over_elapsed()
    {
        Assert.Equal(1000, NetworkMonitor.Rate(previousBytes: 0, currentBytes: 1000, elapsedSeconds: 1.0));
        Assert.Equal(1000, NetworkMonitor.Rate(previousBytes: 1000, currentBytes: 3000, elapsedSeconds: 2.0));
    }

    [Fact]
    public void Rate_treats_a_counter_reset_as_zero()
    {
        // Adapter reset: the current reading is lower than the previous one.
        Assert.Equal(0, NetworkMonitor.Rate(previousBytes: 5000, currentBytes: 4000, elapsedSeconds: 1.0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Rate_is_zero_when_no_time_has_passed(double elapsed) =>
        Assert.Equal(0, NetworkMonitor.Rate(previousBytes: 0, currentBytes: 10_000, elapsedSeconds: elapsed));

    [Fact]
    public void Rate_handles_large_counters_without_overflow()
    {
        // 32-bit counters wrap; these are the 64-bit values the API returns.
        long previous = 10_000_000_000L;
        long current = 10_001_000_000L;
        Assert.Equal(1_000_000, NetworkMonitor.Rate(previous, current, elapsedSeconds: 1.0));
    }
}
