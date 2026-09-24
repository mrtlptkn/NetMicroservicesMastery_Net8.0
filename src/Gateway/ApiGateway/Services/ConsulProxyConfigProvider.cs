using Consul;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using RouteConfig = Yarp.ReverseProxy.Configuration.RouteConfig;

namespace ApiGateway.Services
{

  public class ConsulProxyConfig : IProxyConfig
  {
    private readonly CancellationTokenSource _cts = new();

    public IReadOnlyList<Yarp.ReverseProxy.Configuration.RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public IChangeToken ChangeToken { get; }

    public ConsulProxyConfig(IReadOnlyList<Yarp.ReverseProxy.Configuration.RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
      Routes = routes;
      Clusters = clusters;
      ChangeToken = new CancellationChangeToken(_cts.Token);
    }

    internal void SignalChange() => _cts.Cancel();
  }

  public class ConsulProxyConfigProvider : IProxyConfigProvider
  {
    private readonly IConsulClient _consulClient;
    private readonly ILogger<ConsulProxyConfigProvider> _logger;
    private volatile ConsulProxyConfig _currentConfig;
    private string _lastSignature = string.Empty;

    public ConsulProxyConfigProvider(IConsulClient consulClient, ILogger<ConsulProxyConfigProvider> logger)
    {
      _consulClient = consulClient;
      _logger = logger;
      // Başlangıçta boş config; ilk Reload hemen dolduracak
      _currentConfig = new ConsulProxyConfig(Array.Empty<RouteConfig>(), Array.Empty<ClusterConfig>());
    }

    public IProxyConfig GetConfig() => _currentConfig;

    public async Task ReloadAsync(CancellationToken ct)
    {
      try
      {
        var services = await _consulClient.Agent.Services(ct);
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        var groups = services.Response.Values
            .GroupBy(s => s.Service, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
          var serviceName = group.Key;

          routes.Add(new RouteConfig
          {
            RouteId = $"{serviceName}-route",
            ClusterId = serviceName,
            Match = new RouteMatch { Path = $"/{serviceName}/{{**catch-all}}" },
            Transforms = new List<IReadOnlyDictionary<string, string>>
                        {
                            new Dictionary<string, string> { { "PathRemovePrefix", $"/{serviceName}" } }
                        }
          });

          var destinations = group.ToDictionary(
              i => i.ID,
              i => new Yarp.ReverseProxy.Configuration.DestinationConfig { Address = $"http://{i.Address}:{i.Port}" },
              StringComparer.OrdinalIgnoreCase);

          clusters.Add(new ClusterConfig
          {
            ClusterId = serviceName,
            LoadBalancingPolicy = "RoundRobin",
            Destinations = destinations
          });
        }

        // Sadece gerçekten değişiklik varsa YARP'a sinyal gönder
        var signature = string.Join("|", services.Response.Values
            .OrderBy(s => s.ID)
            .Select(s => $"{s.ID}:{s.Service}:{s.Address}:{s.Port}"));

        if (signature == _lastSignature) return;
        _lastSignature = signature;

        var oldConfig = _currentConfig;
        _currentConfig = new ConsulProxyConfig(routes, clusters); // önce yeni config'i ata
        oldConfig.SignalChange();                                 // sonra eskisini iptal et

        _logger.LogInformation("YARP config güncellendi: {Count} servis", clusters.Count);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        // Consul'e ulaşılamazsa mevcut config korunur
        _logger.LogWarning(ex, "Consul'den servisler alınamadı, mevcut config korunuyor.");
      }
    }
  }
}

