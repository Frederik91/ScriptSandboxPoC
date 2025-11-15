using StreamJsonRpc;
using System.IO.Pipes;
using Worker.Core.RpcClient;

namespace Worker.Services;

/// <summary>
/// Sets up JSON-RPC communication with the host process over a named pipe.
/// </summary>
public static class WorkerRpcSetup
{
    /// <summary>
    /// Initializes the RPC connection and starts listening for host calls.
    /// </summary>
    /// <param name="pipeName">The name of the named pipe to connect to.</param>
    /// <param name="rpcTarget">The target object containing RPC methods to expose.</param>
    /// <returns>A task that completes when the RPC connection is closed.</returns>
    /// <exception cref="ArgumentException">Thrown when pipeName is null or empty.</exception>
    public static async Task SetupAndRunAsync(string pipeName, object rpcTarget)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name cannot be null or empty.", nameof(pipeName));
        }

        if (rpcTarget == null)
        {
            throw new ArgumentNullException(nameof(rpcTarget));
        }

        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync();

        var rpc = new JsonRpc(pipe, pipe);
        rpc.AddLocalRpcTarget(rpcTarget);
        rpc.StartListening();

        // Wait until the host closes the stream
        await rpc.Completion;
    }
}
