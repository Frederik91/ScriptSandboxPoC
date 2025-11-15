namespace Worker.Tests;

using Xunit;
using Moq;

/// <summary>
/// TDD tests for WorkerMethods WASM execution.
/// These tests define the desired behavior - fix the code to make them pass.
/// </summary>
public class WorkerMethodsTests
{
    [Fact]
    public void RunScript_WithSimpleJavaScript_ExecutesSuccessfully()
    {
        // Arrange
        var mockRpc = new Mock<IRpcClient>();
        mockRpc
            .Setup(r => r.InvokeAsync<object?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .ReturnsAsync((object?)null);

        var workerMethods = new WorkerMethods(mockRpc.Object);
        var simpleCode = "1 + 1";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(simpleCode));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void RunScript_WithConsoleLog_CallsHostLogRpc()
    {
        // Arrange
        var mockRpc = new Mock<IRpcClient>();
        var logCalls = new List<string>();

        mockRpc
            .Setup(r => r.InvokeAsync<object?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .Callback((string method, object?[]? args) =>
            {
                if (method == "Host.Log" && args?.Length > 0)
                {
                    logCalls.Add(args[0]?.ToString() ?? "");
                }
            })
            .ReturnsAsync((object?)null);

        var workerMethods = new WorkerMethods(mockRpc.Object);
        var codeWithLog = "console.log('test message')";

        // Act
        workerMethods.RunScript(codeWithLog);

        // Assert
        Assert.NotEmpty(logCalls);
        Assert.Contains("test message", logCalls[0]);
    }

    [Fact]
    public void RunScript_WithEmptyCode_ExecutesWithoutError()
    {
        // Arrange
        var mockRpc = new Mock<IRpcClient>();
        mockRpc
            .Setup(r => r.InvokeAsync<object?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .ReturnsAsync((object?)null);

        var workerMethods = new WorkerMethods(mockRpc.Object);

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(""));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void RunScript_WithMultipleConsoleLogStatements_CallsHostLogMultipleTimes()
    {
        // Arrange
        var mockRpc = new Mock<IRpcClient>();
        var logCalls = new List<string>();

        mockRpc
            .Setup(r => r.InvokeAsync<object?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .Callback((string method, object?[]? args) =>
            {
                if (method == "Host.Log" && args?.Length > 0)
                {
                    logCalls.Add(args[0]?.ToString() ?? "");
                }
            })
            .ReturnsAsync((object?)null);

        var workerMethods = new WorkerMethods(mockRpc.Object);
        var codeWithMultipleLogs = @"
console.log('first');
console.log('second');
console.log('third');
";

        // Act
        workerMethods.RunScript(codeWithMultipleLogs);

        // Assert
        Assert.Equal(3, logCalls.Count);
        Assert.Contains("first", logCalls[0]);
        Assert.Contains("second", logCalls[1]);
        Assert.Contains("third", logCalls[2]);
    }

    [Fact]
    public void RunScript_WithJavaScriptArrayOperations_ExecutesSuccessfully()
    {
        // Arrange
        var mockRpc = new Mock<IRpcClient>();
        var logCalls = new List<string>();

        mockRpc
            .Setup(r => r.InvokeAsync<object?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .Callback((string method, object?[]? args) =>
            {
                if (method == "Host.Log" && args?.Length > 0)
                {
                    logCalls.Add(args[0]?.ToString() ?? "");
                }
            })
            .ReturnsAsync((object?)null);

        var workerMethods = new WorkerMethods(mockRpc.Object);
        var complexCode = @"
let arr = [1, 2, 3, 4, 5];
let sum = arr.reduce((a, b) => a + b, 0);
console.log(sum);
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(complexCode));

        // Assert
        Assert.Null(exception);
        Assert.NotEmpty(logCalls);
    }

    [Fact]
    public void RunScript_WithLoopAndConditionals_ExecutesSuccessfully()
    {
        // Arrange
        var mockRpc = new Mock<IRpcClient>();
        var logCalls = new List<string>();

        mockRpc
            .Setup(r => r.InvokeAsync<object?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .Callback((string method, object?[]? args) =>
            {
                if (method == "Host.Log" && args?.Length > 0)
                {
                    logCalls.Add(args[0]?.ToString() ?? "");
                }
            })
            .ReturnsAsync((object?)null);

        var workerMethods = new WorkerMethods(mockRpc.Object);
        var codeWithLoop = @"
for (let i = 0; i < 3; i++) {
    console.log('iteration: ' + i);
}
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(codeWithLoop));

        // Assert
        Assert.Null(exception);
        Assert.Equal(3, logCalls.Count);
    }

    [Fact]
    public void RunScript_WithInvalidJavaScript_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockRpc = new Mock<IRpcClient>();
        mockRpc
            .Setup(r => r.InvokeAsync<object?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .ReturnsAsync((object?)null);

        var workerMethods = new WorkerMethods(mockRpc.Object);
        var invalidCode = "{{invalid js code";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(invalidCode));

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void RunScript_WithLargeJavaScriptCode_HandlesMemoryCorrectly()
    {
        // Arrange
        var mockRpc = new Mock<IRpcClient>();
        mockRpc
            .Setup(r => r.InvokeAsync<object?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .ReturnsAsync((object?)null);

        var workerMethods = new WorkerMethods(mockRpc.Object);
        
        // Create code with a reasonably large string
        var largeCode = @"
let str = '" + new string('x', 500) + @"';
console.log(str.length);
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(largeCode));

        // Assert
        Assert.Null(exception);
    }
}
