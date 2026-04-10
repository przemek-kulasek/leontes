namespace Leontes.Worker.Sentinel;

public interface IActiveWindowMonitor
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
