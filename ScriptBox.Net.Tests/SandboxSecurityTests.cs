namespace ScriptBox.Net.Tests;

using System.IO;
using Xunit;
using global::ScriptBox.Net.Core.Configuration;
using global::ScriptBox.Net.Core.HostApi;

/// <summary>
/// Security tests for the sandbox file system and HTTP APIs.
/// Tests path traversal prevention, URL validation, and other security boundaries.
/// </summary>
public class SandboxSecurityTests : IDisposable
{
    private readonly string _testSandboxPath;
    private readonly SandboxConfiguration _config;
    private readonly HostApiImpl _hostApi;

    public SandboxSecurityTests()
    {
        // Create a temporary sandbox directory for testing
        _testSandboxPath = Path.Combine(Path.GetTempPath(), "sandbox_test_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testSandboxPath);

        _config = new SandboxConfiguration
        {
            SandboxDirectory = _testSandboxPath
        };
        _hostApi = new HostApiImpl(_config);
    }

    public void Dispose()
    {
        // Clean up test sandbox directory
        if (Directory.Exists(_testSandboxPath))
        {
            Directory.Delete(_testSandboxPath, recursive: true);
        }
    }

    #region File System Security Tests

    [Fact]
    public void FileSystemReadFile_PathTraversal_ThrowsSecurityException()
    {
        // Arrange
        var maliciousPath = "../../../etc/passwd";

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            _hostApi.FileSystemReadFile(maliciousPath));

        Assert.Contains("Path traversal detected", exception.Message);
    }

    [Fact]
    public void FileSystemReadFile_AbsolutePath_ThrowsSecurityException()
    {
        // Arrange
        var absolutePath = "/etc/passwd";

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            _hostApi.FileSystemReadFile(absolutePath));

