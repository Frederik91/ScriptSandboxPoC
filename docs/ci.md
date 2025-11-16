## Continuous Integration & Publishing

This repo ships with two GitHub Actions workflows so new contributors immediately understand how their code moves from a PR to a prerelease package.

### `ci.yml` – Build & Test on Every Push

Location: `.github/workflows/ci.yml`

- **Triggers**: every push or pull request targeting `main`.
- **Steps**:
  1. `actions/checkout` fetches the repo.
  2. `actions/setup-dotnet` installs the latest .NET 9 and .NET 10 SDKs (matching Wasmtime's supported TFMs).
  3. `dotnet restore` restores all solution dependencies.
  4. `dotnet build --configuration Release --no-restore` builds every project.
  5. `dotnet test --configuration Release --no-build` runs the entire test suite.
- **Purpose**: provides fast feedback that the solution compiles and tests pass before merging. Failing builds will block PRs.

### `prerelease.yml` – Tag-Based Prerelease NuGets

Location: `.github/workflows/prerelease.yml`

- **Triggers**: pushing tags that match `v*-alpha.*`, `v*-beta.*`, or `v*-rc.*` (e.g., `v0.2.0-beta.1`).
- **Steps**:
  1. Checkout with full history (`fetch-depth: 0`) so future versioning tools work.
  2. Install .NET 9 and .NET 10 SDKs.
  3. Restore all dependencies.
  4. Extract the package version from the tag name (strip the leading `v`).
  5. Pack `ScriptBox/ScriptBox.csproj` with `/p:PackageVersion=<tag>` into `out/`.
  6. Pack `ScriptBox.DependencyInjection/ScriptBox.DependencyInjection.csproj` with the same version into `out/`.
  7. `dotnet nuget push out/*.nupkg --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }}` publishes both packages (core + DI helpers).
- **Secrets**: set `NUGET_API_KEY` (nuget.org API key) under **Settings → Secrets and variables → Actions**.
- **Flow**: merge the desired changes into `main`, then run:
  ```bash
  git tag v0.3.0-beta.1
  git push origin v0.3.0-beta.1
  ```
  Within minutes the prerelease packages appear on nuget.org as `ScriptBox 0.3.0-beta.1` and `ScriptBox.DependencyInjection 0.3.0-beta.1`.

### FAQs

- **How do I publish a stable release?** Create a similar workflow that listens to tags like `v1.0.0` and pushes without the prerelease suffix. For now we only publish prereleases.
- **What if the workflow fails?** Open the Actions tab, inspect the failed job, fix the issue, and push a new tag (e.g., increment the prerelease number).
- **Can I publish to GitHub Packages instead?** Replace the final `dotnet nuget push` step with the GitHub Packages command (instructions in `docs/ci.md`).

This file should give new contributors everything they need to understand our CI/publishing pipeline without reading the workflow YAML inside `.github/workflows/`.
