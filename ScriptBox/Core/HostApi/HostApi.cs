using System.Text;
using System.Text.Json;
using ScriptBox.Core.Configuration;

namespace ScriptBox.Core.HostApi;

/// <summary>
/// Direct host API implementation that executes in the worker process.
/// Replaces the RPC-based communication with direct method calls.
/// </summary>
public class HostApiImpl : IHostApi
{
    private readonly SandboxConfiguration _config;
    private readonly string _sandboxRoot;
    private readonly HttpClient _httpClient;

    public HostApiImpl(SandboxConfiguration? config = null)
    {
        _config = config ?? SandboxConfiguration.CreateDefault();
        _config.Validate();
        _sandboxRoot = _config.GetOrCreateSandboxDirectory();
        
        if (_config.HttpClientFactory != null)
        {
            _httpClient = _config.HttpClientFactory();
        }
        else
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(_config.HttpTimeoutMs)
            };
        }
        
        _config.HttpClientConfigurator?.Invoke(_httpClient);
    }

    /// <summary>
    /// Logs a message directly to the console.
    /// </summary>
    public void Log(string message)
    {
        Console.WriteLine("[scriptbox] " + message);
    }

    /// <summary>
    /// Adds two integers and returns the result.
    /// </summary>
    public int Add(int a, int b)
    {
        var sum = a + b;
        Console.WriteLine($"[scriptbox.host] Add({a}, {b}) = {sum}");
        return sum;
    }

    /// <summary>
    /// Subtracts b from a and returns the result.
    /// </summary>
    public int Subtract(int a, int b)
    {
        var difference = a - b;
        Console.WriteLine($"[scriptbox.host] Subtract({a}, {b}) = {difference}");
        return difference;
    }

    #region File System API

    /// <summary>
    /// Validates and resolves a path within the sandbox, preventing path traversal attacks.
    /// </summary>
    private string ValidateAndResolvePath(string relativePath, string operation)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Path cannot be null or empty");
        }

        // Prevent absolute paths
        if (Path.IsPathRooted(relativePath))
        {
            throw new SecurityException("Absolute paths are not allowed");
        }

        // Combine with sandbox root and get full path
        var fullPath = Path.GetFullPath(Path.Combine(_sandboxRoot, relativePath));

        // Ensure the resolved path is still within the sandbox
        if (!fullPath.StartsWith(_sandboxRoot, StringComparison.OrdinalIgnoreCase))
        {
            // Check if consent hook allows this access
            if (_config.FileSystemConsentHook != null && _config.FileSystemConsentHook(new FileSystemConsentContext(fullPath, operation)))
            {
                return fullPath;
            }

            throw new SecurityException($"Path traversal detected: {relativePath}");
        }

        return fullPath;
    }

    public string FileSystemReadFile(string path)
    {
        var fullPath = ValidateAndResolvePath(path, "Read");

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    public void FileSystemWriteFile(string path, string content)
    {
        var fullPath = ValidateAndResolvePath(path, "Write");

        // Create parent directory if it doesn't exist
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content, Encoding.UTF8);
    }

    public string FileSystemListFiles(string path)
    {
        var fullPath = ValidateAndResolvePath(path, "List");

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        var entries = new List<object>();

        // List directories
        foreach (var dir in Directory.GetDirectories(fullPath))
        {
            var dirInfo = new DirectoryInfo(dir);
            entries.Add(new
            {
                name = dirInfo.Name,
                isDirectory = true,
                size = 0
            });
        }

        // List files
        foreach (var file in Directory.GetFiles(fullPath))
        {
            var fileInfo = new FileInfo(file);
            entries.Add(new
            {
                name = fileInfo.Name,
                isDirectory = false,
                size = fileInfo.Length
            });
        }

        return JsonSerializer.Serialize(entries);
    }

    public bool FileSystemExists(string path)
    {
        var fullPath = ValidateAndResolvePath(path, "Exists");
        return File.Exists(fullPath) || Directory.Exists(fullPath);
    }

    public void FileSystemDelete(string path)
    {
        var fullPath = ValidateAndResolvePath(path, "Delete");

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        else if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }
    }

    public void FileSystemCreateDirectory(string path)
    {
        var fullPath = ValidateAndResolvePath(path, "CreateDirectory");
        Directory.CreateDirectory(fullPath);
    }

    #endregion

    #region HTTP Client API

    /// <summary>
    /// Validates a URL for security (protocol check and optional domain whitelist).
    /// </summary>
    private void ValidateRequest(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri == null)
        {
            throw new ArgumentException("URL cannot be null");
        }

        // Only allow HTTP and HTTPS
        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            throw new SecurityException($"Only HTTP and HTTPS protocols are allowed, got: {uri.Scheme}");
        }

        // Check domain whitelist if configured
        if (_config.AllowedHttpDomains != null && _config.AllowedHttpDomains.Count > 0)
        {
            var host = uri.Host.ToLowerInvariant();
            var allowed = _config.AllowedHttpDomains.Any(domain =>
                host.Equals(domain.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + domain.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
            );

            if (!allowed)
            {
                // Check if consent hook allows this access
                if (_config.NetworkConsentHook != null && _config.NetworkConsentHook(new NetworkConsentContext(request)))
                {
                    return;
                }

                throw new SecurityException($"Domain not in whitelist: {uri.Host}");
            }
        }
    }

    /// <summary>
    /// Validates that the response size is within limits.
    /// </summary>
    private async Task<string> ReadResponseWithLimitAsync(HttpResponseMessage response)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        using var memoryStream = new MemoryStream();

        var buffer = new byte[8192];
        var totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > _config.MaxHttpResponseSize)
            {
                throw new InvalidOperationException(
                    $"Response size exceeds maximum allowed size of {_config.MaxHttpResponseSize} bytes");
            }

            memoryStream.Write(buffer, 0, bytesRead);
        }

        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public string HttpGet(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL cannot be null or empty");
        
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ValidateRequest(request);

        try
        {
            var response = _httpClient.SendAsync(request).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return ReadResponseWithLimitAsync(response).GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP GET request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException($"HTTP GET request timed out after {_config.HttpTimeoutMs}ms");
        }
    }

    public string HttpPost(string url, string dataJson)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL cannot be null or empty");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(dataJson, Encoding.UTF8, "application/json");
        ValidateRequest(request);

        try
        {
            var response = _httpClient.SendAsync(request).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return ReadResponseWithLimitAsync(response).GetAwaiter().GetResult();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP POST request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException($"HTTP POST request timed out after {_config.HttpTimeoutMs}ms");
        }
    }

    public string HttpRequest(string optionsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(optionsJson);
            var root = doc.RootElement;

            var url = root.GetProperty("url").GetString()
                ?? throw new ArgumentException("url is required");
            var method = root.GetProperty("method").GetString()
                ?? throw new ArgumentException("method is required");

            var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), url);

            // Add custom headers if provided
            if (root.TryGetProperty("headers", out var headersElement))
            {
                foreach (var header in headersElement.EnumerateObject())
                {
                    request.Headers.TryAddWithoutValidation(header.Name, header.Value.GetString());
                }
            }

            // Add body if provided
            if (root.TryGetProperty("body", out var bodyElement))
            {
                var body = bodyElement.GetString();
                if (!string.IsNullOrEmpty(body))
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }
            }

            ValidateRequest(request);

            var response = _httpClient.SendAsync(request).GetAwaiter().GetResult();
            var responseBody = ReadResponseWithLimitAsync(response).GetAwaiter().GetResult();

            // Build response headers dictionary
            var responseHeaders = new Dictionary<string, string>();
            foreach (var header in response.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }
            foreach (var header in response.Content.Headers)
            {
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            }

            var result = new
            {
                status = (int)response.StatusCode,
                headers = responseHeaders,
                body = responseBody
            };

            return JsonSerializer.Serialize(result);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"HTTP request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException($"HTTP request timed out after {_config.HttpTimeoutMs}ms");
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON in request options: {ex.Message}", ex);
        }
    }

    #endregion
}

/// <summary>
/// Custom exception for security violations.
/// </summary>
public class SecurityException : Exception
{
    public SecurityException(string message) : base(message) { }
}
