namespace ScriptBox.Core.HostApi;

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

    // File System API

    /// <summary>
    /// Reads the entire contents of a file as a UTF-8 string.
    /// Path must be within the configured sandbox directory.
    /// </summary>
    /// <param name="path">Relative path to the file within the sandbox.</param>
    /// <returns>File contents as a string.</returns>
    string FileSystemReadFile(string path);

    /// <summary>
    /// Writes a string to a file, creating parent directories if needed.
    /// Path must be within the configured sandbox directory.
    /// </summary>
    /// <param name="path">Relative path to the file within the sandbox.</param>
    /// <param name="content">Content to write to the file.</param>
    void FileSystemWriteFile(string path, string content);

    /// <summary>
    /// Lists files and directories in a directory.
    /// Path must be within the configured sandbox directory.
    /// </summary>
    /// <param name="path">Relative path to the directory within the sandbox.</param>
    /// <returns>JSON array of file information objects.</returns>
    string FileSystemListFiles(string path);

    /// <summary>
    /// Checks if a file or directory exists.
    /// Path must be within the configured sandbox directory.
    /// </summary>
    /// <param name="path">Relative path to check within the sandbox.</param>
    /// <returns>True if the path exists, false otherwise.</returns>
    bool FileSystemExists(string path);

    /// <summary>
    /// Deletes a file or directory (recursive for directories).
    /// Path must be within the configured sandbox directory.
    /// </summary>
    /// <param name="path">Relative path to delete within the sandbox.</param>
    void FileSystemDelete(string path);

    /// <summary>
    /// Creates a directory, including parent directories if needed.
    /// Path must be within the configured sandbox directory.
    /// </summary>
    /// <param name="path">Relative path to the directory to create within the sandbox.</param>
    void FileSystemCreateDirectory(string path);

    // HTTP Client API

    /// <summary>
    /// Performs a simple HTTP GET request and returns the response body as a string.
    /// </summary>
    /// <param name="url">URL to request.</param>
    /// <returns>Response body as a string.</returns>
    string HttpGet(string url);

    /// <summary>
    /// Performs a simple HTTP POST request with JSON data and returns the response body as a string.
    /// </summary>
    /// <param name="url">URL to request.</param>
    /// <param name="dataJson">JSON string to send as the request body.</param>
    /// <returns>Response body as a string.</returns>
    string HttpPost(string url, string dataJson);

    /// <summary>
    /// Performs an advanced HTTP request with custom method, headers, and body.
    /// </summary>
    /// <param name="optionsJson">JSON object containing url, method, headers, and body.</param>
    /// <returns>JSON object containing status, headers, and body.</returns>
    string HttpRequest(string optionsJson);
}
