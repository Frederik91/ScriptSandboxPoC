@echo off
REM QuickJS WASM Module Build Script for Windows
REM Compiles QuickJS + assistant_wrapper.c to WebAssembly

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set BUILD_DIR=%SCRIPT_DIR%build
set OUTPUT=%SCRIPT_DIR%assistant.wasm

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

REM Apply patches to QuickJS for WASI compatibility
if exist "%SCRIPT_DIR%quickjs.patch" (
    echo [*] Applying WASI compatibility patches...
    pushd "%QUICKJS_DIR%"
    REM Try to reverse any previously applied patch (ignore errors)
    git apply --reverse --check "%SCRIPT_DIR%quickjs.patch" >nul 2>&1
    if not errorlevel 1 (
        git apply --reverse "%SCRIPT_DIR%quickjs.patch" >nul 2>&1
    )
    REM Apply the patch
    git apply "%SCRIPT_DIR%quickjs.patch" >nul 2>&1
    if errorlevel 1 (
        patch -p1 < "%SCRIPT_DIR%quickjs.patch" >nul 2>&1
        if errorlevel 1 (
            echo [*] Patch failed, applying manual fixes via PowerShell...
            REM Use a simple sed-like replacement for both fixes
            powershell -NoProfile -Command "& { $content = Get-Content 'quickjs.c' -Raw; $content = $content -replace 'QuickJS memory usage -- \" CONFIG_VERSION \" version', 'QuickJS memory usage -- version'; if ($content -notmatch '__wasi__') { $pattern = '#elif defined\(_WIN32\)\s*\n\s*return _msize\(\(void \*\)ptr\);'; $replacement = '$0' + [System.Environment]::NewLine + '#elif defined(__wasi__)' + [System.Environment]::NewLine + '    return 0;'; $content = $content -replace $pattern, $replacement; } $content | Set-Content 'quickjs.c' -Encoding UTF8; Write-Output '[+] Applied manual fixes' }"
        )
    )
    popd
)

REM Create build directory
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

echo.
echo [*] Compiling QuickJS + assistant_wrapper.c to WASM...
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
    "%SCRIPT_DIR%assistant_wrapper.c" ^
    -o "%OUTPUT%" ^
    -Wl,--export=eval_js ^
    -Wl,--export=quickjs_selftest ^
    -Wl,--export=get_last_error_ptr ^
    -Wl,--export=get_last_error_len ^
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
echo   1. Rebuild the Worker: dotnet build Worker\Worker.csproj
echo   2. Run the Host: dotnet run --project Host\Host.csproj
echo.
