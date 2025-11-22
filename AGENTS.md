# ScriptBox Developer Guide for AI Agents

This document is designed to provide LLM agents with a comprehensive understanding of the ScriptBox repository. It covers the project's purpose, architecture, structure, and workflows for development, testing, and contribution.

## 1. Project Overview

**ScriptBox** is a reusable runtime for executing untrusted JavaScript and TypeScript code within a secure, deterministic **QuickJS-in-WASM** sandbox. It is designed for .NET applications, particularly AI copilots and agents, that need to run generated code safely.

**Key Features:**
*   **Isolation:** Runs code in a WebAssembly sandbox (using Wasmtime) with no direct access to the host system.
*   **Interop:** Provides a strongly-typed bridge between C# host APIs and the JavaScript environment.
*   **Semantic Kernel Integration:** Includes a plugin to easily expose the sandbox as a tool to Semantic Kernel agents.
*   **No External Processes:** Runs in-process via WASM, avoiding the overhead and complexity of managing separate Node.js or Python processes.

## 2. Repository Structure

The repository is organized as a Visual Studio solution (`ScriptBox.sln`) containing the following key components:

### Core Components
*   **`ScriptBox/`**: The core library. Contains the `ScriptBox` runtime, `ScriptBoxBuilder`, WASM bridge logic, and attribute-based API discovery (`[SandboxApi]`, `[SandboxMethod]`).
*   **`ScriptBox.DependencyInjection/`**: Extension methods for integrating ScriptBox with `Microsoft.Extensions.DependencyInjection`.
*   **`ScriptBox.SemanticKernel/`**: Integration library for Microsoft Semantic Kernel. Contains `ScriptBoxPlugin` and tools for exposing ScriptBox as an SK function.

### Testing & Examples
*   **`ScriptBox.Tests/`**: Unit and integration tests for the core library. Uses xUnit and Moq.
*   **`ScriptBox.SemanticKernel.Tests/`**: Tests for the Semantic Kernel integration.
*   **`Examples/ScriptBox.Example/`**: A simple console application demonstrating how to configure ScriptBox, register APIs, and run scripts without Semantic Kernel.
*   **`Examples/Scriptbox.SemanticKernel.Example/`**: A complete example showing how to use ScriptBox with Semantic Kernel, including plugin registration and chat completion.

### Supporting Files
*   **`scripts/`**: Contains the TypeScript SDK (`sdk/`) and build scripts for the JavaScript side of the bridge.
*   **`docs/`**: Documentation files (`vision.md`, `ci.md`).
*   **`.github/workflows/`**: CI/CD definitions.

## 3. Architecture & Concepts

Understanding the data flow is crucial for modifying the system:

1.  **Host (C#)**: The .NET application configures a `ScriptBox` instance using `ScriptBoxBuilder`. It registers C# classes as APIs.
2.  **WASM Runtime**: ScriptBox loads a pre-compiled `scriptbox.wasm` module (QuickJS compiled to WASM).
3.  **Initialization**: When a session starts, ScriptBox injects a bootstrap script (`scriptbox.js`) into the WASM runtime. This script sets up the `__scriptbox` global.
4.  **Execution**:
    *   The Host calls `RunAsync(script)`.
    *   The script is written to WASM memory.
    *   QuickJS evaluates the script.
5.  **Interop (RPC)**:
    *   **JS to Host**: The script calls `__scriptbox.hostCall('Namespace.Method', args)`. This triggers a host function import in WASM, which routes the call back to the registered C# method.
    *   **Host to JS**: The Host receives the call, executes the C# method, and returns the result (serialized as JSON) back to WASM.

## 4. Development Workflow

### Prerequisites
*   **.NET 9 SDK**: Required for building the solution.
*   **Node.js & npm**: Required if you need to modify the TypeScript SDK in `scripts/`.

### Build Commands
Run these commands from the repository root:

*   **Restore Dependencies**:
    ```powershell
    dotnet restore ScriptBox.sln
    ```
*   **Build Solution**:
    ```powershell
    dotnet build ScriptBox.sln -c Release
    ```
*   **Run Tests**:
    ```powershell
    dotnet test ScriptBox.Tests/ScriptBox.Tests.csproj -c Release
    ```
    *   *Note: Always run tests after modifying core logic.*

### Running Examples
*   **Basic Example**:
    ```powershell
    dotnet run --project Examples/ScriptBox.Example/ScriptBox.Example.csproj
    ```
*   **Semantic Kernel Example**:
    ```powershell
    dotnet run --project Examples/Scriptbox.SemanticKernel.Example/Scriptbox.SemanticKernel.Example.csproj
    ```
    *   *Note: This example may require configuring an LLM endpoint (e.g., OpenAI/Azure OpenAI) in the code or environment variables.*

## 5. Coding Standards

*   **Language**: C# 12 / .NET 9.
*   **Style**:
    *   Use file-scoped namespaces.
    *   Use `var` when the type is obvious.
    *   Enable nullable reference types.
    *   Public APIs should be PascalCase.
    *   JavaScript method aliases in `[SandboxMethod]` should be `snake_case` to match JS conventions.
*   **Formatting**: Run `dotnet format ScriptBox.sln` to ensure compliance.

## 6. Common Tasks for Agents

### Task: Add a New Host API
1.  Create a C# class (e.g., `MyNewApi`).
2.  Decorate the class with `[SandboxApi("my_namespace")]`.
3.  Decorate public methods with `[SandboxMethod("my_method")]`.
4.  Register it in the builder: `.RegisterApisFrom<MyNewApi>()`.
5.  **Test**: Add a test case in `ScriptBox.Tests` that runs a script calling `my_namespace.my_method()`.

### Task: Fix a Bug in the Runtime
1.  Identify the issue in `ScriptBox/Core/`.
2.  Create a reproduction test case in `ScriptBox.Tests/`.
3.  Apply the fix.
4.  Verify the test passes.

### Task: Update Semantic Kernel Integration
1.  Modify `ScriptBox.SemanticKernel/`.
2.  If changing the plugin interface, update `ScriptBoxPlugin.cs`.
3.  Run `ScriptBox.SemanticKernel.Tests/` to verify.

## 7. Troubleshooting

*   **"WASM module not found"**: Ensure the `ScriptBox.Wasm` package is referenced or the `.wasm` file is being copied to the output directory.
*   **"Method not found" in JS**: Check the `[SandboxApi]` and `[SandboxMethod]` names. They are case-sensitive in the JS bridge.
*   **Serialization Errors**: Ensure arguments and return types are JSON-serializable. Complex objects may need `JsonElement` or specific DTOs.

## 8. Vision & Roadmap
Refer to `docs/vision.md` for the long-term goals. The project aims to be the standard, safe way to run AI-generated code in .NET.
