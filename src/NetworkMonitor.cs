using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetMetter;

/// <summary>A snapshot of one network interface and its current throughput.</summary>
internal sealed record InterfaceStat(
    string Id,
    string Name,
    string Description,
    NetworkInterfaceType Type,
    long LinkSpeed,
    IReadOnlyList<string> Addresses,
    bool HasGateway,
    double DownBytesPerSec,
    double UpBytesPerSec,
    long TotalReceived,
    long TotalSent);

/// <summary>
/// Samples the byte counters of every connected interface and turns the deltas into speeds.
/// The interface list is cached and only rebuilt when Windows reports a network change
/// (or every few seconds as a safety net), since enumerating adapters is comparatively expensive.
/// </summary>
internal sealed class NetworkMonitor : IDisposable
{
    private static readonly TimeSpan ListRefreshInterval = TimeSpan.FromSeconds(5);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Dictionary<string, (long Rx, long Tx)> _lastCounters = new();
    private TimeSpan _lastSampleTime;
    private TimeSpan _lastListRefresh = TimeSpan.MinValue;
    private NetworkInterface[] _interfaces = [];
    private volatile bool _listDirty = true;

    public NetworkMonitor()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => _listDirty = true;

    public IReadOnlyList<InterfaceStat> Sample()
    {
        var now = _clock.Elapsed;
        if (_listDirty || now - _lastListRefresh > ListRefreshInterval)
        {
            _listDirty = false;
            _lastListRefresh = now;
            _interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsCandidate)
                .ToArray();
        }

        double elapsed = (now - _lastSampleTime).TotalSeconds;
        _lastSampleTime = now;

        var counters = new Dictionary<string, (long Rx, long Tx)>(_interfaces.Length);
        var result = new List<InterfaceStat>(_interfaces.Length);

        foreach (var ni in _interfaces)
        {
            long rx, tx;
            try
            {
                var stats = ni.GetIPStatistics();
                rx = stats.BytesReceived;
                tx = stats.BytesSent;
            }
            catch (NetworkInformationException)
            {
                // The adapter disappeared between enumeration and sampling.
                _listDirty = true;
                continue;
            }

            double down = 0, up = 0;
            if (elapsed > 0 && _lastCounters.TryGetValue(ni.Id, out var prev))
            {
                // Counters can go backwards when an adapter is reset; treat that as zero traffic.
                down = Math.Max(0, rx - prev.Rx) / elapsed;
                up = Math.Max(0, tx - prev.Tx) / elapsed;
            }
            counters[ni.Id] = (rx, tx);

            var props = ni.GetIPProperties();
            result.Add(new InterfaceStat(
                ni.Id,
                ni.Name,
                ni.Description,
                ni.NetworkInterfaceType,
                ni.Speed,
                props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                || (a.Address.AddressFamily == AddressFamily.InterNetworkV6 && !a.Address.IsIPv6LinkLocal))
                    .Select(a => a.Address.ToString())
                    .ToArray(),
                props.GatewayAddresses.Any(g => !g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.IPv6Any)),
                down,
                up,
                rx,
                tx));
        }

        _lastCounters = counters;
        return result;
    }

    private static bool IsCandidate(NetworkInterface ni) =>
        ni.OperationalStatus == OperationalStatus.Up
        && ni.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel);

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
    }
}
