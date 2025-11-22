using System.Threading;

namespace Scriptbox.SemanticKernel.Example;

public sealed class TokenSink
{
    private long _input;
    private long _output;

    public void Add(long input, long output)
    {
        Interlocked.Add(ref _input, input);
        Interlocked.Add(ref _output, output);
    }

    public (long Input, long Output) Snapshot() =>
        (Interlocked.Read(ref _input), Interlocked.Read(ref _output));

    public void Reset()
    {
        Interlocked.Exchange(ref _input, 0);
        Interlocked.Exchange(ref _output, 0);
    }
}
