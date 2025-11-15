using StreamJsonRpc;

namespace Worker.Core.RpcClient;

/// <summary>
/// Adapts StreamJsonRpc to the IRpcClient interface.
/// Provides a thin bridge between the low-level JSON-RPC transport and the domain logic.
/// </summary>
public class JsonRpcClientAdapter : IRpcClient
{
    private readonly JsonRpc _jsonRpc;

    public JsonRpcClientAdapter(JsonRpc jsonRpc)
    {
        _jsonRpc = jsonRpc ?? throw new ArgumentNullException(nameof(jsonRpc));
    }

    /// <inheritdoc />
    public async Task<T> InvokeAsync<T>(string method, params object?[]? arguments)
    {
        return await _jsonRpc.InvokeAsync<T>(method, arguments);
    }
}
