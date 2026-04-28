using System.Collections.Concurrent;

namespace Leontes.Vision.Windows;

/// <summary>
/// Single-threaded TaskScheduler whose worker thread runs in STA apartment state.
/// Windows UI Automation pattern providers (especially TextPattern) marshal calls
/// through COM and behave reliably only on STA threads — calling from the
/// ASP.NET Core MTA thread pool causes intermittent COMException and silent
/// data loss for some applications.
/// </summary>
internal sealed class StaTaskScheduler : TaskScheduler, IDisposable
{
    private readonly BlockingCollection<Task> _tasks = [];
    private readonly Thread _thread;

    public StaTaskScheduler()
    {
        _thread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "Leontes.Vision.STA"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public override int MaximumConcurrencyLevel => 1;

    protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

    protected override void QueueTask(Task task) => _tasks.Add(task);

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

    private void WorkerLoop()
    {
        foreach (var task in _tasks.GetConsumingEnumerable())
        {
            TryExecuteTask(task);
        }
    }

    public void Dispose()
    {
        _tasks.CompleteAdding();
        if (_thread.IsAlive)
            _thread.Join(TimeSpan.FromSeconds(5));
        _tasks.Dispose();
    }
}
