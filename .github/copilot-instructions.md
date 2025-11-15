ScriptSandboxPoC is a proof-of-concept for executing untrusted TypeScript/JavaScript inside a QuickJS-in-WASM sandbox while a .NET host exposes a tightly scoped `assistantApi`.

**Project Overview**
- `Host/Program.cs` launches `Worker.dll`, wires up JSON-RPC, and exposes host capabilities (`Host.Log`, `Host.Add`, future methods) to the worker.
- Host pulls compiled scripts from `scripts/dist/*.js` and streams them to the worker over the pipe for execution.
- `Worker/Program.cs` connects to the host pipe, adapts `StreamJsonRpc` behind `IRpcClient`, and delegates all script execution to `WorkerMethods`.
- `WorkerMethods` instantiates `assistant.wasm`, writes the script into WASM memory, calls `eval_js`, and marshals results back over RPC.
- `wasm/assistant_wrapper.c` builds the QuickJS bridge: exports `eval_js` and error buffers, imports the single `host.call` entry point, and defines the JS-side `assistantApi` façade.

**Build & Run Flow**
- Requires `.NET 9 SDK`, `Node.js + npm`, and `WASI SDK v28` (`WASI_SDK_PATH` must point at the SDK; the build script clears macOS quarantine bits).
- From clean clone run in order: `cd wasm && ./build.sh` → `dotnet build Worker/Worker.csproj` → `cd scripts && npm install && npx tsc` → `dotnet run --project Host/Host.csproj` (expect `[script]` console lines).
- `tsconfig.json` targets ES2019 with `module: none` so the compiler emits bare JS that QuickJS can execute without a loader.
- Unit tests in `Worker.Tests/UnitTest1.cs` mock `IRpcClient` to exercise `WorkerMethods` behaviour without hitting WASM/QuickJS; run `dotnet test Worker.Tests/Worker.Tests.csproj` for a fast contract check.

**Key Contracts**
- `WorkerMethods.RunScript` resolves `assistant.wasm` via `ResolveWasmPath`; extend its candidate list instead of hard-coding new absolute paths.
- `WriteStringToMemory` writes source at linear-memory offset `0x2000` with a 1 MB ceiling; adjust the pointer and bounds check together and mirror any layout changes in `assistant_wrapper.c`.
- `eval_js` returns status codes: `0` success, `20/21/23/24/25` bootstrap failures, `22+` QuickJS evaluation errors (details available via `get_last_error_ptr/len` and logged on stderr).
- `HandleHostCall` synchronously dispatches `{method,args}` JSON to `Host.Log`/`Host.Add`; keep the `.GetAwaiter().GetResult()` pattern so calls stay on the WASM thread.
- New host capabilities must be added in three places: `assistant_wrapper.c` (`host.call` dispatch), `HandleHostCall` (C# switch case), and the TypeScript `assistantApi` surface (`scripts/sdk/index.d.ts` / generated bindings).

**Extension Tips**
- Treat `assistantApi` as the single gateway for user scripts; never expose raw WASI or `host.call` directly—wrap capabilities in explicit methods and document them.
- Prefer dependency injection via `IRpcClient` for any new worker services so tests can swap in mocks without spinning up WASM.
- If you choose to reuse QuickJS runtimes for stateful scenarios, define reset semantics and audit for object retention to avoid long-lived memory leaks.
- Scripts execute in a host-free environment; offer helper utilities through `assistantApi` instead of pulling browser globals into the sandbox.

**Security & Lifecycle**
- The sandbox assumes scripts are untrusted: QuickJS runs under WASI with no filesystem or network access, and only vetted RPC methods are reachable via `assistantApi`.
- Current lifecycle is single-run: each `dotnet run --project Host/Host.csproj` loads one script and tears everything down; pooling or multi-tenant scheduling is out of scope for this PoC.
- This project demonstrates execution and host-bridging only—it is not a production permissions system or job scheduler.

**Debugging Notes**
- Build & SDK issues: rerun `wasm/build.sh` to reapply patches and validate the SDK; the script also removes macOS quarantine flags and ad-hoc signs binaries.
- Runtime failures: `[Worker]` logs surface worker-side exceptions, `[script]` logs come from the sandboxed JS `console.*`, and non-zero `eval_js` codes expose error text via `get_last_error_ptr/len`.
- Pipe handshake hiccups usually mean the host could not spawn or connect to the worker; verify `dotnet` points at the same SDK targeting `net9.0` in both projects.
