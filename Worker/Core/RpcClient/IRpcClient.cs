namespace Worker.Core.RpcClient;

/// <summary>
/// Abstraction for JSON-RPC communication with the host process.
/// Enables dependency injection and testability by decoupling from StreamJsonRpc.
/// </summary>
public interface IRpcClient
{
    /// <summary>
    /// Invokes a remote procedure on the host.
    /// </summary>
    /// <typeparam name="T">The return type of the RPC method.</typeparam>
    /// <param name="method">The name of the RPC method.</param>
    /// <param name="arguments">Arguments to pass to the RPC method.</param>
    /// <returns>The result of the RPC call.</returns>
    Task<T> InvokeAsync<T>(string method, params object?[]? arguments);
}
