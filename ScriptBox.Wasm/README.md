# WASM Module Development

This directory contains the C wrapper that bridges your C# host with a QuickJS JavaScript runtime, compiled to WebAssembly (WASM).

## Files

- **`scriptbox_wrapper.c`**: The main C bridge code that:
  - Initializes QuickJS runtime and context
  - Exports `eval_js(ptr, len)` entry point
  - Imports `host_call(in_ptr, in_len, out_ptr, out_cap)` from C#
  - Defines `globalThis.scriptbox` with JS bindings to `host_call`
  - Only ~150 lines of C code

- **`build.sh`**: macOS/Linux build script
- **`build.bat`**: Windows build script  
- **`BUILD.md`**: Detailed build instructions

## Quick Start

### 1. Set up the WASI SDK

Download from: https://github.com/WebAssembly/wasi-sdk/releases

Extract and set the path:
```bash
export WASI_SDK_PATH="$HOME/wasi-sdk-20.0"
```

### 2. Build the WASM module

```bash
cd ScriptBox.Wasm
./build.sh
```

Or on Windows:
```cmd
cd ScriptBox.Wasm
build.bat
```

This produces `scriptbox.wasm` (~200-300KB).

### 3. The module is automatically copied to output

When you rebuild the Worker project:
```bash
dotnet build ScriptBox.Net/ScriptBox.Net.csproj
```

It copies `scriptbox.wasm` to the output directory.

### 4. Run the full system

```bash
dotnet run --project ScriptBox.Demo/ScriptBox.Demo.csproj
```

## Architecture

```
┌─────────────────────────────────────────┐
│ TypeScript/JavaScript                   │
│ (scripts/sample-script.ts)              │
└──────────────┬──────────────────────────┘
               │ Compiles to JS
               ▼
┌─────────────────────────────────────────┐
│ Compiled JavaScript                     │
│ (scripts/dist/sample-script.js)         │
└──────────────┬──────────────────────────┘
               │ Sent to Worker
               ▼
┌─────────────────────────────────────────┐
│ Host (C#)                               │
│ Writes JS code to WASM memory           │
│ Calls eval_js(ptr, len)                 │
└──────────────┬──────────────────────────┘
               │ Instantiates WASM module
               ▼
┌─────────────────────────────────────────┐
│ scriptbox.wasm (QuickJS + wrapper)      │
│ - eval_js() parses & executes JS        │
│ - JS calls scriptbox.log/add         │
│ - Which calls host_call()               │
└──────────────┬──────────────────────────┘
               │ JSON RPC calls through
               ▼
┌─────────────────────────────────────────┐
│ Worker (C#)                             │
│ - HandleHostCall() dispatches to C#     │
│ - Calls Host.Log / Host.Add via RPC     │
└──────────────┬──────────────────────────┘
               │ JSON RPC response
               ▼
┌─────────────────────────────────────────┐
│ Host Process                            │
│ Receives result, prints output          │
└─────────────────────────────────────────┘
```

## The Bridge Protocol

JavaScript in WASM calls `__host_call_json(jsonString)`:

**Input (from JS):**
```json
{"method":"Log","args":["Hello from WASM"]}
```

**C# handles it in `HandleHostCall`:**
- Parses method name
- Calls appropriate C# API (Host.Log, Host.Add, etc.)
- Gets result

**Output (back to JS):**
```json
{"result":null}
```

All communication is JSON-based, so adding new APIs is as simple as:
1. Add a new case in `HandleHostCall`
2. Add a new method to the JS bootstrap in `scriptbox_wrapper.c`
3. No additional C glue code needed!

## Debugging

### Build failures
See `BUILD.md` Troubleshooting section.

### Runtime errors
- Check `Host` console output for error messages from `eval_js`
- JavaScript errors are printed (if enabled in scriptbox_wrapper.c)
- WASM memory can be inspected through the C# `Memory` object

### Performance
- The WASM module is sandboxed; only talk to the host via `host_call`
- No filesystem access, no network directly (controlled via C# API)
- Memory is limited to WASM linear memory (default 1GB)

## Extending the API

To add a new method like `scriptbox.divide(a, b)`:

1. **Add C# implementation** in `HandleHostCall`:
   ```csharp
   case "Divide":
       var dividend = args[0].GetDouble();
       var divisor = args[1].GetDouble();
       var result = _rpc.InvokeAsync<double>("Host.Divide", dividend, divisor)
           .GetAwaiter().GetResult();
       return $"{{\"result\":{result}}}";
   ```

2. **Add C bootstrap** in `scriptbox_wrapper.c`:
   ```c
   "  divide: async function(a, b) {                               \n"
   "    const res = JSON.parse(                                    \n"
   "      hostCall(JSON.stringify({ method: 'Divide', args: [a,b] }))\n"
   "    );                                                          \n"
   "    return res.result;                                         \n"
   "  }                                                            \n"
   ```

3. **Rebuild**:
   ```bash
   ./build.sh
   dotnet build ScriptBox.Net/ScriptBox.Net.csproj
   ```

That's it! No additional C functions or glue code needed.

## References

- QuickJS: https://bellard.org/quickjs/
- QuickJS-NG (maintained fork): https://github.com/quickjs-zh/quickjs-ng
- WebAssembly: https://webassembly.org/
- WASI SDK: https://github.com/WebAssembly/wasi-sdk
