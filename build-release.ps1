<#
.SYNOPSIS
    Genera los archivos de una versión de Modo Windows en artifacts\release:
    el instalador MSI y un ZIP portable por cada arquitectura.

.PARAMETER Version
    Versión a generar (p. ej. 1.2.0). Por defecto, la <Version> de ModoWindows.csproj.

.EXAMPLE
    .\build-release.ps1
    .\build-release.ps1 -Platform x64, arm64 -Version 1.2.0
#>
param(
    [ValidateSet('x64', 'arm64')]
    [string[]] $Platform = @('x64'),

    [string] $Version
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'ModoWindows.csproj'
$installer = Join-Path $root 'installer\ModoWindows.Installer.wixproj'
$releaseDir = Join-Path $root 'artifacts\release'

if (-not $Version) {
    $Version = (dotnet msbuild $project -getProperty:Version).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $Version) { throw 'No se pudo leer la versión de ModoWindows.csproj' }
}

# MSI solo admite versiones numéricas (x.y.z): un sufijo como "-beta" se quita para el instalador.
$msiVersion = $Version -replace '[-+].*$', ''
if ($msiVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Versión no válida: $Version (se espera x.y.z)" }

Write-Host "Modo Windows $Version" -ForegroundColor Cyan

New-Item -ItemType Directory -Force $releaseDir | Out-Null

foreach ($arch in $Platform) {
    $publishDir = Join-Path $root "artifacts\publish\win-$arch"

    # Se parte de una carpeta vacía para que no se cuelen archivos de publicaciones anteriores.
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

    Write-Host "`n[$arch] Publicando..." -ForegroundColor Cyan
    dotnet publish $project -c Release -r "win-$arch" --self-contained true -p:Platform=$arch -p:Version=$Version -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "Falló la publicación para $arch" }

    # Sin el .pri la interfaz XAML no se encuentra y la app se cierra nada más abrirse.
    if (-not (Test-Path (Join-Path $publishDir 'ModoWindows.pri'))) {
        throw "La publicación para $arch no incluye ModoWindows.pri (¿falta EnableMsixTooling en el .csproj?)"
    }

    Write-Host "`n[$arch] Creando instalador MSI..." -ForegroundColor Cyan
    dotnet build $installer -c Release -p:Platform=$arch -p:ProductVersion=$msiVersion -p:PublishDir=$publishDir -o $releaseDir
    if ($LASTEXITCODE -ne 0) { throw "Falló el instalador para $arch" }

    Write-Host "`n[$arch] Creando ZIP portable..." -ForegroundColor Cyan
    Compress-Archive -Path "$publishDir\*" -DestinationPath (Join-Path $releaseDir "ModoWindows-$Version-$arch-portable.zip") -Force
}

# Solo interesan los .msi y .zip; el resto (símbolos .wixpdb) se elimina.
Get-ChildItem $releaseDir -File | Where-Object { $_.Extension -notin '.msi', '.zip' } | Remove-Item -Force

Write-Host "`nListo:" -ForegroundColor Green
Get-ChildItem $releaseDir -File | ForEach-Object { '  {0}  ({1:N1} MB)' -f $_.Name, ($_.Length / 1MB) }
