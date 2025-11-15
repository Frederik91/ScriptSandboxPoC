namespace ScriptBox.Net.Tests;

using Xunit;
using Moq;
using ScriptBox.Net.Core.WasmExecution;
using ScriptBox.Net.Services;

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
        var mockExecutor = new Mock<IWasmScriptExecutor>();
        mockExecutor.Setup(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()));

        var workerMethods = new WorkerMethods(mockExecutor.Object);
        var simpleCode = "1 + 1";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(simpleCode));

        // Assert
        Assert.Null(exception);
        mockExecutor.Verify(e => e.ExecuteScript(simpleCode, It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public void RunScript_WithConsoleLog_IsPassedToExecutor()
    {
        // Arrange
        var mockExecutor = new Mock<IWasmScriptExecutor>();
        mockExecutor.Setup(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()));

        var workerMethods = new WorkerMethods(mockExecutor.Object);
        var codeWithLog = "console.log('test message')";

        // Act
        workerMethods.RunScript(codeWithLog);

        // Assert
        mockExecutor.Verify(e => e.ExecuteScript(codeWithLog, It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public void RunScript_WithEmptyCode_ExecutesWithoutError()
    {
        // Arrange
        var mockExecutor = new Mock<IWasmScriptExecutor>();
        mockExecutor.Setup(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()));

        var workerMethods = new WorkerMethods(mockExecutor.Object);

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(""));

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void RunScript_WithMultipleConsoleLogStatements_IsPassedToExecutor()
    {
        // Arrange
        var mockExecutor = new Mock<IWasmScriptExecutor>();
        mockExecutor.Setup(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()));

        var workerMethods = new WorkerMethods(mockExecutor.Object);
        var codeWithMultipleLogs = @"
console.log('first');
console.log('second');
console.log('third');
";

        // Act
        workerMethods.RunScript(codeWithMultipleLogs);

        // Assert
        mockExecutor.Verify(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public void RunScript_WithJavaScriptArrayOperations_IsPassedToExecutor()
    {
        // Arrange
        var mockExecutor = new Mock<IWasmScriptExecutor>();
        mockExecutor.Setup(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()));

        var workerMethods = new WorkerMethods(mockExecutor.Object);
        var complexCode = @"
let arr = [1, 2, 3, 4, 5];
let sum = arr.reduce((a, b) => a + b, 0);
console.log(sum);
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(complexCode));

        // Assert
        Assert.Null(exception);
        mockExecutor.Verify(e => e.ExecuteScript(complexCode, It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public void RunScript_WithLoopAndConditionals_IsPassedToExecutor()
    {
        // Arrange
        var mockExecutor = new Mock<IWasmScriptExecutor>();
        mockExecutor.Setup(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()));

        var workerMethods = new WorkerMethods(mockExecutor.Object);
        var codeWithLoop = @"
for (let i = 0; i < 3; i++) {
    console.log('iteration: ' + i);
}
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(codeWithLoop));

        // Assert
        Assert.Null(exception);
        mockExecutor.Verify(e => e.ExecuteScript(codeWithLoop, It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public void RunScript_WithInvalidJavaScript_ThrowsFromExecutor()
    {
        // Arrange
        var mockExecutor = new Mock<IWasmScriptExecutor>();
        mockExecutor
            .Setup(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()))
            .Throws(new InvalidOperationException("eval_js failed"));

        var workerMethods = new WorkerMethods(mockExecutor.Object);
        var invalidCode = "{{invalid js code";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(invalidCode));

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void RunScript_WithLargeJavaScriptCode_IsPassedToExecutor()
    {
        // Arrange
        var mockExecutor = new Mock<IWasmScriptExecutor>();
        mockExecutor.Setup(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()));

        var workerMethods = new WorkerMethods(mockExecutor.Object);

        // Create code with a reasonably large string
        var largeCode = @"
let str = '" + new string('x', 500) + @"';
console.log(str.length);
";

        // Act
        var exception = Record.Exception(() => workerMethods.RunScript(largeCode));

        // Assert
        Assert.Null(exception);
        mockExecutor.Verify(e => e.ExecuteScript(It.IsAny<string>(), It.IsAny<int?>()), Times.Once);
    }
}
