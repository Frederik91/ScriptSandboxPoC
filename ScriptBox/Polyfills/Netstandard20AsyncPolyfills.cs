#if NETSTANDARD2_0
using System.Runtime.CompilerServices;

namespace System
{
    public interface IAsyncDisposable
    {
        System.Threading.Tasks.ValueTask DisposeAsync();
    }
}

namespace System.Threading.Tasks
{
    public readonly struct ValueTask
    {
        private readonly Task? _task;

        public ValueTask(Task task)
        {
            _task = task ?? Task.CompletedTask;
        }

        public Task AsTask() => _task ?? Task.CompletedTask;

        public TaskAwaiter GetAwaiter() => AsTask().GetAwaiter();

        public static implicit operator ValueTask(Task task) => new ValueTask(task);

        public static ValueTask CompletedTask => new ValueTask(Task.CompletedTask);
    }
}
#endif
