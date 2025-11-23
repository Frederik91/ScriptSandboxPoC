namespace ScriptBox.Tests;

using Xunit;
using Moq;
using global::ScriptBox.Core.HostApi;
using global::ScriptBox.Core.WasmExecution;

/// <summary>
/// Integration tests that perform actual WASM/QuickJS code execution.
/// These tests require the scriptbox.wasm module to be built.
/// Run 'ScriptBox.Wasm/build.bat' (Windows) or 'ScriptBox.Wasm/build.sh' (Unix) before running these tests.
/// 
/// NOTE: Some tests currently fail because 'console' and 'scriptbox' are not yet
/// bootstrapped in the WASM module (scriptbox_wrapper.c). These failures demonstrate
/// that the tests are actually executing code in QuickJS and validating the full stack.
/// 
/// Working tests (proving WASM/QuickJS execution):
/// - ExecuteScript_SimpleArithmetic_ExecutesSuccessfully ✓
/// - ExecuteScript_InvalidSyntax_ThrowsException ✓
/// - ExecuteScript_ReferenceError_ThrowsException ✓
/// 
/// Pending implementation in scriptbox_wrapper.c:
/// - Bootstrap 'console' object with log, warn, error methods
/// - Bootstrap 'scriptbox' object with host call bridge
/// </summary>
public class WasmIntegrationTests
{
    private readonly Mock<IHostApi> _mockHostApi;

    public WasmIntegrationTests()
    {
        _mockHostApi = new Mock<IHostApi>();
        
        // Setup default host API responses
        _mockHostApi.Setup(api => api.Log(It.IsAny<string>()));
        _mockHostApi.Setup(api => api.Add(It.IsAny<int>(), It.IsAny<int>()))
                    .Returns((int a, int b) => a + b);
        _mockHostApi.Setup(api => api.Subtract(It.IsAny<int>(), It.IsAny<int>()))
                    .Returns((int a, int b) => a - b);
    }

    [Fact]
    public void ExecuteScript_SimpleArithmetic_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "return 1 + 1";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2", result.Result);
    }

    [Fact]
    public void ExecuteScript_SumTwoNumbers_ReturnsNumber()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
function sum(a, b) {
    return a + b;
}
return sum(5, 3)";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("8", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnString_ReturnsString()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "return 'Hello World'";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello World", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnBoolean_ReturnsBoolean()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "return true";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("true", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnNull_ReturnsNull()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "return null";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("null", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnUndefined_ReturnsUndefined()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "undefined";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("undefined", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnObject_ReturnsJson()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "return { name: 'Alice', age: 30 }";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("\"name\"", result.Result);
        Assert.Contains("\"Alice\"", result.Result);
        Assert.Contains("\"age\"", result.Result);
        Assert.Contains("30", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnArray_ReturnsJson()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "return [1, 2, 3, 4, 5]";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("[1,2,3,4,5]", result.Result);
    }

    [Fact]
    public void ExecuteScript_ConsoleLog_CallsHostLog()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "console.log('Hello from QuickJS!')";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg == "Hello from QuickJS!")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_MultipleConsoleLogs_CallsHostLogMultipleTimes()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
console.log('first');
console.log('second');
console.log('third');
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(
            api => api.Log(It.IsAny<string>()),
            Times.Exactly(3));
    }

    [Fact]
    public void ExecuteScript_AssistantApiAdd_CallsHostAdd()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
const result = scriptbox.add(5, 3);
console.log('Result: ' + result);
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(
            api => api.Add(5, 3),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_VariablesAndOperations_ExecutesCorrectly()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let x = 10;
let y = 20;
let sum = x + y;
console.log('Sum: ' + sum);
return sum;
";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("30", result.Result);
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg == "Sum: 30")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_ArrayOperations_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let arr = [1, 2, 3, 4, 5];
let sum = arr.reduce((a, b) => a + b, 0);
console.log('Array sum: ' + sum);
";

        // Act
        var exception = Record.Exception(() => executor.ExecuteScript(code));

        // Assert
        Assert.Null(exception);
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg == "Array sum: 15")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_ForLoop_ExecutesAllIterations()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
for (let i = 0; i < 3; i++) {
    console.log('iteration: ' + i);
}
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(
            api => api.Log(It.IsAny<string>()),
            Times.Exactly(3));
    }

    [Fact]
    public void ExecuteScript_FunctionDeclaration_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
