$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$out = Join-Path $root "dist\win-x64"
Write-Host "Publicando PokeHex Local -> $out"

dotnet publish "src\PokeHex.Desktop\PokeHex.Desktop.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeAllContentForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o $out

$exe = Join-Path $out "PokeHexLocal.exe"
$www = Join-Path $out "wwwroot\index.html"
if (-not (Test-Path $exe)) { throw "No se generó PokeHexLocal.exe en $out" }
if (-not (Test-Path $www)) { throw "Falta wwwroot junto al exe (necesario para Photino). Revisa el publish." }

Write-Host ""
Write-Host "Listo: $exe"
Write-Host "Importante: distribuye la carpeta completa (exe + wwwroot), no solo el .exe."
Write-Host "Requisito: WebView2 Runtime (suele venir en Windows 10/11)."
Write-Host "Puedes crear un acceso directo en el Escritorio."
