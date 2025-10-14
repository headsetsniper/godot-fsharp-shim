param(
    [string]$Configuration = "Debug",
    [string]$GodotBin,
    [switch]$SkipBuild,
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$testShimsProj = Join-Path $PSScriptRoot 'FsharpWithShim.TestShims.csproj'

if (-not $GodotBin) { $GodotBin = $env:GODOT_BIN }
if (-not $GodotBin) {
    $defaultCandidate = Join-Path $repoRoot 'Godot' 'godot.exe'
    if (Test-Path $defaultCandidate) { $GodotBin = $defaultCandidate }
}
if (-not $GodotBin -or -not (Test-Path $GodotBin)) {
    Write-Error "Godot executable not found. Provide -GodotBin or set GODOT_BIN environment variable."
}

# Build to ensure F# tests + shims current
if (-not $SkipBuild) {
    Write-Host "[shimgen][tests] Building TestShims project (configuration=$Configuration)" -ForegroundColor Cyan
    dotnet build $testShimsProj -c $Configuration | Write-Host
}

# Locate the compiled shim test assembly
$binDir = Join-Path $PSScriptRoot ".godot/mono/temp/bin/$Configuration"
$testAsm = Join-Path $binDir 'FsharpWithShim.TestShims.dll'
if (-not (Test-Path $testAsm)) {
    Write-Error "Compiled test assembly not found at $testAsm (build may have failed)."
}

# gdUnit4 CLI arguments (headless)
$godotArgs = @('--headless', '--quit', '--audio-driver', 'Dummy', '--rendering-driver', 'opengl3', '--', '-s', 'res://addons/gdUnit4/runners/GdUnit4.dll', '-a')
# Notes:
#  - '--quit' ensures exit after tests.
#  - '-a' : run all suites; filtering could be added later.
#  - To restrict to specific suites, add: '-suites=SuiteName1,SuiteName2'

$projectDir = Split-Path $PSScriptRoot
Push-Location $projectDir
try {
    Write-Host "[shimgen][tests] Running Godot headless: $GodotBin $($godotArgs -join ' ')" -ForegroundColor Yellow
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $GodotBin
    $pinfo.ArgumentList.AddRange($godotArgs)
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.Environment['GODOT_BIN'] = $GodotBin
    # Provide a hint for suite filtering in future; can pass patterns as env
    if ($Verbose) { $pinfo.Environment['GDUNIT_VERBOSE'] = '1' }

    $p = [System.Diagnostics.Process]::Start($pinfo)
    $stdOut = $p.StandardOutput.ReadToEnd()
    $stdErr = $p.StandardError.ReadToEnd()
    $p.WaitForExit()

    Write-Host $stdOut
    if ($stdErr) { Write-Warning $stdErr }

    if ($p.ExitCode -ne 0) {
        Write-Error "Godot test run failed (exit code $($p.ExitCode))"
    }
    else {
        Write-Host "[shimgen][tests] Godot test run succeeded" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
