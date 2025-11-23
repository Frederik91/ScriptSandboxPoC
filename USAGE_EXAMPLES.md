# Usage Examples - File System and HTTP Client APIs

This document provides practical examples of using the newly implemented File System and HTTP Client APIs in the ScriptBox environment.

## File System API

The File System API provides sandboxed file operations. All file paths are relative to a configurable sandbox directory, preventing path traversal attacks.

### Reading and Writing Files

```javascript
// Write a simple text file
scriptbox.fs.writeFile('hello.txt', 'Hello, World!');

// Read it back
const content = scriptbox.fs.readFile('hello.txt');
console.log(content); // "Hello, World!"

// Write JSON data
const data = { name: 'Alice', age: 30, active: true };
scriptbox.fs.writeFile('data.json', JSON.stringify(data, null, 2));

// Read and parse JSON
const jsonContent = scriptbox.fs.readFile('data.json');
const parsed = JSON.parse(jsonContent);
console.log(parsed.name); // "Alice"
```

### Working with Directories

```javascript
// Create a directory (creates parent directories automatically)
scriptbox.fs.createDirectory('reports/2025/january');

// Write a file in the new directory
scriptbox.fs.writeFile('reports/2025/january/sales.txt', 'Total sales: $10,000');

// List files in a directory
const files = scriptbox.fs.listFiles('reports/2025/january');
files.forEach(file => {
    console.log(`${file.name} (${file.isDirectory ? 'directory' : 'file'}, ${file.size} bytes)`);
});
// Output: sales.txt (file, 20 bytes)
```

### Checking File Existence

```javascript
// Check if a file exists before reading
if (scriptbox.fs.exists('config.json')) {
    const config = JSON.parse(scriptbox.fs.readFile('config.json'));
    console.log('Loaded config:', config);
} else {
    console.log('Config file not found, using defaults');
}
```

### Deleting Files and Directories

```javascript
// Delete a file
scriptbox.fs.writeFile('temp.txt', 'temporary data');
scriptbox.fs.delete('temp.txt');

// Delete a directory (recursive)
scriptbox.fs.createDirectory('temp-dir/subdir');
scriptbox.fs.writeFile('temp-dir/subdir/file.txt', 'test');
scriptbox.fs.delete('temp-dir'); // Deletes directory and all contents
```

### Practical Example: Processing Multiple Files

```javascript
// Create a directory structure
scriptbox.fs.createDirectory('data');

// Write multiple data files
for (let i = 1; i <= 5; i++) {
    const data = { id: i, value: Math.random() * 100 };
    scriptbox.fs.writeFile(`data/record${i}.json`, JSON.stringify(data));
}

// Read and process all files
const files = scriptbox.fs.listFiles('data');
let total = 0;
let count = 0;

files.forEach(file => {
    if (!file.isDirectory && file.name.endsWith('.json')) {
        const content = scriptbox.fs.readFile(`data/${file.name}`);
        const data = JSON.parse(content);
        total += data.value;
        count++;
    }
});

const average = count > 0 ? total / count : 0;
console.log(`Processed ${count} files, average value: ${average.toFixed(2)}`);

// Save summary
const summary = { count, total, average };
scriptbox.fs.writeFile('data/summary.json', JSON.stringify(summary, null, 2));
```

## HTTP Client API

The HTTP Client API provides simple and advanced methods for making web requests with built-in security controls.

### Simple GET Request

```javascript
// Make a simple GET request
const response = scriptbox.http.get('https://jsonplaceholder.typicode.com/posts/1');
const post = JSON.parse(response);
console.log(`Title: ${post.title}`);
console.log(`Body: ${post.body}`);
```

### Simple POST Request

```javascript
// Post data to an API
const newPost = {
    title: 'My New Post',
    body: 'This is the content of my post',
    userId: 1
};

const response = scriptbox.http.post(
    'https://jsonplaceholder.typicode.com/posts',
    newPost
);

const created = JSON.parse(response);
console.log(`Created post with ID: ${created.id}`);
```

### Advanced Request with Custom Headers

```javascript
// Make an advanced request with custom headers and method
const response = scriptbox.http.request({
    url: 'https://api.example.com/data',
    method: 'PUT',
    headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer my-token-here',
        'X-Custom-Header': 'custom-value'
    },
    body: JSON.stringify({ key: 'value' })
});

console.log(`Status: ${response.status}`);
console.log(`Content-Type: ${response.headers['Content-Type']}`);
console.log(`Body: ${response.body}`);
```

### Practical Example: Fetch and Store Data

```javascript
// Fetch data from a public API
const response = scriptbox.http.get('https://jsonplaceholder.typicode.com/users');
const users = JSON.parse(response);

console.log(`Fetched ${users.length} users`);

// Create directory for user data
scriptbox.fs.createDirectory('users');

// Save each user to a separate file
users.forEach(user => {
    const filename = `users/user-${user.id}.json`;
    scriptbox.fs.writeFile(filename, JSON.stringify(user, null, 2));
    console.log(`Saved ${user.name} to ${filename}`);
});

// Create an index file with user summaries
const index = users.map(user => ({
    id: user.id,
    name: user.name,
    email: user.email
}));
scriptbox.fs.writeFile('users/index.json', JSON.stringify(index, null, 2));

console.log('All users saved successfully');
```

### Practical Example: API Integration with Error Handling

