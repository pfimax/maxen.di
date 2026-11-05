<#
.SYNOPSIS
  Installs the freshly packed packages into a brand-new project and checks that what ships in them really works.

.DESCRIPTION
  Tests inside this repository reference the projects directly, so they cannot notice a package that forgot to ship the
  analyzer, the code fixes or the tool. This script packs everything into a throwaway feed, creates a consumer that
  knows nothing about the repository, builds it, and checks:
    - the analyzers report MAXEN005 and MAXEN007 for deliberately bad code,
    - the source generator output is used (a [Service] class resolves, with a decorator and an interceptor),
    - the consumer runs and prints what it should,
    - the dotnet tool installs from the feed and can validate, explain and diff the consumer.

  Run from the repository root:  pwsh scripts/verify-packages.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("maxen-verify-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
$feed = Join-Path $work "feed"
$packages = Join-Path $work "packages"
$consumer = Join-Path $work "consumer"
$tools = Join-Path $work "tools"
New-Item -ItemType Directory -Force -Path $feed, $consumer, $tools | Out-Null

function Invoke-Checked([string]$what, [scriptblock]$command) {
    Write-Host "==> $what"
    $output = & $command 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { Write-Host $output; throw "$what failed (exit code $LASTEXITCODE)" }
    return $output
}

function Assert-Contains([string]$text, [string]$expected, [string]$what) {
    if ($text -notmatch [regex]::Escape($expected)) {
        Write-Host $text
        throw "Expected $what to contain '$expected'."
    }
    Write-Host "    ok: $what contains '$expected'"
}

try {
    # A version no cached package can have, so the restore below can only be satisfied by what was just packed.
    $version = "0.0.0-verify." + (Get-Date -Format "HHmmss")
    Invoke-Checked "pack" { dotnet pack "$root/Maxen.DI.slnx" -c $Configuration -o $feed -p:Version=$version -p:ContinuousIntegrationBuild=true } | Out-Null

    $expected = @("Maxen.DI", "Maxen.DI.Tool", "Maxen.DI.Extensions.DependencyInjection", "Maxen.DI.Extensions.AspNetCore")
    foreach ($name in $expected) {
        if (-not (Test-Path (Join-Path $feed "$name.$version.nupkg"))) { throw "Package $name was not produced." }
    }

    Set-Content (Join-Path $consumer "consumer.csproj") @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$Framework</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RestorePackagesPath>$packages</RestorePackagesPath>
    <RestoreSources>$feed;https://api.nuget.org/v3/index.json</RestoreSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Maxen.DI" Version="$version" />
  </ItemGroup>
</Project>
"@

    Set-Content (Join-Path $consumer "Program.cs") @'
using Maxen.DI;
using consumer;                                  // where the generator puts RegisterGeneratedServices (the assembly's name)
using Maxen.DI.Extensibility;
using Maxen.DI.Interception;

var builder = new ContainerBuilder();
builder.RegisterGeneratedServices();
builder.Register<Handle>();                      // a disposable transient...
builder.Register<Repo>().AsScoped();
builder.Register<Cache>().AsSingleton();         // ...and a singleton holding a scoped service
builder.Options.ValidateScopes = false;

using var container = builder.Build();
var leaked = container.Resolve<Handle>();        // ...resolved from the root

using var scope = container.CreateScope();
var shouter = scope.Resolve<IShouter>();
Console.WriteLine("shout=" + shouter.Shout("hi"));
Console.WriteLine("proxy=" + shouter.GetType().Name);

public interface IShouter { string Shout(string text); }

[Service(ServiceType = typeof(IShouter))]
[Intercept(typeof(Exclaim))]
public class Shouter : IShouter { public string Shout(string text) => text; }

[Decorator(typeof(IShouter))]
public class Loud : IShouter
{
    private readonly IShouter _inner;
    public Loud(IShouter inner) => _inner = inner;
    public string Shout(string text) => _inner.Shout(text).ToUpperInvariant();
}

public class Exclaim : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        invocation.Proceed();
        invocation.ReturnValue += "!";
    }
}

public class Handle : IDisposable { public void Dispose() { } }
public class Repo { }
public class Cache { public Cache(Repo repo) { } }

public class ConsumerModule : IContainerModule
{
    public void Configure(ContainerBuilder builder) => builder.RegisterGeneratedServices();
}
'@

    $build = Invoke-Checked "build consumer" { dotnet build $consumer -c $Configuration -p:TreatWarningsAsErrors=false }
    Assert-Contains $build "MAXEN005" "build output (captive dependency analyzer)"
    Assert-Contains $build "MAXEN007" "build output (disposable transient analyzer)"

    $run = Invoke-Checked "run consumer" { dotnet run --project $consumer -c $Configuration --no-build }
    # Decorator wraps the intercepted service: interceptor adds "!", decorator upper-cases.
    Assert-Contains $run "shout=HI!" "program output (decorator and interceptor)"
    Assert-Contains $run "proxy=GeneratedProxy" "program output (compile-time proxy, not the runtime one)"

    # The tool, installed from the same feed.
    Invoke-Checked "install tool" { dotnet tool install Maxen.DI.Tool --tool-path $tools --add-source $feed --version $version } | Out-Null
    $tool = Join-Path $tools "maxen-di"
    $dll = Join-Path $consumer "bin/$Configuration/$Framework/consumer.dll"

    $explain = Invoke-Checked "maxen-di explain" { & $tool explain $dll IShouter }
    Assert-Contains $explain "IShouter" "explain output"
    $diff = Invoke-Checked "maxen-di diff" { & $tool diff $dll $dll }
    Assert-Contains $diff "No differences" "diff output"

    Write-Host ""
    Write-Host "Package verification passed ($version)."
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
