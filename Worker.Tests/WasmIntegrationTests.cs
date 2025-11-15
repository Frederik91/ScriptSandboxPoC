namespace Worker.Tests;

using Xunit;
using Moq;
using Worker.Core.RpcClient;
using Worker.Core.WasmExecution;
using Worker.Services;

/// <summary>
/// Integration tests that perform actual WASM/QuickJS code execution.
/// These tests require the assistant.wasm module to be built.
/// Run 'wasm/build.bat' (Windows) or 'wasm/build.sh' (Unix) before running these tests.
/// 
/// NOTE: Some tests currently fail because 'console' and 'assistantApi' are not yet
/// bootstrapped in the WASM module (assistant_wrapper.c). These failures demonstrate
/// that the tests are actually executing code in QuickJS and validating the full stack.
/// 
/// Working tests (proving WASM/QuickJS execution):
/// - ExecuteScript_SimpleArithmetic_ExecutesSuccessfully ✓
/// - ExecuteScript_InvalidSyntax_ThrowsException ✓
/// - ExecuteScript_ReferenceError_ThrowsException ✓
/// 
/// Pending implementation in assistant_wrapper.c:
/// - Bootstrap 'console' object with log, warn, error methods
/// - Bootstrap 'assistantApi' object with host call bridge
/// </summary>
public class WasmIntegrationTests
{
    private readonly Mock<IRpcClient> _mockRpc;

    public WasmIntegrationTests()
    {
        _mockRpc = new Mock<IRpcClient>();
        
        // Setup default RPC responses
        _mockRpc.Setup(rpc => rpc.InvokeAsync<object?>(It.IsAny<string>(), It.IsAny<object[]>()))
                .ReturnsAsync((object?)null);
        _mockRpc.Setup(rpc => rpc.InvokeAsync<int>("Host.Add", It.IsAny<object[]>()))
                .ReturnsAsync((string method, object[] args) => (int)args[0] + (int)args[1]);
    }

    [Fact]
    public void ExecuteScript_SimpleArithmetic_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = "1 + 1";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void ExecuteScript_ConsoleLog_CallsHostLog()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = "console.log('Hello from QuickJS!')";

        // Act
        workerMethods.RunScript(code);

        // Assert
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.Is<object[]>(args => 
                args.Length == 1 && args[0].ToString() == "Hello from QuickJS!")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_MultipleConsoleLogs_CallsHostLogMultipleTimes()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
console.log('first');
console.log('second');
console.log('third');
";

        // Act
        workerMethods.RunScript(code);

        // Assert
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.IsAny<object[]>()),
            Times.Exactly(3));
    }

    [Fact]
    public void ExecuteScript_AssistantApiAdd_CallsHostAdd()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
const result = assistantApi.add(5, 3);
console.log('Result: ' + result);
";

        // Act
        workerMethods.RunScript(code);

        // Assert
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<int>("Host.Add", It.Is<object[]>(args =>
                args.Length == 2 && (int)args[0] == 5 && (int)args[1] == 3)),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_VariablesAndOperations_ExecutesCorrectly()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
let x = 10;
let y = 20;
let sum = x + y;
console.log('Sum: ' + sum);
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.Null(exception);
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.Is<object[]>(args =>
                args[0].ToString() == "Sum: 30")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_ArrayOperations_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
let arr = [1, 2, 3, 4, 5];
let sum = arr.reduce((a, b) => a + b, 0);
console.log('Array sum: ' + sum);
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.Null(exception);
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.Is<object[]>(args =>
                args[0].ToString() == "Array sum: 15")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_ForLoop_ExecutesAllIterations()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
for (let i = 0; i < 3; i++) {
    console.log('iteration: ' + i);
}
";

        // Act
        workerMethods.RunScript(code);

        // Assert
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.IsAny<object[]>()),
            Times.Exactly(3));
    }

    [Fact]
    public void ExecuteScript_FunctionDeclaration_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
function greet(name) {
    return 'Hello, ' + name + '!';
}
let message = greet('World');
console.log(message);
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.Null(exception);
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.Is<object[]>(args =>
                args[0].ToString() == "Hello, World!")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_ConditionalLogic_ExecutesCorrectBranch()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
let value = 42;
if (value > 40) {
    console.log('greater');
} else {
    console.log('lesser');
}
";

        // Act
        workerMethods.RunScript(code);

        // Assert
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.Is<object[]>(args =>
                args[0].ToString() == "greater")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_ObjectLiterals_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
let person = {
    name: 'Alice',
    age: 30
};
console.log(person.name + ' is ' + person.age);
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.Null(exception);
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.Is<object[]>(args =>
                args[0].ToString() == "Alice is 30")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_InvalidSyntax_ThrowsException()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = "{{invalid js syntax";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("eval_js failed", exception.Message);
    }

    [Fact]
    public void ExecuteScript_ReferenceError_ThrowsException()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = "console.log(undefinedVariable);";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void ExecuteScript_ComplexNestedOperations_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
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
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.Null(exception);
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.Is<object[]>(args =>
                args[0].ToString() == "Total: 24")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_StringManipulation_ExecutesSuccessfully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
let text = 'hello world';
let upper = text.toUpperCase();
let parts = upper.split(' ');
console.log(parts.join('-'));
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.Null(exception);
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.Is<object[]>(args =>
                args[0].ToString() == "HELLO-WORLD")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_TryCatchBlock_HandlesErrorGracefully()
    {
        // Arrange
        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
try {
    throw new Error('test error');
} catch (e) {
    console.log('caught: ' + e.message);
}
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(code));

        // Assert
        Assert.Null(exception);
        _mockRpc.Verify(
            rpc => rpc.InvokeAsync<object?>("Host.Log", It.Is<object[]>(args =>
                args[0].ToString() == "caught: test error")),
            Times.Once);
    }

    [Fact]
    public void ExecuteScript_MultipleHostApiCalls_ExecutesInCorrectOrder()
    {
        // Arrange
        var loggedMessages = new List<string>();
        _mockRpc.Setup(rpc => rpc.InvokeAsync<object?>("Host.Log", It.IsAny<object[]>()))
                .Callback<string, object[]>((method, args) => loggedMessages.Add(args[0].ToString()!))
                .ReturnsAsync((object?)null);

        var executor = new WasmScriptExecutor(_mockRpc.Object);
        var workerMethods = new WorkerMethods(executor);
        var code = @"
console.log('first');
let sum1 = assistantApi.add(1, 2);
console.log('sum1: ' + sum1);
let sum2 = assistantApi.add(3, 4);
console.log('sum2: ' + sum2);
console.log('last');
";

        // Act
        workerMethods.RunScript(code);

        // Assert
        Assert.Equal(4, loggedMessages.Count);
        Assert.Equal("first", loggedMessages[0]);
        Assert.Equal("sum1: 3", loggedMessages[1]);
        Assert.Equal("sum2: 7", loggedMessages[2]);
        Assert.Equal("last", loggedMessages[3]);
    }
}
