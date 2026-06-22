#Requires -Version 5.1
<#
.SYNOPSIS
    Buduje instalator AnalizatorWiFi z obsluga automatycznych aktualizacji (Velopack).

.DESCRIPTION
    Skrypt publikuje aplikacje dla Windows (win-x64), pakuje ja narzedziem vpk
    i tworzy gotowa strukture plikow do wgrania na serwer aktualizacji:
        https://analizatorwifi.ebtech.pl/soft/

    Struktura wyjsciowa (installer\win\):
        AnalizatorWiFiSetup.exe          -- instalator NSIS (dystrybucja poczatkowa)
        AnalizatorWiFi-X.Y.Z-win.zip     -- pelna paczka aktualizacji
        releases.win.json                -- manifest kanalu aktualizacji

    Pliki z katalogu installer\win\ nalezy skopiowac na serwer:
        /soft/AnalizatorWiFiSetup.exe
        /soft/AnalizatorWiFi-X.Y.Z-win.zip
        /soft/releases.win.json

.PARAMETER Version
    Numer wersji w formacie X.Y.Z (np. 1.2.0). Wymagany.

.PARAMETER SkipVpkInstall
    Pominaj automatyczna instalacje narzedzia vpk (jesli juz zainstalowane).

.EXAMPLE
    .\build-installer.ps1 -Version 1.0.0
    .\build-installer.ps1 -Version 1.2.3 -SkipVpkInstall
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$SkipVpkInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir    = $PSScriptRoot
$Project      = Join-Path $ScriptDir "src\AnalizatorWiFi.UI\AnalizatorWiFi.UI.csproj"
$PublishDir   = Join-Path $ScriptDir "publish\win-x64"
$InstallerDir = Join-Path $ScriptDir "installer\win"
$UpdateUrl    = "https://analizatorwifi.ebtech.pl/soft"

function Write-Step([string]$msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}
function Write-Ok([string]$msg) {
    Write-Host "    OK: $msg" -ForegroundColor Green
}
function Fail([string]$msg) {
    Write-Host "`n    BLAD: $msg" -ForegroundColor Red
    exit 1
}

# -- 1. Sprawdz srodowisko ----------------------------------------------------

Write-Step "Sprawdzanie srodowiska"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { Fail "Nie znaleziono 'dotnet'. Zainstaluj .NET 9 SDK." }

$sdkVer = dotnet --version 2>&1
if ($sdkVer -notmatch "^9\.") {
    Write-Warning "Aktywna wersja SDK: $sdkVer. Zalecany .NET 9."
}
Write-Ok ".NET SDK $sdkVer"

# -- 2. Narzedzie vpk ---------------------------------------------------------

Write-Step "Sprawdzanie narzedzia vpk (Velopack CLI)"

$vpk = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpk) {
    if ($SkipVpkInstall) {
        Fail "Narzedzie 'vpk' nie jest zainstalowane. Uruchom: dotnet tool install -g vpk"
    }
    Write-Host "    Instalowanie vpk jako globalne narzedzie .NET..." -ForegroundColor Yellow
    dotnet tool install -g vpk
    if ($LASTEXITCODE -ne 0) { Fail "Instalacja vpk nie powiodla sie." }
    $env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
    $vpk = Get-Command vpk -ErrorAction SilentlyContinue
    if (-not $vpk) { Fail "vpk zainstalowane, ale niedostepne w PATH. Uruchom skrypt ponownie." }
}
Write-Ok "vpk dostepny: $($vpk.Source)"

# -- 3. Przygotuj katalogi ----------------------------------------------------

Write-Step "Przygotowanie katalogow wyjsciowych"

if (Test-Path $PublishDir)   { Remove-Item $PublishDir   -Recurse -Force }
if (Test-Path $InstallerDir) { Remove-Item $InstallerDir -Recurse -Force }

New-Item -ItemType Directory -Force -Path $PublishDir   | Out-Null
New-Item -ItemType Directory -Force -Path $InstallerDir | Out-Null
Write-Ok "publish\win-x64 i installer\win gotowe"

# -- 4. Restore ---------------------------------------------------------------

Write-Step "Przywracanie pakietow NuGet"
dotnet restore $Project
if ($LASTEXITCODE -ne 0) { Fail "dotnet restore nie powiodlo sie." }
Write-Ok "Pakiety przywrocone"

# -- 5. Publish win-x64 -------------------------------------------------------

Write-Step "Publikowanie aplikacji (win-x64, self-contained)"

# WAZNE: Velopack wymaga osobnych plikow -- nie uzywaj PublishSingleFile=true
dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:Version=$Version `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) { Fail "dotnet publish nie powiodlo sie." }
Write-Ok "Opublikowano do: $PublishDir"

# -- 6. Pakowanie z vpk -------------------------------------------------------

Write-Step "Pakowanie instalatora Velopack (vpk pack)"

$iconPath = Join-Path $ScriptDir "src\AnalizatorWiFi.UI\Assets\app.ico"
$vpkArgs  = @(
    "pack",
    "--packId",      "AnalizatorWiFi",
    "--packVersion", $Version,
    "--packDir",     $PublishDir,
    "--outputDir",   $InstallerDir,
    "--channel",     "win",
    "--packTitle",   "Analizator WiFi",
    "--mainExe",     "AnalizatorWiFi.UI.exe"
)

if (Test-Path $iconPath) {
    $vpkArgs += @("--icon", $iconPath)
}

& vpk @vpkArgs
if ($LASTEXITCODE -ne 0) { Fail "vpk pack nie powiodlo sie." }
Write-Ok "Instalator i paczki aktualizacji gotowe"

# -- 7. Podsumowanie ----------------------------------------------------------

$line = "-" * 64
Write-Host "`n$line" -ForegroundColor DarkGray
Write-Host " Wersja   : $Version" -ForegroundColor White
Write-Host " Katalog  : $InstallerDir\" -ForegroundColor White
Write-Host $line -ForegroundColor DarkGray

$files = Get-ChildItem $InstallerDir | Sort-Object Name
foreach ($f in $files) {
    $size = "{0:N0} KB" -f ($f.Length / 1KB)
    Write-Host ("  {0,-45} {1,10}" -f $f.Name, $size)
}

Write-Host ""
Write-Host " NASTEPNY KROK: wgraj na serwer aktualizacji" -ForegroundColor Yellow
Write-Host "  URL: $UpdateUrl/" -ForegroundColor Yellow
Write-Host "  Pliki z katalogu installer\win\:" -ForegroundColor Yellow
Write-Host "    releases.win.json                  <- zastap poprzedni" -ForegroundColor Gray
Write-Host "    AnalizatorWiFi-$Version-win.zip    <- nowa wersja" -ForegroundColor Gray
Write-Host "    AnalizatorWiFiSetup.exe             <- installer (opcjonalnie)" -ForegroundColor Gray
Write-Host ""
Write-Host " UWAGA: Nie usuwaj starszych paczek .zip z serwera." -ForegroundColor DarkYellow
Write-Host "  Klienci ze starszych wersji potrzebuja ich do delta-update." -ForegroundColor DarkYellow
Write-Host ""