function greet(name) {
    return 'Hello, ' + name + '!';
}
let message = greet('World');
console.log(message);
";

        // Act
        var exception = Record.Exception(() => executor.ExecuteScript(code));

        // Assert
        Assert.Null(exception);
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg == "Hello, World!")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_ConditionalLogic_ExecutesCorrectBranch()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let value = 42;
if (value > 40) {
    console.log('greater');
} else {
    console.log('lesser');
}
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg == "greater")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_ObjectLiterals_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let person = {
    name: 'Alice',
    age: 30
};
console.log(person.name + ' is ' + person.age);
";

        // Act
        var exception = Record.Exception(() => executor.ExecuteScript(code));

        // Assert
        Assert.Null(exception);
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg == "Alice is 30")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_InvalidSyntax_ThrowsException()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "{{invalid js syntax";

        // Act
        var exception = Record.Exception(() => executor.ExecuteScript(code));

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("eval_js failed", exception.Message);
    }

    [Fact]
    public void ExecuteScript_ReferenceError_ThrowsException()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = "console.log(undefinedVariable);";

        // Act
        var exception = Record.Exception(() => executor.ExecuteScript(code));

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void ExecuteScript_ComplexNestedOperations_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
function calculate() {
    let numbers = [1, 2, 3, 4, 5];
    let result = numbers
        .map(x => x * 2)
        .filter(x => x > 5)
        .reduce((a, b) => a + b, 0);
    return result;
}
let total = calculate();
console.log('Total: ' + total);
";

        // Act
        var exception = Record.Exception(() => executor.ExecuteScript(code));

        // Assert
        Assert.Null(exception);
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg == "Total: 24")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_StringManipulation_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let text = 'hello world';
let upper = text.toUpperCase();
let parts = upper.split(' ');
console.log(parts.join('-'));
";

        // Act
        var exception = Record.Exception(() => executor.ExecuteScript(code));

        // Assert
        Assert.Null(exception);
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg == "HELLO-WORLD")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_TryCatchBlock_HandlesErrorGracefully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
try {
    throw new Error('test error');
} catch (e) {
    console.log('caught: ' + e.message);
}
";

        // Act
        var exception = Record.Exception(() => executor.ExecuteScript(code));

        // Assert
        Assert.Null(exception);
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg == "caught: test error")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_MultipleHostApiCalls_ExecutesInCorrectOrder()
    {
        // Arrange
        var loggedMessages = new List<string>();
        _mockHostApi.Setup(api => api.Log(It.IsAny<string>()))
                    .Callback<string>(msg => loggedMessages.Add(msg));

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
console.log('first');
let sum1 = scriptbox.add(1, 2);
console.log('sum1: ' + sum1);
let sum2 = scriptbox.add(3, 4);
console.log('sum2: ' + sum2);
console.log('last');
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        Assert.Equal(4, loggedMessages.Count);
        Assert.Equal("first", loggedMessages[0]);
        Assert.Equal("sum1: 3", loggedMessages[1]);
        Assert.Equal("sum2: 7", loggedMessages[2]);
        Assert.Equal("last", loggedMessages[3]);
    }

    [Fact]
    public void ExecuteScript_InfiniteLoop_ThrowsTimeoutException()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
while (true) {
    // Infinite loop
}
";

        // Act & Assert
        var exception = Assert.Throws<TimeoutException>(() =>
            executor.ExecuteScript(code, timeoutMs: 100));

        Assert.Contains("timeout", exception.Message.ToLower());
    }

    [Fact]
    public void ExecuteScript_LongRunningButFinite_CompletesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let sum = 0;
for (let i = 0; i < 100000; i++) {
    sum += i;
}
console.log('Sum: ' + sum);
";

        // Act - Give it enough time to complete
        var exception = Record.Exception(() =>
            executor.ExecuteScript(code, timeoutMs: 2000));

        // Assert
        Assert.Null(exception);
        _mockHostApi.Verify(
            api => api.Log(It.Is<string>(msg => msg.StartsWith("Sum:"))),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_CustomTimeout_RespectsTimeoutValue()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
while (true) {
    // Infinite loop
}
";

        // Act & Assert - Very short timeout should fail quickly
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var exception = Assert.Throws<TimeoutException>(() =>
            executor.ExecuteScript(code, timeoutMs: 50));
        sw.Stop();

        Assert.Contains("50ms", exception.Message);
        Assert.True(sw.ElapsedMilliseconds < 500,
            "Timeout should trigger quickly, not wait for default timeout");
    }

    [Fact]
    public void ExecuteScript_NoTimeout_AllowsLongExecution()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let sum = 0;
