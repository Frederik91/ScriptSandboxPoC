# ScriptBox Semantic Kernel Integration Design Specification

## Overview

This document describes the design for integrating ScriptBox with Semantic Kernel (SK). The integration aligns three goals:

* Reuse the same attributed C# methods for both **Semantic Kernel tools** and **ScriptBox host APIs**.
* Provide the AI with a strongly typed view of the ScriptBox JavaScript environment through a generated **TypeScript declaration file (.d.ts)**.
* Enable the AI to execute JavaScript safely via ScriptBox using only **one Semantic Kernel tool**: `run_js`.

This creates a consistent and unified developer experience, where the C# source of truth defines:

* SK tool surface
* ScriptBox JS host API surface
* AI-facing type definitions

## Components

### 1. ScriptBox.SemanticKernel Package

A new package that acts as the integration layer. Its responsibilities:

* Scan C# types for `[KernelFunction]` methods.
* Register these methods as ScriptBox host APIs.
* Feed API metadata into the TypeScript declaration generator.
* Provide the SK plugin (`ScriptBoxPlugin`) that exposes the `run_js` tool.

This package depends on Semantic Kernel but does not introduce any SK requirements into the core ScriptBox assemblies.

### 2. Host API Discovery (Attribute Scanning)

Any class containing SK methods annotated with `[KernelFunction]` can be registered:

```csharp
scriptBoxBuilder.RegisterSemanticKernelPlugin<MathPlugin>("math");
```

This produces two results:

* The `math` namespace becomes part of the ScriptBox host JS API.
* The descriptor provider records the metadata for `.d.ts` generation.

Reflected metadata includes:

* Namespace name
* Method name
* Parameter names and types
* Return type
* Method and parameter descriptions

### 3. TypeScript Declaration Generator

From the collected descriptors, a `.d.ts` file is generated:

* One interface per API namespace.
* All methods typed with correct parameter and return types.
* Async return values typed as `Promise<T>`.
* Global variables declared for each API namespace.

This file is written as:

```
scriptbox.generated.d.ts
```

Its purpose is to give the AI a trustworthy, strongly typed map of the JS environment.

### 4. ScriptBoxPlugin (Semantic Kernel Plugin)

One SK plugin is exposed to the model:

```csharp
public sealed class ScriptBoxPlugin
{
    [KernelFunction("run_js")]
    public Task<string> RunJavaScriptAsync(string code, string? inputJson = null);
}
```

**Only one tool is provided**: `run_js`.

This ensures:

* The model writes JS code freely.
* The JS code can call any registered host API.
* All host API knowledge comes from the `.d.ts` file, not additional SK tools.

### 5. AI Workflow

The AI agent uses the system like this:

1. Receives the `.d.ts` contents in prompt context.
2. Writes JavaScript using strongly typed ScriptBox APIs.
3. Returns the JS code.
4. Orchestration layer calls `scriptbox.run_js` with the generated code.

This design avoids multiple SK tools and keeps the runtime simple.

## Runtime API Shape

At runtime, the JavaScript sandbox exposes one global object per namespace:

```ts
declare const math: ScriptBox.MathApi;
declare const files: ScriptBox.FilesApi;
```

Each method returns a Promise, matching ScriptBox’s async host call model.

Example usage in AI-generated JavaScript:

```js
const result = await math.add(2, 40);
return JSON.stringify({ total: result });
```

## Responsibilities and Boundaries

### ScriptBox Core

* Executes JS in WASM/QuickJS
* Provides generic host API plumbing
* Exposes descriptors through an internal provider

### ScriptBox.SemanticKernel

* Converts `[KernelFunction]` methods to ScriptBox host APIs
* Generates `.d.ts`
* Exposes SK tool (`run_js`)

### Semantic Kernel Host Application

* Provides `.d.ts` in the AI prompt
* Calls `run_js` after model produces output

## Future Extensions (Out of Scope)

Future enhancements may include:

* Enforcing namespace-level permissions
* Mapping enum types to TS union types
* Generating richer documentation in `.d.ts`
* Supporting validation constraints inferred from attributes

These are explicitly not part of this initial design and will be revisited later.

## Status

Initial specification ready for implementation. Adjustments expected during integration with ScriptBox’s descriptor pipeline.
