**Project Overview**
- ScriptSandboxPoC runs TypeScript through a sandboxed QuickJS instance; Host bootstraps Worker via named pipes and JSON-RPC to execute compiled JS.
- `Host/Program.cs` launches `Worker.dll`, exposes `Host.Log`/`Host.Add`, and pushes `scripts/dist/sample-script.js` payloads over RPC.
- `Worker/Program.cs` connects back, wraps `StreamJsonRpc` with `IRpcClient`, and delegates script execution to `WorkerMethods`.
- `wasm/assistant_wrapper.c` builds the WASM bridge around QuickJS, exporting `eval_js` and error buffers while importing a single `host.call` entry point.

**Build & Run Flow**
- Build the QuickJS module first: `cd wasm && ./build.sh`; ensure `WASI_SDK_PATH` points to a v28 SDK (script auto-unquarantines binaries on macOS).
- Build the Worker (copies `assistant.wasm` into `bin/Debug/net9.0`): `dotnet build Worker/Worker.csproj`.
- Compile TypeScript into the `dist/` folder before running Host: `cd scripts && npm install && npx tsc` (tsconfig targets ES2019 with `module: none`).
- Full pipeline check: `dotnet run --project Host/Host.csproj`; Host prints script logs prefixed with `[script]` and worker stderr with `[Worker]`.
- Unit tests live in `Worker.Tests/UnitTest1.cs`; run with `dotnet test Worker.Tests/Worker.Tests.csproj` (uses xUnit + Moq to stub `IRpcClient`).

**Key Contracts**
- `WorkerMethods.RunScript` loads `assistant.wasm` via `ResolveWasmPath`; tests assume the file exists or the method is mocked—avoid hard-coding new paths without updating the resolver list.
- `HandleHostCall` dispatches JSON `{method, args}` messages to `Host.Log`/`Host.Add`; maintain synchronous `.GetAwaiter().GetResult()` calls to stay on the WASM thread.
- `WriteStringToMemory` writes scripts at offset `0x2000` with a 1 MB safety cap; adjust both pointer and bounds check together if you change memory layout.
- The WASM wrapper sets `assistantApi` methods that ultimately call the `host.call` import; adding new host capabilities requires parallel updates in `assistant_wrapper.c` and C# dispatch.
- QuickJS errors bubble back through `get_last_error_ptr/len`; Host currently reads stderr, so keep informative messages in the C layer.

**Extension Tips**
- When adding new RPC methods, extend `HostApi` in `Host/Program.cs`, add a switch case in `HandleHostCall`, and expose JS bindings in `assistant_wrapper.c` (see `wasm/README.md` example).
- Prefer injecting dependencies through `IRpcClient` to keep tests viable; new Worker services should accept the interface rather than using `JsonRpc` directly.
- If you need persistent JS state, reuse QuickJS runtimes in the C layer—current design creates a fresh context per `eval_js` call for isolation.
- Scripts expect browser-free globals; provide helpers through `assistantApi` rather than exposing WASI directly to keep the sandbox minimal.

**Debugging Notes**
- WASM build issues: rerun `./build.sh` for patch reapplication and SDK validation; script removes macOS quarantine flags automatically.
- Runtime failures: check both `[Worker]` stderr (C# exceptions) and WASM error buffer (status codes ≥22 logged via `Console.Error`).
- Pipe handshake problems usually mean Host couldn't launch the Worker—confirm `dotnet` resolves to the same SDK both projects target (`net9.0`).
