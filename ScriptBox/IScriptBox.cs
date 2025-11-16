using System;

namespace ScriptBox;

/// <summary>
/// Represents a compiled ScriptBox runtime. Sessions created from the same
/// instance share the underlying WASM module and host bridge configuration.
/// </summary>
#if NET6_0_OR_GREATER
public interface IScriptBox : IAsyncDisposable
{
#else
public interface IScriptBox : IDisposable
{
#endif
    /// <summary>
    /// Creates a new session for executing scripts.
    /// </summary>
    /// <param name="timeout">Optional timeout for the session. If not specified, uses the default timeout.</param>
    /// <returns>A new script session.</returns>
    ScriptSession CreateSession(TimeSpan? timeout = null);
}
