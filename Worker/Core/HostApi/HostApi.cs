namespace Worker.Core.HostApi;

/// <summary>
/// Direct host API implementation that executes in the worker process.
/// Replaces the RPC-based communication with direct method calls.
/// </summary>
public class HostApiImpl : IHostApi
{
    /// <summary>
    /// Logs a message directly to the console.
    /// </summary>
    public void Log(string message)
    {
        Console.WriteLine("[script] " + message);
    }

    /// <summary>
    /// Adds two integers and returns the result.
    /// </summary>
    public int Add(int a, int b)
    {
        var sum = a + b;
        Console.WriteLine($"[host] Add({a}, {b}) = {sum}");
        return sum;
    }

    /// <summary>
    /// Subtracts b from a and returns the result.
    /// </summary>
    public int Subtract(int a, int b)
    {
        var difference = a - b;
        Console.WriteLine($"[host] Subtract({a}, {b}) = {difference}");
        return difference;
    }
}
