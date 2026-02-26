namespace Worker.Models;

public class WorkerState
{
    private int _busyThreads;
    private readonly int _maxThreads;
    private long _tasksExecutedTotal;
    private long _tasksFailedTotal;

    public WorkerState(int? maxThreads = null)
    {
        _maxThreads = maxThreads ?? Math.Max(16, Environment.ProcessorCount);
    }

    public bool IsReady => _busyThreads < _maxThreads;
    public int FreeThreads => Math.Max(0, _maxThreads - _busyThreads);
    public int BusyThreads => _busyThreads;
    public int MaxThreads => _maxThreads;
    public long TasksExecutedTotal => Interlocked.Read(ref _tasksExecutedTotal);
    public long TasksFailedTotal => Interlocked.Read(ref _tasksFailedTotal);

    public void IncrementTasksExecuted() => Interlocked.Increment(ref _tasksExecutedTotal);
    public void IncrementTasksFailed() => Interlocked.Increment(ref _tasksFailedTotal);

    public bool TryStartTask()
    {
        while (true)
        {
            var current = _busyThreads;
            if (current >= _maxThreads) return false;
            if (Interlocked.CompareExchange(ref _busyThreads, current + 1, current) == current)
                return true;
        }
    }

    public void FinishTask()
    {
        while (true)
        {
            var current = _busyThreads;
            if (current <= 0) return;
            if (Interlocked.CompareExchange(ref _busyThreads, current - 1, current) == current)
                return;
        }
    }
}
