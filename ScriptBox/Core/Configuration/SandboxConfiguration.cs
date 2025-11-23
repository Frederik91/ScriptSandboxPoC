using System.Net.Http;

namespace ScriptBox.Core.Configuration;

/// <summary>
/// Context for file system consent checks.
/// </summary>
/// <param name="Path">The absolute path being accessed.</param>
/// <param name="Operation">The operation being performed (Read, Write, List, Exists, Delete, CreateDirectory).</param>
public record FileSystemConsentContext(string Path, string Operation);

/// <summary>
/// Context for network consent checks.
/// </summary>
/// <param name="Request">The HTTP request message containing URL, method, headers, and content.</param>
public record NetworkConsentContext(HttpRequestMessage Request);

/// <summary>
/// Configuration for sandbox security and resource limits.
/// Controls file system access, HTTP requests, and other security boundaries.
/// </summary>
public class SandboxConfiguration
{
    /// <summary>
    /// Root directory for sandboxed file system operations.
    /// All file paths will be resolved relative to this directory.
    /// Defaults to "./sandbox" in the current working directory.
    /// </summary>
    public string SandboxDirectory { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "sandbox");

    /// <summary>
    /// Optional whitelist of allowed domains for HTTP requests.
    /// If null or empty, all domains are allowed.
    /// Example: ["api.example.com", "data.example.org"]
    /// </summary>
    public List<string>? AllowedHttpDomains { get; set; }

    /// <summary>
    /// Maximum allowed size for HTTP response bodies in bytes.
    /// Defaults to 10MB to prevent memory exhaustion.
    /// </summary>
    public int MaxHttpResponseSize { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Timeout for HTTP requests in milliseconds.
    /// Defaults to 30 seconds.
    /// </summary>
    public int HttpTimeoutMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Scripts that should be prepended before every user script.
    /// Developers can remove scriptbox-api.js from this list to provide their own API surface.
    /// Paths can be absolute or relative to the application base directory.
    /// </summary>
    public List<string> BootstrapScripts { get; set; } = new()
    {
        Path.Combine("scripts", "sdk", "scriptbox.js"),
        Path.Combine("scripts", "scriptbox-api.js")
    };

    /// <summary>
    /// Optional callback to configure the HttpClient used for network requests.
    /// </summary>
    public Action<HttpClient>? HttpClientConfigurator { get; set; }

    /// <summary>
    /// Optional factory to create the HttpClient used for network requests.
    /// Use this to provide custom message handlers (e.g. for authentication or mocking).
    /// </summary>
    public Func<HttpClient>? HttpClientFactory { get; set; }

    /// <summary>
    /// Optional hook to request consent for file system access that would otherwise be denied.
    /// Return true to allow, false to deny.
    /// </summary>
    public Func<FileSystemConsentContext, bool>? FileSystemConsentHook { get; set; }

    /// <summary>
    /// Optional hook to request consent for network access that would otherwise be denied.
    /// Return true to allow, false to deny.
    /// </summary>
    public Func<NetworkConsentContext, bool>? NetworkConsentHook { get; set; }

    /// <summary>
    /// Creates a default configuration with reasonable security settings.
    /// </summary>
    public static SandboxConfiguration CreateDefault()
    {
        return new SandboxConfiguration();
    }

    /// <summary>
    /// Validates the configuration and throws if invalid.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SandboxDirectory))
        {
            throw new InvalidOperationException("SandboxDirectory cannot be null or empty");
        }

        if (MaxHttpResponseSize <= 0)
        {
            throw new InvalidOperationException("MaxHttpResponseSize must be positive");
        }

        if (HttpTimeoutMs <= 0)
        {
            throw new InvalidOperationException("HttpTimeoutMs must be positive");
        }

        BootstrapScripts ??= new List<string>();
    }

    /// <summary>
    /// Ensures the sandbox directory exists and returns the absolute path.
    /// </summary>
    public string GetOrCreateSandboxDirectory()
    {
        var absolutePath = Path.GetFullPath(SandboxDirectory);
        Directory.CreateDirectory(absolutePath);
        return absolutePath;
    }
}
