namespace Leontes.Worker.Sentinel;

public interface IClipboardMonitor
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
