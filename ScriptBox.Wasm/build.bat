@echo off
REM QuickJS WASM Module Build Script for Windows
REM Compiles QuickJS + scriptbox_wrapper.c to WebAssembly

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set BUILD_DIR=%SCRIPT_DIR%build
set OUTPUT=%SCRIPT_DIR%scriptbox.wasm

echo.
echo ==========================================
echo QuickJS WASM Module Builder
echo ==========================================
echo.

REM Configuration
REM Try multiple possible SDK locations in order of preference
if not defined WASI_SDK_PATH (
    if exist "%USERPROFILE%\Downloads\wasi-sdk-28.0" (
        set WASI_SDK_PATH=%USERPROFILE%\Downloads\wasi-sdk-28.0
    ) else if exist "%USERPROFILE%\wasi-sdk-28.0" (
        set WASI_SDK_PATH=%USERPROFILE%\wasi-sdk-28.0
    ) else if exist "%USERPROFILE%\wasi-sdk-20.0" (
        set WASI_SDK_PATH=%USERPROFILE%\wasi-sdk-20.0
    )
)

set QUICKJS_REPO=https://github.com/bellard/quickjs.git
set QUICKJS_DIR=%SCRIPT_DIR%quickjs

REM Check for WASI SDK
if not exist "%WASI_SDK_PATH%" (
    echo [X] WASI SDK not found at: %WASI_SDK_PATH%
    echo.
    echo Please download WASI SDK 28 from:
    echo   https://github.com/WebAssembly/wasi-sdk/releases/tag/v28.0
    echo.
    echo For Windows x86-64, download:
    echo   wasi-sdk-28.0-x86_64-windows.zip
    echo.
    echo Then extract and set WASI_SDK_PATH:
    echo   set WASI_SDK_PATH=C:\path\to\wasi-sdk-28.0
    echo.
    exit /b 1
)

set CLANG=%WASI_SDK_PATH%\bin\clang.exe
if not exist "%CLANG%" (
    echo [X] clang not found at: %CLANG%
    exit /b 1
)

echo [+] WASI SDK found at: %WASI_SDK_PATH%
echo [+] Using clang: %CLANG%

REM Clone QuickJS if needed
if not exist "%QUICKJS_DIR%" (
    echo.
    echo [*] Cloning QuickJS from: %QUICKJS_REPO%
    git clone "%QUICKJS_REPO%" "%QUICKJS_DIR%"
    if errorlevel 1 (
        echo [X] Failed to clone QuickJS
        exit /b 1
    )
) else (
    echo [+] QuickJS source found at: %QUICKJS_DIR%
)

REM Apply manual fixes to QuickJS for WASI compatibility
REM These fixes replace the unreliable patch file with direct edits
echo [*] Applying WASI compatibility fixes...
pushd "%QUICKJS_DIR%"

REM Fix 1: Fix the CONFIG_VERSION string concatenation issue
REM The issue is: fprintf(fp, "QuickJS memory usage -- " CONFIG_VERSION " version, ...
REM This fails because CONFIG_VERSION is a macro and can't be stringified inline
REM Replace with a simpler format string that doesn't try to embed the macro
powershell -NoProfile -Command "& { $content = Get-Content 'quickjs.c' -Raw; $content = $content -replace 'fprintf\(fp, \"QuickJS memory usage -- \"\" CONFIG_VERSION \"\" version,', 'fprintf(fp, \"QuickJS memory usage -- version,'; $content | Set-Content 'quickjs.c' -Encoding UTF8 }" 2>nul

REM Fix 2: Add __wasi__ conditional to js_def_malloc_usable_size
REM This prevents WASI builds from trying to use platform-specific malloc_size functions
powershell -NoProfile -Command "& { $content = Get-Content 'quickjs.c' -Raw; if ($content -notmatch '__wasi__') { $pattern = '(#elif defined\(_WIN32\)\s+return _msize\(\(void \*\)ptr\);)'; $replacement = "`$1`r`n#elif defined(__wasi__)`r`n    return 0;"; $content = $content -replace $pattern, $replacement; $content | Set-Content 'quickjs.c' -Encoding UTF8; Write-Output '[+] Applied manual fixes' } else { Write-Output '[+] Fixes already applied' } }" 2>nul

popd

REM Create build directory
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

echo.
echo [*] Compiling QuickJS + scriptbox_wrapper.c to WASM...
echo.

REM Compile to WASM with minimal optimizations for stable build
REM Use -Wno-error to convert errors to warnings for compatibility
REM NOTE: Using -O0 to diagnose if aggressive size optimization (-Oz) is causing stack overflow
"%CLANG%" ^
    --target=wasm32-wasi ^
    -I "%QUICKJS_DIR%" ^
    -I "%WASI_SDK_PATH%\include" ^
    -O0 ^
    -Wall ^
    -Wno-error=implicit-function-declaration ^
    -Wno-error=format ^
    -Wno-format-pedantic ^
    -mllvm -wasm-enable-sjlj ^
    -D_WASI_EMULATED_MMAN ^
    -D_WASI_EMULATED_SIGNAL ^
    -D_WASI_EMULATED_GETPID ^
    "%QUICKJS_DIR%\quickjs.c" ^
    "%QUICKJS_DIR%\libregexp.c" ^
    "%QUICKJS_DIR%\libunicode.c" ^
    "%QUICKJS_DIR%\cutils.c" ^
    "%QUICKJS_DIR%\dtoa.c" ^
    "%SCRIPT_DIR%scriptbox_wrapper.c" ^
    -o "%OUTPUT%" ^
    -Wl,--export=eval_js ^
    -Wl,--export=quickjs_selftest ^
    -Wl,--export=get_last_error_ptr ^
    -Wl,--export=get_last_error_len ^
    -Wl,--export=get_result_ptr ^
    -Wl,--export=get_result_len ^
    -Wl,--no-entry ^
    -Wl,--strip-all

if errorlevel 1 (
    echo.
    echo [X] Build failed!
    exit /b 1
)

for /f "tokens=*" %%A in ('powershell -Command "(Get-Item '%OUTPUT%').Length"') do set SIZE=%%A
set /a SIZE_KB=%SIZE% / 1024

echo.
echo [+] Build successful!
echo [*] Output: %OUTPUT%
echo [*] Size: %SIZE_KB%KB
echo [*] WASI SDK: %WASI_SDK_PATH%
echo.
echo Next steps:
echo   1. Rebuild ScriptBox: dotnet build ScriptBox\ScriptBox.csproj
echo   2. Run the demo: dotnet run --project ScriptBox.Demo\ScriptBox.Demo.csproj
echo.
