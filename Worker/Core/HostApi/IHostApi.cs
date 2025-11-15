namespace Worker.Core.HostApi;

/// <summary>
/// Interface for host API methods that can be called from the sandbox.
/// </summary>
public interface IHostApi
{
    /// <summary>
    /// Logs a message to the host console.
    /// </summary>
    void Log(string message);

    /// <summary>
    /// Adds two integers.
    /// </summary>
    int Add(int a, int b);

    /// <summary>
    /// Subtracts b from a.
    /// </summary>
    int Subtract(int a, int b);
}
