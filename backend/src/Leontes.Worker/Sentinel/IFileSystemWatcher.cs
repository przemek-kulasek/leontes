namespace Leontes.Worker.Sentinel;

public interface IFileSystemWatcher
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
