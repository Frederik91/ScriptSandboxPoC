# ScriptBox – Product Vision & Architecture

## Why this project exists

Modern AI copilots constantly emit small snippets of JavaScript/TypeScript that need to be executed on behalf of the user. Running that code inside the host process is a non‑starter – it needs isolation, guard rails, and a clear contract describing what the AI is allowed to touch. ScriptBox turns the experimental prototype into a reusable runtime that lets developers:

1. **Execute untrusted TypeScript/JavaScript inside a deterministic QuickJS-in-WASM sandbox** without starting a new process.
2. **Expose their own “ScriptBox API”** – a curated set of file system, HTTP, or domain-specific helpers – while hiding the low-level JSON plumbing required to communicate with the host.
3. **Iterate quickly**: swap bootstrap scripts, develop helper libraries in TypeScript, and keep everything testable from .NET.

## North Star goals

- **Safety first**: single-process isolation, configurable timeouts, filesystem path validation, HTTP allowlists.
- **Extensibility by design**: developers bring their own APIs; we merely provide the transport bridge (`__scriptbox`) and sample ergonomics.
- **Framework agnostic**: ship .NET libraries plus a tiny TypeScript helper so teams can build their own CLI tools, web servers, or background workers on top.

## High-level architecture

```
┌──────────────┐      ┌────────────────┐      ┌────────────────┐
│ .NET Host    │      │ QuickJS + WASM │      │ User Script     │
│ (WasmScript  │◀────▶│ (scriptbox.wasm│◀────▶│ (AI-generated   │
│ Executor)    │host  │ + scriptbox│ API  │  TypeScript)    │
└──────────────┘call  └────────────────┘bridge└────────────────┘
```

- **ScriptBox.Net/Core/WasmExecution** loads `scriptbox.wasm`, inserts bootstrap scripts, and feeds user code via `eval_js`.
- The WASM module exposes a minimal `__host.bridge(requestJson)` entry point. All host APIs are funneled through that synchronous JSON call.
- **`scripts/sdk/scriptbox.js`** is the key developer-facing primitive. It wraps the `__host.bridge` call and exposes:
  - `__scriptbox.hostCall(method, args)` – synchronous host RPC.
  - `__scriptbox.createMethod("Namespace.Method")` – returns a callable that handles argument/response marshalling.
- **Bootstrap scripts** are configurable (`SandboxConfiguration.BootstrapScripts`). By default we ship:
  1. `scripts/sdk/scriptbox.js` – required helper.
  2. `scripts/scriptbox.js` – example consumer API used by tests and docs. It is intentionally small; apps are expected to replace it.

## Defining your own ScriptBox API

The finished product should feel like this for a consuming team:

```ts
// scripts/myScriptBoxApi.ts – written by the app developer
const scriptboxApi = {
  // Hook directly into host methods exposed by IHostApi or custom registries
  files: {
    read: __scriptbox.createMethod('MyFiles.Read'),
    write: __scriptbox.createMethod('MyFiles.Write'),
  },
  http: {
    request(options: RequestOptions) {
      return __scriptbox.hostCall('Http.Request', [JSON.stringify(options)]);
    },
  },
  crm: {
    createLead: __scriptbox.createMethod('Crm.CreateLead'),
  },
};

(globalThis as any).scriptbox = scriptboxApi;
```

On the .NET side the developer either:

1. Implements methods on `IHostApi` (e.g., extends `HostApiImpl`) and routes the JSON payloads manually, **or**
2. Uses `Worker.Core.HostApi.ApiRegistry` to reflect over classes marked with `[HostMethod]` and automatically expose them as `Namespace.Method` host calls. (The registry exists today; wiring it into `WasmScriptExecutor` is the remaining TODO.)

Because bootstrap scripts are configurable, shipping `scripts/myScriptBoxApi.js` is as easy as:

```csharp
var config = new SandboxConfiguration
{
    BootstrapScripts = new List<string>
    {
        Path.Combine("scripts", "sdk", "scriptbox.js"),
        Path.Combine("scripts", "myAssistantApi.js")
    }
};
var executor = new WasmScriptExecutor(config);
```

## Developer workflow

1. **Author your API definitions in TypeScript** using the helper:
   - Use `__scriptbox.createMethod("Namespace.Member")` for simple calls.
   - Use `__scriptbox.hostCall(...)` when you need to preprocess arguments (e.g., serialize objects).
2. **Bundle the bootstrap scripts** (`scriptbox.js` + your API file) into the Worker output. We currently rely on simple file copies; future work includes `npm run build` tasks.
3. **Update the .NET host** to expose matching methods:
   - Extend `HostApiImpl` (or wrap it) for file/HTTP features.
   - Plug in domain services through `ApiRegistry` so you do not need to edit the core runtime.
4. **Write tests** in `ScriptBox.Net.Tests` using `Mock<IHostApi>` to verify the QuickJS stack exercises your APIs correctly.

## Future roadmap (MVP → GA)

| Area | MVP Target | Future Enhancements |
|------|------------|---------------------|
| Bootstrap system | Configurable list (done) | Declarative manifest & hashed cache |
| API ergonomics | `__scriptbox` helper + example API (done) | Turn ApiRegistry into first-class feature with codegen for TypeScript typings |
| Isolation | Timeouts + filesystem sandbox | Fuel-based CPU limits, memory quotas |
| Tooling | Manual docs (this file + README) | `dotnet tool` / `npm` scaffolder to spin up new ScriptBox APIs |
| Observability | Basic logging | Structured tracing, metrics, script replay |

## Takeaways

- The **core deliverable** is the combination of `WasmScriptExecutor` + `scriptbox.wasm` + `__scriptbox`. Everything else (including our sample `scriptbox`) is optional sugar provided for convenience.
- Teams can roll their own JS/TS APIs, inject them via `BootstrapScripts`, and expose any host capability by implementing `IHostApi` or plugging into `ApiRegistry`.
- Documentation, samples, and packaging should push developers toward creating their own ScriptBox APIs while reusing the furnished bridge to stay safe and fast.
