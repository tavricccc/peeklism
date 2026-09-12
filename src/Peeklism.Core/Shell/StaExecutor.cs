using System.Collections.Concurrent;

namespace Peeklism.Core.Shell;

/// <summary>
/// Runs work on a single long-lived STA thread. The shell automation objects are apartment
/// threaded, and creating a thread per key press would add latency to the one path that has
/// to feel instant.
/// </summary>
public sealed class StaExecutor : IDisposable
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;

    public StaExecutor(string name)
    {
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public T Invoke<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        using var completion = new ManualResetEventSlim(false);
        var result = default(T);
        Exception? failure = null;
        _queue.Add(() =>
        {
            try { result = work(); }
            catch (Exception exception) when (exception is not OutOfMemoryException) { failure = exception; }
            finally { completion.Set(); }
        }, cancellationToken);
        completion.Wait(cancellationToken);
        if (failure is not null)
        {
            throw new InvalidOperationException($"STA 工作失敗：{failure.Message}", failure);
        }

        return result!;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
        _queue.Dispose();
    }

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            work();
        }
    }
}
