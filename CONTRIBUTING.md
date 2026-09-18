# Building and testing

Install the .NET SDK version recorded in `global.json`. This branch uses .NET 11
RC1 (`11.0.100-rc.1.26425.128`) for local builds and CI. The SDK selection is
exact so an installed preview or a newer SDK cannot silently change the build.
`allowPrerelease` must be enabled because .NET classifies release candidates as
prerelease SDKs; the version pin selects the RC explicitly.

From the repository root:

```powershell
dotnet restore src/DynamicData.sln
dotnet build src/DynamicData.sln --configuration Release --no-restore
dotnet test --solution src/DynamicData.sln --configuration Release --no-build
```

`DynamicData` is the lean ReactiveUI.Primitives package. `DynamicData.Reactive`
compiles the shared sources with `REACTIVE_SHIM` and preserves the System.Reactive
API conventions. Changes to shared operators must preserve both variants.

Tests use TUnit and Microsoft.Testing.Platform. Use native, awaited TUnit assertions
and virtual time or explicit synchronization for asynchronous behavior. Collect
coverage for the shipping assemblies, including their auto-properties, with:

```powershell
dotnet test --solution src/DynamicData.sln --configuration Release --no-build --coverage --coverage-settings src/coverage.config --coverage-output-format cobertura
```

The coverage configuration selects the two shipping assemblies. It does not
exclude obsolete operators or uncovered source files.

Performance changes should include a benchmark for the affected operation and
regression tests covering observable values, completion, errors, and disposal.
Run the cache snapshot comparison with:

```powershell
dotnet run --project src/DynamicData.Benchmarks --configuration Release -- --filter '*ToCollection*'
```

Public API tests compare generated signatures against the framework-specific files
in `src/DynamicData.Tests/API`. A mismatch writes a `.received.txt` file next to the
reviewed `.verified.txt` baseline. Review the API diff before updating a baseline;
do not accept generated changes just to make the test pass.
