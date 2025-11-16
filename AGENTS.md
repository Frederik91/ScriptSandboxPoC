# Repository Guidelines

## Project Structure & Module Organization
`ScriptBox.sln` ties together `ScriptBox/` (core runtime + QuickJS bridge), `ScriptBox.DependencyInjection/` (service registration helpers), `ScriptBox.Wasm/` (packaged WASI payload), `ScriptBox.Tests/` (xUnit suite), and `ScriptBox.Demo/` (host sample). Docs sit in `docs/`; WASI installers and JS tooling live in `scripts/`. Keep shared abstractions inside `ScriptBox` and reference them from satellites rather than re-defining contracts.

## Build, Test, and Development Commands
- `dotnet restore ScriptBox.sln` – Primes NuGet caches before other commands.
- `dotnet build ScriptBox.sln -c Release` – Mirrors CI; run before opening a PR.
- `dotnet test ScriptBox.Tests/ScriptBox.Tests.csproj -c Release --collect:"Xplat Code Coverage"` – Executes the suite and emits Coverlet data in `TestResults/`.
- `dotnet run --project ScriptBox.Demo/ScriptBox.Demo.csproj` – Validates host API wiring end to end.
- `dotnet format ScriptBox.sln` – Applies analyzers/code style so reviews focus on behavior.

## Coding Style & Naming Conventions
Use idiomatic C# 12 with 4-space indents, `var` for obvious assignments, nullable reference types enabled, and file-scoped namespaces. Public APIs, sandbox namespaces, and DI services are PascalCase; exported JavaScript method aliases stay snake_case through `[SandboxMethod("...")]` to align with QuickJS expectations. Builder extensions should be fluent, immutable, and return `ScriptBoxBuilder`.

## Testing Guidelines
`ScriptBox.Tests` targets `net9.0` and depends on xUnit, Moq, and Coverlet. Keep test names descriptive (`When_X_Should_Y`) and co-locate fixtures beside the feature under test (e.g., `Sandbox/SessionTests.cs`). Regression fixes require accompanying tests, and touched files should maintain ≥80% coverage per Coverlet output. When exercising QuickJS flows, use deterministic scripts and dispose `IScriptBoxSession` with `await using`.

## Commit & Pull Request Guidelines
Commits follow the `type: summary` format visible in `git log` (`fix: Update WASI SDK installation script`, etc.); flag breaking changes with `feat!` or `refactor!`. PRs should link an issue, list user-visible changes, paste `dotnet test` output when runtime logic changes, and call out new configuration toggles or docs edits. Reviews move faster when the branch builds cleanly, formatting is applied, and screenshots accompany UI/console tweaks.

## Security & Configuration Tips
Expose new host capabilities only via `[SandboxApi]` classes that validate inputs and honour `CancellationToken`/`HostCallContext`. The pinned WASI SDK scripts in `scripts/sdk` include checksum verification—update both the URL and hash when bumping versions. Store secrets such as `NUGET_API_KEY` outside the repo (`dotnet user-secrets` or environment variables) and rely on configuration switches rather than hard-coded guards for experimental APIs.
