namespace ApiGateway.Services
{
  public class ConsulConfigRefreshService : BackgroundService
  {
    private readonly ConsulProxyConfigProvider _configProvider;

    public ConsulConfigRefreshService(ConsulProxyConfigProvider configProvider)
        => _configProvider = configProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      await _configProvider.ReloadAsync(stoppingToken); // ilk yükleme hemen

      using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
      while (await timer.WaitForNextTickAsync(stoppingToken))
      {
        await _configProvider.ReloadAsync(stoppingToken);
      }
    }
  }
}
