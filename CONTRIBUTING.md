# Contributing to Maxen.DI

Thanks for helping out. This is how work gets done here.

## Setting up

You need the .NET 11 SDK to build, and the .NET 8, 9, 10 and 11 runtimes to run the tests, because the libraries
target all four. Then:

```bash
dotnet build Maxen.DI.slnx
dotnet test Maxen.DI.slnx
```

A clean build has zero warnings, and we keep it that way. CI builds with `-p:ContinuousIntegrationBuild=true`, which
turns warnings into errors; add it to your own build to see what CI will see.

Packing a library also checks that nothing public was removed or changed since the last release
(`PackageValidationBaselineVersion` in `Directory.Build.targets`). A failure there means the change would break code
compiled against the released version.

## Rules for code

- **One type per file**, and the file is named after the type. Folders group things by concern.
- **Comments explain why**, in plain, conversational language, not what the next line does. If the code needs a
  comment to say *what* it does, rename something instead.
- Every public member gets an XML summary (`<inheritdoc/>` for overrides and interface implementations). The build
  warns about missing ones (CS1591), so a clean build means they are all there. Say what the caller gets, not how it works inside.
- The core library stays trim and AOT friendly. If you add something that needs reflection or dynamic code, mark it
  `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` so users are warned, and keep it out of the default path.

## Tests

- Every behaviour change comes with a test. A bug fix starts with a test that fails.
- Tests live in `tests/Maxen.DI.Tests`; small fixture classes go in `Fixtures/` (also one type per file).
- Anything touching threads should hammer the same code path many times (see `ConcurrencyTests` and
  `ScopeLifecycleTests`), because a race that shows up once in a million runs still ships.
- Source generator and analyzer changes are tested by compiling small snippets (see `GeneratorDiagnosticsTests`,
  `BuilderCallAnalyzerTests`, `GeneratedActivatorTests`).
- Anything that adds a fast path (like resolve plans) must be compared against the normal path: see
  `PlanComparisonTests`. `MAXEN_EXHAUSTIVE=1 dotnet test` runs every combination instead of a sample.
- `pwsh scripts/verify-packages.ps1` installs the packed packages into a clean project; run it when you touch packaging,
  analyzers, the generator or the tool.

## Performance changes

Run the benchmarks before and after, and put the numbers in the pull request:

```bash
dotnet run -c Release --project benchmarks/Maxen.DI.Benchmarks -- --filter "*" --job short --exporters json
pwsh scripts/compare-benchmarks.ps1
```

Short runs are noisy; a change that is not clearly outside the noise is not a win.

`benchmarks/baseline.json` holds the numbers of the CI machine, because that is where the Benchmarks job compares
against it. On your own machine `compare-benchmarks.ps1` therefore tells you how your machine differs from CI's, not
whether your change helped; for that, run the benchmarks before and after your change. When a change is meant to move
the numbers, take the new baseline from the Benchmarks job's output on your pull request, not from a local `-Update`.

## Native AOT

The console sample is published with `PublishAot`. Check it before touching reflection-heavy code:

```bash
dotnet publish samples/Maxen.DI.Samples.Console -c Release -r <rid>
```

On Windows this needs the "Desktop development with C++" Visual Studio workload; on Linux, `clang` and `zlib1g-dev`.
The CI job does the Linux version.

## Docs

The site is built with DocFX from `docs/`, `index.md` and the XML comments:

```bash
dotnet tool install -g docfx
docfx docfx.json        # output goes to _site/
```

## Pull requests

1. Keep a pull request to one idea. Smaller ones get reviewed faster.
2. Add an entry under "Unreleased" in `CHANGELOG.md`.
3. Update `docs/` when something users can see changes, and the READMEs if it affects them: `README.md` is the
   NuGet page of `Maxen.DI`, and the tool and the two extension packages have their own in `src/<project>/README.md`.
4. Make sure the build and tests are clean.

## Releasing (maintainers)

Update `CHANGELOG.md` (rename "Unreleased" to the version) and, once the previous release is on NuGet, set
`PackageValidationBaselineVersion` in `Directory.Build.targets` to it. Then push a tag such as `v1.2.0`. A tag with a suffix, such
as `v1.2.0-preview.1`, is published as a prerelease. The release workflow builds, tests, packs, checks the packages in a clean project (`verify-packages.ps1`) and only then pushes to NuGet. It uses NuGet Trusted Publishing, so no API key is stored; the one-time setup is described at the top of `.github/workflows/release.yml`.