for (let i = 0; i < 1000000; i++) {
    sum += i;
}
console.log('Sum: ' + sum);
";

        // Act - timeout=0 means no timeout
        var exception = Record.Exception(() =>
            executor.ExecuteScript(code, timeoutMs: 0));

        // Assert
        Assert.Null(exception);
    }

    #region File System API Tests

    [Fact]
    public void ExecuteScript_FileSystemWriteAndRead_WorksCorrectly()
    {
        // Arrange
        _mockHostApi.Setup(api => api.FileSystemWriteFile("test.txt", "Hello World"));
        _mockHostApi.Setup(api => api.FileSystemReadFile("test.txt"))
                    .Returns("Hello World");

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
scriptbox.fs.writeFile('test.txt', 'Hello World');
const content = scriptbox.fs.readFile('test.txt');
console.log('Content: ' + content);
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(api => api.FileSystemWriteFile("test.txt", "Hello World"), Times.Once);
        _mockHostApi.Verify(api => api.FileSystemReadFile("test.txt"), Times.Once);
        _mockHostApi.Verify(api => api.Log("Content: Hello World"), Times.Once);
    }

    [Fact]
    public void ExecuteScript_FileSystemWriteJsonAndRead_WorksCorrectly()
    {
        // Arrange
        var jsonData = "{\"name\":\"test\",\"value\":42}";
        _mockHostApi.Setup(api => api.FileSystemWriteFile("data.json", jsonData));
        _mockHostApi.Setup(api => api.FileSystemReadFile("data.json"))
                    .Returns(jsonData);

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
const data = {name: 'test', value: 42};
scriptbox.fs.writeFile('data.json', JSON.stringify(data));
const content = scriptbox.fs.readFile('data.json');
const parsed = JSON.parse(content);
console.log('Name: ' + parsed.name + ', Value: ' + parsed.value);
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(api => api.FileSystemWriteFile("data.json", jsonData), Times.Once);
        _mockHostApi.Verify(api => api.FileSystemReadFile("data.json"), Times.Once);
        _mockHostApi.Verify(api => api.Log("Name: test, Value: 42"), Times.Once);
    }

    [Fact]
    public void ExecuteScript_FileSystemListFiles_WorksCorrectly()
    {
        // Arrange
        var filesJson = "[{\"name\":\"file1.txt\",\"isDirectory\":false,\"size\":100},{\"name\":\"subdir\",\"isDirectory\":true,\"size\":0}]";
        _mockHostApi.Setup(api => api.FileSystemListFiles("."))
                    .Returns(filesJson);

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
const files = scriptbox.fs.listFiles('.');
console.log('Files: ' + files.length);
files.forEach(f => console.log(f.name + ' (' + (f.isDirectory ? 'dir' : 'file') + ')'));
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(api => api.FileSystemListFiles("."), Times.Once);
        _mockHostApi.Verify(api => api.Log("Files: 2"), Times.Once);
        _mockHostApi.Verify(api => api.Log("file1.txt (file)"), Times.Once);
        _mockHostApi.Verify(api => api.Log("subdir (dir)"), Times.Once);
    }

    [Fact]
    public void ExecuteScript_FileSystemExists_WorksCorrectly()
    {
        // Arrange
        _mockHostApi.Setup(api => api.FileSystemExists("existing.txt"))
                    .Returns(true);
        _mockHostApi.Setup(api => api.FileSystemExists("missing.txt"))
                    .Returns(false);

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
const exists1 = scriptbox.fs.exists('existing.txt');
const exists2 = scriptbox.fs.exists('missing.txt');
console.log('existing.txt: ' + exists1);
console.log('missing.txt: ' + exists2);
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(api => api.FileSystemExists("existing.txt"), Times.Once);
        _mockHostApi.Verify(api => api.FileSystemExists("missing.txt"), Times.Once);
        _mockHostApi.Verify(api => api.Log("existing.txt: true"), Times.Once);
        _mockHostApi.Verify(api => api.Log("missing.txt: false"), Times.Once);
    }

    [Fact]
    public void ExecuteScript_FileSystemDelete_WorksCorrectly()
    {
        // Arrange
        _mockHostApi.Setup(api => api.FileSystemDelete("test.txt"));

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
scriptbox.fs.delete('test.txt');
console.log('File deleted');
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(api => api.FileSystemDelete("test.txt"), Times.Once);
        _mockHostApi.Verify(api => api.Log("File deleted"), Times.Once);
    }

    [Fact]
    public void ExecuteScript_FileSystemCreateDirectory_WorksCorrectly()
    {
        // Arrange
        _mockHostApi.Setup(api => api.FileSystemCreateDirectory("newdir/subdir"));

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
scriptbox.fs.createDirectory('newdir/subdir');
console.log('Directory created');
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(api => api.FileSystemCreateDirectory("newdir/subdir"), Times.Once);
        _mockHostApi.Verify(api => api.Log("Directory created"), Times.Once);
    }

    #endregion

    #region HTTP Client API Tests

    [Fact]
    public void ExecuteScript_HttpGet_WorksCorrectly()
    {
        // Arrange
        _mockHostApi.Setup(api => api.HttpGet("https://api.example.com/data"))
                    .Returns("{\"result\":\"success\"}");

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
const response = scriptbox.http.get('https://api.example.com/data');
const data = JSON.parse(response);
console.log('Result: ' + data.result);
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(api => api.HttpGet("https://api.example.com/data"), Times.Once);
        _mockHostApi.Verify(api => api.Log("Result: success"), Times.Once);
    }

    [Fact]
    public void ExecuteScript_HttpPost_WorksCorrectly()
    {
        // Arrange
        _mockHostApi.Setup(api => api.HttpPost("https://api.example.com/create", "{\"name\":\"test\"}"))
                    .Returns("{\"id\":123}");

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
const response = scriptbox.http.post('https://api.example.com/create', {name: 'test'});
const data = JSON.parse(response);
console.log('ID: ' + data.id);
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(api => api.HttpPost("https://api.example.com/create", "{\"name\":\"test\"}"), Times.Once);
        _mockHostApi.Verify(api => api.Log("ID: 123"), Times.Once);
    }

    [Fact]
    public void ExecuteScript_HttpRequest_WorksCorrectly()
    {
        // Arrange
        var responseJson = "{\"status\":200,\"headers\":{\"Content-Type\":\"application/json\"},\"body\":\"{\\\"data\\\":\\\"value\\\"}\"}";
        _mockHostApi.Setup(api => api.HttpRequest(It.IsAny<string>()))
                    .Returns(responseJson);

        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
const response = scriptbox.http.request({
    url: 'https://api.example.com/endpoint',
    method: 'PUT',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({key: 'value'})
});
console.log('Status: ' + response.status);
console.log('Body: ' + response.body);
";

        // Act
        executor.ExecuteScript(code);

        // Assert
        _mockHostApi.Verify(api => api.HttpRequest(It.IsAny<string>()), Times.Once);
        _mockHostApi.Verify(api => api.Log("Status: 200"), Times.Once);
        _mockHostApi.Verify(api => api.Log(It.Is<string>(msg => msg.StartsWith("Body:"))), Times.Once);
    }

    #endregion

    #region Return Statement Tests

    /// <summary>
    /// Tests for supporting explicit return statements in user scripts.
    /// User scripts are now wrapped in an IIFE (Immediately Invoked Function Expression)
    /// to allow return statements at the top level, which aligns with how LLMs generate code.
    /// </summary>

    [Fact]
    public void ExecuteScript_ExplicitReturnStatement_ReturnsValue()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let x = 42;
return x;
";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("42", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnObject_ReturnsJsonObject()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let result = { name: 'Alice', age: 30 };
return result;
";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Alice", result.Result);
        Assert.Contains("30", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnArray_ReturnsJsonArray()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let arr = [1, 2, 3, 4, 5];
return arr;
";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("[", result.Result);
        Assert.Contains("]", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnComplexStructure_ReturnsJson()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let text = 'hello world';
let upper = text.toUpperCase();
let len = upper.length;

return { 
    text_ops: [upper, len], 
    array_ops: [1, 2, 3]
};
";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("text_ops", result.Result);
        Assert.Contains("array_ops", result.Result);
        Assert.Contains("HELLO WORLD", result.Result);
    }

    [Fact]
    public void ExecuteScript_ExpressionAsReturnValue_ReturnsValue()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let x = 10;
let y = 20;
return x + y
";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("30", result.Result);
    }

    [Fact]
    public void ExecuteScript_ReturnEarly_SkipsFollowingCode()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockHostApi.Object);
        var code = @"
let result = { value: 'early' };
return result;
// This code should not execute:
throw new Error('Should not reach here');
";

        // Act
        var result = executor.ExecuteScript(code);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("early", result.Result);
    }

    #endregion
}