```javascript
// Fetch data with error handling
function fetchAndSave(url, filename) {
    try {
        console.log(`Fetching ${url}...`);
        const response = scriptbox.http.get(url);
        scriptbox.fs.writeFile(filename, response);
        console.log(`Saved to ${filename}`);
        return true;
    } catch (error) {
        console.log(`Error: ${error.message}`);
        return false;
    }
}

// Fetch multiple resources
const resources = [
    { url: 'https://jsonplaceholder.typicode.com/posts', file: 'posts.json' },
    { url: 'https://jsonplaceholder.typicode.com/comments', file: 'comments.json' },
    { url: 'https://jsonplaceholder.typicode.com/albums', file: 'albums.json' }
];

let successCount = 0;
resources.forEach(resource => {
    if (fetchAndSave(resource.url, resource.file)) {
        successCount++;
    }
});

console.log(`Successfully fetched ${successCount}/${resources.length} resources`);
```

## Configuration Examples

### C# Host Configuration

```csharp
using Worker.Core.Configuration;
using Worker.Core.WasmExecution;

// Create a custom sandbox configuration
var config = new SandboxConfiguration
{
    // Set custom sandbox directory
    SandboxDirectory = "/path/to/sandbox",

    // Limit HTTP responses to 5MB
    MaxHttpResponseSize = 5 * 1024 * 1024,

    // Set HTTP timeout to 15 seconds
    HttpTimeoutMs = 15000,

    // Optional: Restrict HTTP requests to specific domains
    AllowedHttpDomains = new List<string>
    {
        "api.example.com",
        "data.example.com"
    }
};

// Create executor with custom configuration
var executor = new WasmScriptExecutor(config);

// Execute script with configured sandbox
executor.ExecuteScript(@"
    // This will work (in whitelist)
    const data1 = scriptbox.http.get('https://api.example.com/data');

    // This will throw SecurityException (not in whitelist)
    // const data2 = scriptbox.http.get('https://other-site.com/data');

    // File operations are sandboxed to /path/to/sandbox
    scriptbox.fs.writeFile('output.txt', data1);
");
```

### Default Configuration

```csharp
// Use default configuration
var executor = new WasmScriptExecutor();

// Default settings:
// - SandboxDirectory: "./sandbox" in current working directory
// - MaxHttpResponseSize: 10MB
// - HttpTimeoutMs: 30000 (30 seconds)
// - AllowedHttpDomains: null (all domains allowed)
// - StartupScripts:
//     scripts/sdk/scriptbox.js
//     scripts/scriptbox.js (example API you can replace)
```

### Bringing your own ScriptBox API

The runtime now separates the **bridge helper** from the **API surface** so consuming apps can ship their own TypeScript without touching the core. Bootstrap scripts are concatenated (in order) before every evaluation, so you can swap `scriptbox.js` with your own file while reusing the helper in `scripts/sdk/scriptbox.js`.

1. **Author a TypeScript file** that uses `__scriptbox`:

```ts
// scripts/myAssistantApi.ts
const make = __scriptbox.createMethod;

const scriptboxApi = {
  math: {
    add: make('Math.Add'),
    subtract: make('Math.Subtract'),
  },
  files: {
    read(path: string) {
      return __scriptbox.hostCall('MyFiles.Read', [path]);
    },
  },
};

(globalThis as any).scriptbox = scriptboxApi;
```

2. **Compile/copy the file** into `scripts/myScriptBoxApi.js`.

3. **Point the sandbox configuration at your scripts**:

```csharp
var config = new SandboxConfiguration
{
    StartupScripts = new List<string>
    {
        Path.Combine("scripts", "sdk", "scriptbox.js"),
        Path.Combine("scripts", "myScriptBoxApi.js")
    }
};

var executor = new WasmScriptExecutor(config);
```

4. **Implement matching host methods** (extend `HostApiImpl` or plug in your own API registry) so calls such as `Math.Add` or `MyFiles.Read` are routed to trusted .NET code.

> Tip: `scriptbox.js` lives in `scripts/sdk` and only depends on the `__host.bridge` primitive provided by the WASM module, so you can also reuse it inside other runtimes (Node, Deno) when mocking your ScriptBox API.

## Security Considerations

### File System Security

1. **Path Traversal Prevention**: All paths like `../../../etc/passwd` are rejected
2. **Absolute Path Rejection**: Absolute paths like `/etc/passwd` are rejected
3. **Sandbox Enforcement**: All operations are confined to the configured sandbox directory

```javascript
// These will throw SecurityException:
// scriptbox.fs.readFile('../../../etc/passwd');
// scriptbox.fs.readFile('/etc/passwd');
// scriptbox.fs.writeFile('C:\\Windows\\System32\\config', 'data');

// This is safe (within sandbox):
scriptbox.fs.writeFile('subdir/file.txt', 'data');
```

### HTTP Security

1. **Protocol Restriction**: Only HTTP and HTTPS are allowed
2. **Domain Whitelist**: Optional whitelist prevents requests to unauthorized domains
3. **Response Size Limit**: Prevents memory exhaustion from large responses
4. **Timeout Control**: Prevents hanging on slow or unresponsive servers

```javascript
// These will throw SecurityException:
// scriptbox.http.get('file:///etc/passwd');
// scriptbox.http.get('ftp://example.com/file');

// This will throw if domain not in whitelist (when configured):
// scriptbox.http.get('https://unauthorized.com/data');
```

## Testing

The implementation includes comprehensive tests for both functionality and security:

- **Unit Tests**: `Worker.Tests/SandboxSecurityTests.cs`
- **Integration Tests**: `Worker.Tests/WasmIntegrationTests.cs`

Run tests with:
```bash
dotnet test
```

Run security tests only:
```bash
dotnet test --filter "FullyQualifiedName~SandboxSecurityTests"
```
