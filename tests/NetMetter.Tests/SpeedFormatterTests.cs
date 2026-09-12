using NetMetter;

namespace NetMetter.Tests;

public class SpeedFormatterTests
{
    [Theory]
    [InlineData(0, false, "0.0 KB/s")]
    [InlineData(512, false, "0.5 KB/s")]
    [InlineData(1024, false, "1.0 KB/s")]
    [InlineData(1536, false, "1.5 KB/s")]
    [InlineData(102_400, false, "100 KB/s")]       // >= 100 drops the decimal
    [InlineData(1_048_576, false, "1.0 MB/s")]     // rolls over to MB at 1024 KB/s
    [InlineData(1_073_741_824, false, "1.0 GB/s")]
    public void Format_bytes_scales_and_rounds(double bytesPerSec, bool bits, string expected) =>
        Assert.Equal(expected, SpeedFormatter.Format(bytesPerSec, bits));

    [Theory]
    [InlineData(125, true, "1.0 Kbps")]            // 125 B/s * 8 = 1000 bits = 1 Kbps
    [InlineData(125_000, true, "1.0 Mbps")]
    [InlineData(125_000_000, true, "1.0 Gbps")]
    public void Format_bits_uses_decimal_units(double bytesPerSec, bool bits, string expected) =>
        Assert.Equal(expected, SpeedFormatter.Format(bytesPerSec, bits));

    [Fact]
    public void Format_rolls_over_just_below_a_thousand_of_the_unit()
    {
        // 999.95 KB/s should present as the next unit rather than "1000 KB/s".
        Assert.Equal("1.0 MB/s", SpeedFormatter.Format(999.95 * 1024, bits: false));
    }

    [Theory]
    [InlineData(false, "999.9 MB/s")]
    [InlineData(true, "999.9 Mbps")]
    public void WidestSample_matches_the_widest_value_Format_can_emit(bool bits, string expected) =>
        Assert.Equal(expected, SpeedFormatter.WidestSample(bits));

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1_572_864, "1.5 MB")]
    [InlineData(5_368_709_120, "5 GB")]
    public void FormatBytes_scales_totals(long bytes, string expected) =>
        Assert.Equal(expected, SpeedFormatter.FormatBytes(bytes));

    [Theory]
    [InlineData(0, "unknown")]
    [InlineData(-1, "unknown")]
    [InlineData(72_200_000, "72.2 Mbps")]
    [InlineData(1_000_000_000, "1 Gbps")]
    [InlineData(2_500_000_000, "2.5 Gbps")]
    [InlineData(56_000, "56 Kbps")]
    public void FormatLinkSpeed_covers_ranges_and_unknown(long bitsPerSec, string expected) =>
        Assert.Equal(expected, SpeedFormatter.FormatLinkSpeed(bitsPerSec));
}
