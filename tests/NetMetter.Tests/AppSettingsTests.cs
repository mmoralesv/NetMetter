using System.Net.NetworkInformation;
using System.Text.Json;
using NetMetter;

namespace NetMetter.Tests;

public class AppSettingsTests
{
    private static InterfaceStat Stat(string id, bool hasGateway) =>
        new(id, "Name", "Description", NetworkInterfaceType.Ethernet,
            LinkSpeed: 1_000_000_000, Addresses: [], HasGateway: hasGateway,
            DownBytesPerSec: 0, UpBytesPerSec: 0, TotalReceived: 0, TotalSent: 0);

    [Fact]
    public void IsVisible_defaults_to_having_a_gateway()
    {
        var settings = new AppSettings();
        Assert.True(settings.IsVisible(Stat("eth0", hasGateway: true)));
        Assert.False(settings.IsVisible(Stat("vbox0", hasGateway: false)));
    }

    [Fact]
    public void IsVisible_honours_an_explicit_override_over_the_gateway_rule()
    {
        var settings = new AppSettings();
        settings.InterfaceVisibility["vbox0"] = true;   // show a gateway-less adapter
        settings.InterfaceVisibility["eth0"] = false;   // hide one that has a gateway

        Assert.True(settings.IsVisible(Stat("vbox0", hasGateway: false)));
        Assert.False(settings.IsVisible(Stat("eth0", hasGateway: true)));
    }

    [Fact]
    public void Enums_serialize_as_names_and_round_trip()
    {
        var original = new AppSettings
        {
            Mode = DisplayMode.Floating,
            Anchor = AnchorSide.Far,
            UseBits = true,
            IntervalMs = 2000,
            FloatingLeft = 120,
            FloatingTop = 240,
        };
        original.InterfaceVisibility["eth0"] = false;

        string json = JsonSerializer.Serialize(original, AppSettings.JsonOptions);

        // Enums are written as strings, not integers, so the file stays readable and stable.
        Assert.Contains("\"Floating\"", json);
        Assert.Contains("\"Far\"", json);
        Assert.DoesNotContain("\"Mode\": 1", json);

        var restored = JsonSerializer.Deserialize<AppSettings>(json, AppSettings.JsonOptions)!;
        Assert.Equal(DisplayMode.Floating, restored.Mode);
        Assert.Equal(AnchorSide.Far, restored.Anchor);
        Assert.True(restored.UseBits);
        Assert.Equal(2000, restored.IntervalMs);
        Assert.Equal(120, restored.FloatingLeft);
        Assert.Equal(240, restored.FloatingTop);
        Assert.False(restored.InterfaceVisibility["eth0"]);
    }
}
