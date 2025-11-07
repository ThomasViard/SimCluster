namespace Worker.Models;

public class WorkerState(int? maxThreads = null)
{

    private int busyThreads = 0;
    private readonly int maxThreads = maxThreads ?? Environment.ProcessorCount;

    public bool IsReady => busyThreads < maxThreads;
    public int FreeThreads => maxThreads - busyThreads;

    public void StartTask() => Interlocked.Increment(ref busyThreads);
    public void FinishTask() => Interlocked.Decrement(ref busyThreads);
}
