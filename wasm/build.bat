@echo off
REM QuickJS WASM Module Build Script for Windows
REM Compiles QuickJS + assistant_wrapper.c to WebAssembly

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set BUILD_DIR=%SCRIPT_DIR%build
set OUTPUT=%SCRIPT_DIR%assistant.wasm

echo.
echo ==========================================
echo QuickJS WASM Module Builder (Windows)
echo ==========================================
echo.

REM Configuration
if not defined WASI_SDK_PATH (
    set WASI_SDK_PATH=%USERPROFILE%\wasi-sdk-20.0
)

if not defined QUICKJS_REPO (
    set QUICKJS_REPO=https://github.com/quickjs-zh/quickjs-ng.git
)

set QUICKJS_DIR=%SCRIPT_DIR%quickjs

REM Check for WASI SDK
if not exist "%WASI_SDK_PATH%" (
    echo Error: WASI SDK not found at: %WASI_SDK_PATH%
    echo.
    echo Please download the WASI SDK from:
    echo   https://github.com/WebAssembly/wasi-sdk/releases
    echo.
    echo Then set WASI_SDK_PATH:
    echo   set WASI_SDK_PATH=%%USERPROFILE%%\wasi-sdk-20.0
    echo.
    exit /b 1
)

set CLANG=%WASI_SDK_PATH%\bin\clang.exe
if not exist "%CLANG%" (
    echo Error: clang not found at: %CLANG%
    exit /b 1
)

echo OK: WASI SDK found at: %WASI_SDK_PATH%
echo OK: Using clang: %CLANG%

REM Clone QuickJS if needed
if not exist "%QUICKJS_DIR%" (
    echo.
    echo Cloning QuickJS from: %QUICKJS_REPO%
    git clone %QUICKJS_REPO% "%QUICKJS_DIR%"
    if errorlevel 1 (
        echo Error: Failed to clone QuickJS
        exit /b 1
    )
) else (
    echo OK: QuickJS source found at: %QUICKJS_DIR%
)

REM Create build directory
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

echo.
echo Compiling QuickJS + assistant_wrapper.c to WASM...
echo.

REM Compile to WASM
"%CLANG%" ^
    --target=wasm32-wasi ^
    -I "%QUICKJS_DIR%" ^
    -I "%WASI_SDK_PATH%\include" ^
    -O2 ^
    -Wall ^
    "%QUICKJS_DIR%\quickjs.c" ^
    "%QUICKJS_DIR%\libregexp.c" ^
    "%QUICKJS_DIR%\libunicode.c" ^
    "%QUICKJS_DIR%\cutils.c" ^
    "%SCRIPT_DIR%assistant_wrapper.c" ^
    -o "%OUTPUT%" ^
    -Wl,--export=eval_js ^
    -Wl,--export=memory ^
    -Wl,--no-entry

if errorlevel 1 (
    echo.
    echo Error: Build failed!
    exit /b 1
)

for /f "tokens=*" %%A in ('powershell -Command "(Get-Item '%OUTPUT%').Length"') do set SIZE=%%A
set /a SIZE_KB=%SIZE% / 1024

echo.
echo OK: Build successful!
echo Output: %OUTPUT%
echo Size: %SIZE_KB%KB
echo.
echo Next steps:
echo   1. Rebuild the Worker: dotnet build Worker\Worker.csproj
echo   2. Run the Host: dotnet run --project Host\Host.csproj
echo.
