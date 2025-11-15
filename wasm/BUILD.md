# Building QuickJS WASM Module

This document explains how to build the custom QuickJS WASM module (`assistant.wasm`) that includes the `assistant_wrapper.c` bridge to your C# host.

## Overview

The build process:
1. Downloads or uses local QuickJS source
2. Compiles QuickJS + assistant_wrapper.c to WASM (WebAssembly)
3. Outputs `assistant.wasm` which exports:
   - `memory`: Linear memory for WASM
   - `eval_js(ptr, len)`: Main entry point to evaluate JavaScript
4. Imports from "host" module:
   - `call(inPtr, inLen, outPtr, outCap)`: Generic JSON bridge to C#

## Prerequisites

You need a WASI SDK and clang installed:

### macOS
```bash
# Using Homebrew
brew install llvm
brew install wasmtime  # Optional, for testing

# Or use Clang via Xcode Command Line Tools
xcode-select --install
```

For the WASI SDK, download from:
https://github.com/WebAssembly/wasi-sdk/releases

Extract to a known location (e.g., `~/wasi-sdk-20.0`) and note the path.

### Linux
```bash
apt-get install clang lld
# Download WASI SDK as above
```

### Windows
Use WSL2 with Linux commands above, or:
- Download MinGW or LLVM for Windows
- Download WASI SDK

## Quick Build

### Option 1: Using the build script (Recommended)

```bash
cd wasm
./build.sh
```

The script will:
- Check for WASI SDK
- Download QuickJS if needed
- Compile to `assistant.wasm`

### Option 2: Manual build

```bash
cd wasm

# Set these paths for your system
export WASI_SDK_PATH="$HOME/wasi-sdk-20.0"
export CLANG="${WASI_SDK_PATH}/bin/clang"

# Download QuickJS (if not already present)
if [ ! -d "quickjs" ]; then
  git clone https://github.com/bellard/quickjs.git
  cd quickjs
  # Or use quickjs-ng for newer updates:
  # git clone https://github.com/quickjs-zh/quickjs-ng.git quickjs
fi

# Compile
${CLANG} --target=wasm32-wasi \
  -I quickjs \
  -O2 \
  quickjs/quickjs.c \
  quickjs/libregexp.c \
  quickjs/libunicode.c \
  quickjs/cutils.c \
  assistant_wrapper.c \
  -o assistant.wasm \
  -Wl,--export=eval_js \
  -Wl,--export=memory \
  -Wl,--no-entry
```

## Output

After successful build:
- `assistant.wasm` is created in the `wasm/` directory
- The Worker project will automatically copy it to the output directory during build
- When running the Host, it will use this module

## Troubleshooting

### "clang: command not found"
Make sure you have LLVM installed and in your PATH. For macOS with Homebrew:
```bash
export PATH="$(brew --prefix llvm)/bin:$PATH"
```

### "WASI SDK not found"
Download from https://github.com/WebAssembly/wasi-sdk/releases and update the path in build.sh or your environment variables.

### "quickjs.h: No such file or directory"
Ensure QuickJS source is cloned to `quickjs/` subdirectory or adjust the `-I` include path.

### Module doesn't export `eval_js`
Check the linker flags include `-Wl,--export=eval_js`. The compilation output should show this option was used.

## Testing the Module

To test the generated `assistant.wasm`:

```bash
# Using wasmtime CLI (if installed)
wasmtime quickjs-assistant.wasm

# Or run the full Host/Worker flow
cd ..
dotnet run --project Host/Host.csproj
```

## Module Interface

### Imports (from "host" module)
```c
int host_call(const char* in_ptr, int in_len,
              char* out_ptr, int out_cap);
```
- `in_ptr, in_len`: UTF-8 JSON input (e.g., `{"method":"Log","args":["msg"]}`)
- `out_ptr, out_cap`: Buffer for UTF-8 JSON response (e.g., `{"result":null}`)
- Returns: number of bytes written, or negative error code

### Exports
```c
int eval_js(const char* code_ptr, int len);
```
- Evaluates JavaScript code from WASM memory
- The bootstrap automatically defines `globalThis.assistantApi` with methods that use `host_call`
- Returns: 0 on success, non-zero on error

### Memory Export
```c
memory: LinearMemory
```
- WASM linear memory, accessible from C# via `Memory` object
- C# writes code here, calls `eval_js(ptr, len)`, and reads results

## Next Steps

1. Build the module: `./build.sh` (or manual steps above)
2. Rebuild the Worker: `dotnet build Worker/Worker.csproj`
3. Run Host: `dotnet run --project Host/Host.csproj`

The Host will load the custom WASM module and execute your TypeScript/JavaScript code in the sandbox.
