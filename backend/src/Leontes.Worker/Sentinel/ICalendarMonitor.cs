namespace Leontes.Worker.Sentinel;

public interface ICalendarMonitor
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