        Assert.Contains("Absolute paths are not allowed", exception.Message);
    }

    [Fact]
    public void FileSystemWriteFile_PathTraversal_ThrowsSecurityException()
    {
        // Arrange
        var maliciousPath = "../../../tmp/malicious.txt";

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            _hostApi.FileSystemWriteFile(maliciousPath, "malicious content"));

        Assert.Contains("Path traversal detected", exception.Message);
    }

    [Fact]
    public void FileSystemWriteFile_ValidPath_CreatesFile()
    {
        // Arrange
        var validPath = "test.txt";
        var content = "Hello World";

        // Act
        _hostApi.FileSystemWriteFile(validPath, content);

        // Assert
        var fullPath = Path.Combine(_testSandboxPath, validPath);
        Assert.True(File.Exists(fullPath));
        Assert.Equal(content, File.ReadAllText(fullPath));
    }

    [Fact]
    public void FileSystemWriteFile_NestedPath_CreatesDirectories()
    {
        // Arrange
        var nestedPath = "dir1/dir2/test.txt";
        var content = "Nested content";

        // Act
        _hostApi.FileSystemWriteFile(nestedPath, content);

        // Assert
        var fullPath = Path.Combine(_testSandboxPath, nestedPath);
        Assert.True(File.Exists(fullPath));
        Assert.Equal(content, File.ReadAllText(fullPath));
    }

    [Fact]
    public void FileSystemReadFile_ValidFile_ReturnsContent()
    {
        // Arrange
        var filePath = "test.txt";
        var content = "Test content";
        var fullPath = Path.Combine(_testSandboxPath, filePath);
        File.WriteAllText(fullPath, content);

        // Act
        var result = _hostApi.FileSystemReadFile(filePath);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void FileSystemListFiles_ValidDirectory_ReturnsFiles()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_testSandboxPath, "subdir"));
        File.WriteAllText(Path.Combine(_testSandboxPath, "file1.txt"), "content1");
        File.WriteAllText(Path.Combine(_testSandboxPath, "file2.txt"), "content2");

        // Act
        var result = _hostApi.FileSystemListFiles(".");

        // Assert
        Assert.Contains("file1.txt", result);
        Assert.Contains("file2.txt", result);
        Assert.Contains("subdir", result);
    }

    [Fact]
    public void FileSystemExists_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var filePath = "exists.txt";
        File.WriteAllText(Path.Combine(_testSandboxPath, filePath), "content");

        // Act
        var result = _hostApi.FileSystemExists(filePath);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void FileSystemExists_NonExistingFile_ReturnsFalse()
    {
        // Arrange
        var filePath = "does-not-exist.txt";

        // Act
        var result = _hostApi.FileSystemExists(filePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void FileSystemDelete_ExistingFile_DeletesFile()
    {
        // Arrange
        var filePath = "to-delete.txt";
        var fullPath = Path.Combine(_testSandboxPath, filePath);
        File.WriteAllText(fullPath, "content");
        Assert.True(File.Exists(fullPath));

        // Act
        _hostApi.FileSystemDelete(filePath);

        // Assert
        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public void FileSystemDelete_ExistingDirectory_DeletesRecursively()
    {
        // Arrange
        var dirPath = "to-delete-dir";
        var fullPath = Path.Combine(_testSandboxPath, dirPath);
        Directory.CreateDirectory(fullPath);
        File.WriteAllText(Path.Combine(fullPath, "file.txt"), "content");
        Assert.True(Directory.Exists(fullPath));

        // Act
        _hostApi.FileSystemDelete(dirPath);

        // Assert
        Assert.False(Directory.Exists(fullPath));
    }

    [Fact]
    public void FileSystemCreateDirectory_ValidPath_CreatesDirectory()
    {
        // Arrange
        var dirPath = "newdir/subdir";
        var fullPath = Path.Combine(_testSandboxPath, dirPath);

        // Act
        _hostApi.FileSystemCreateDirectory(dirPath);

        // Assert
        Assert.True(Directory.Exists(fullPath));
    }

    #endregion

    #region HTTP Security Tests

    [Fact]
    public void HttpGet_InvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        var invalidUrl = "not-a-url";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _hostApi.HttpGet(invalidUrl));

        Assert.Contains("Invalid URL", exception.Message);
    }

    [Fact]
    public void HttpGet_FileProtocol_ThrowsSecurityException()
    {
        // Arrange
        var fileUrl = "file:///etc/passwd";

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            _hostApi.HttpGet(fileUrl));

        Assert.Contains("Only HTTP and HTTPS protocols are allowed", exception.Message);
    }

    [Fact]
    public void HttpGet_FtpProtocol_ThrowsSecurityException()
    {
        // Arrange
        var ftpUrl = "ftp://example.com/file.txt";

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            _hostApi.HttpGet(ftpUrl));

        Assert.Contains("Only HTTP and HTTPS protocols are allowed", exception.Message);
    }

    [Fact]
    public void HttpGet_DomainWhitelist_AllowedDomain_Succeeds()
    {
        // Arrange
        var config = new SandboxConfiguration
        {
            SandboxDirectory = _testSandboxPath,
            AllowedHttpDomains = new List<string> { "api.example.com" }
        };
        var hostApi = new HostApiImpl(config);

        // Act & Assert - Will fail with connection error, but not security exception
        var exception = Assert.ThrowsAny<Exception>(() =>
            hostApi.HttpGet("https://api.example.com/data"));

        // Should be InvalidOperationException (connection failed), not SecurityException
        Assert.IsNotType<SecurityException>(exception);
    }

    [Fact]
    public void HttpGet_DomainWhitelist_DisallowedDomain_ThrowsSecurityException()
    {
        // Arrange
        var config = new SandboxConfiguration
        {
            SandboxDirectory = _testSandboxPath,
            AllowedHttpDomains = new List<string> { "api.example.com" }
        };
        var hostApi = new HostApiImpl(config);

        // Act & Assert
        var exception = Assert.Throws<SecurityException>(() =>
            hostApi.HttpGet("https://evil.com/data"));

        Assert.Contains("Domain not in whitelist", exception.Message);
    }

    [Fact]
    public void HttpGet_DomainWhitelist_Subdomain_Succeeds()
    {
        // Arrange
        var config = new SandboxConfiguration
        {
            SandboxDirectory = _testSandboxPath,
            AllowedHttpDomains = new List<string> { "example.com" }
        };
        var hostApi = new HostApiImpl(config);

        // Act & Assert - Will fail with connection error, but not security exception
        var exception = Assert.ThrowsAny<Exception>(() =>
            hostApi.HttpGet("https://api.example.com/data"));

        // Should be InvalidOperationException (connection failed), not SecurityException
        Assert.IsNotType<SecurityException>(exception);
    }

    [Fact]
    public void HttpPost_InvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        var invalidUrl = "not-a-url";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _hostApi.HttpPost(invalidUrl, "{}"));

        Assert.Contains("Invalid URL", exception.Message);
    }

    [Fact]
    public void HttpRequest_InvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        var invalidRequest = "{\"url\":\"not-a-url\",\"method\":\"GET\"}";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _hostApi.HttpRequest(invalidRequest));

        Assert.Contains("Invalid URL", exception.Message);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void SandboxConfiguration_DefaultValues_AreValid()
    {
        // Arrange & Act
        var config = SandboxConfiguration.CreateDefault();

        // Assert
        Assert.NotNull(config.SandboxDirectory);
        Assert.Equal(10 * 1024 * 1024, config.MaxHttpResponseSize);
        Assert.Equal(30000, config.HttpTimeoutMs);
        Assert.Null(config.AllowedHttpDomains);
    }

    [Fact]
    public void SandboxConfiguration_Validate_InvalidSandboxDirectory_Throws()
    {
        // Arrange
        var config = new SandboxConfiguration
        {
            SandboxDirectory = ""
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            config.Validate());

        Assert.Contains("SandboxDirectory cannot be null or empty", exception.Message);
    }

    [Fact]
    public void SandboxConfiguration_Validate_InvalidMaxHttpResponseSize_Throws()
    {
        // Arrange
        var config = new SandboxConfiguration
        {
            MaxHttpResponseSize = -1
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            config.Validate());

        Assert.Contains("MaxHttpResponseSize must be positive", exception.Message);
    }

    [Fact]
    public void SandboxConfiguration_GetOrCreateSandboxDirectory_CreatesDirectory()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), "test_sandbox_" + Guid.NewGuid().ToString());
        var config = new SandboxConfiguration
        {
            SandboxDirectory = tempPath
        };

        try
        {
            // Act
            var result = config.GetOrCreateSandboxDirectory();

            // Assert
            Assert.True(Directory.Exists(result));
            Assert.Equal(Path.GetFullPath(tempPath), result);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    #endregion
}
