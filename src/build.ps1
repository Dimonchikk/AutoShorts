# Збирає AutoShorts.exe вбудованим компілятором C# з .NET Framework (є в кожній Windows 10/11).
# Запуск: powershell -ExecutionPolicy Bypass -File src\build.ps1
$ErrorActionPreference = 'Stop'
$src  = $PSScriptRoot
$root = Split-Path $src -Parent
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$fw  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path $fw)) { $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) { throw "Не знайдено csc.exe ($csc). Потрібен .NET Framework 4.x." }

$out = Join-Path $dist 'AutoShorts.exe'
$files = Get-ChildItem -Path $src -Filter *.cs | ForEach-Object { $_.FullName }
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll',
          'System.Net.Http.dll','System.Security.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')
$common = @('/nologo','/target:winexe','/optimize+','/platform:anycpu','/codepage:65001','/nowarn:1701,1702,0219') +
          ($refs | ForEach-Object { "/r:$_" }) + @("/out:$out")
$icon = Join-Path $src 'app.ico'
if (Test-Path $icon) { $common += "/win32icon:$icon" }

# WinRT (детектор облич Windows) — якщо щось із цього відсутнє, збираємо без нього
# System.Runtime.WindowsRuntime.dll посилається на збірку «Windows», тому потрібен об'єднаний Windows.winmd з Windows SDK
$union = $null
$kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\UnionMetadata'
if (Test-Path $kits) {
  $union = Get-ChildItem -Path $kits -Recurse -Filter 'Windows.winmd' -ErrorAction SilentlyContinue |
           Where-Object { $_.Directory.Name -ne 'Facade' } | Sort-Object FullName -Descending |
           Select-Object -First 1 -ExpandProperty FullName
}
if (-not $union) { $union = Join-Path $kits 'Windows.winmd (немає Windows SDK)' }
$winrt = @(
  (Join-Path $fw 'System.Runtime.WindowsRuntime.dll'),
  (Join-Path $fw 'System.Runtime.dll'),
  (Join-Path $fw 'System.Threading.Tasks.dll'),
  (Join-Path $fw 'System.Runtime.InteropServices.WindowsRuntime.dll'),
  $union
)
$missing = $winrt | Where-Object { -not (Test-Path $_) }

$built = $false
if (-not $missing) {
  Write-Host 'Збірка з детектором облич Windows (WinRT)...'
  $args1 = $common + @('/define:WINRT') + ($winrt | ForEach-Object { "/r:$_" }) + $files
  & $csc @args1
  if ($LASTEXITCODE -eq 0) { $built = $true }
  else { Write-Warning 'Збірка з WinRT не вдалася, збираю без детектора облич.' }
} else {
  Write-Warning ("WinRT недоступний (немає: " + ($missing -join ', ') + "), збираю без детектора облич.")
}

if (-not $built) {
  $args2 = $common + $files
  & $csc @args2
  if ($LASTEXITCODE -ne 0) { throw "Помилка компіляції ($LASTEXITCODE)" }
}

$size = [math]::Round((Get-Item $out).Length / 1KB)
Write-Host "Готово: $out ($size КБ)"
