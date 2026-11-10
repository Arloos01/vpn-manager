Set-Location $PSScriptRoot

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$gacMsil = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL"
$gac64 = "C:\Windows\Microsoft.NET\assembly\GAC_64"

$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Data.dll",
    "System.Xml.dll",
    "Microsoft.CSharp.dll",
    "$gacMsil\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll",
    "$gac64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll",
    "$gacMsil\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll",
    "$gacMsil\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"
)

$refArgs = $refs | ForEach-Object { "/r:$_" }
$sources = Get-ChildItem -Path "$PSScriptRoot\src\*.cs" | ForEach-Object { $_.FullName }

$tempDir = Join-Path $PSScriptRoot "temp"
if (-not (Test-Path $tempDir)) { New-Item -ItemType Directory -Path $tempDir -Force | Out-Null }

$exeName = "VPN-Manager.exe"

# 1. Compilación limpia nativa C#
$iconParam = @()
$icoFile = Join-Path $PSScriptRoot "app.ico"
if (Test-Path $icoFile) { $iconParam = @("/win32icon:$icoFile") }

& $csc /target:winexe "/out:$exeName" @iconParam /codepage:65001 /nologo $refArgs $sources
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error en compilación: código $LASTEXITCODE" -ForegroundColor Red
    exit 1
}

# 2. Firma digital Authenticode oficial
try {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | Where-Object { $_.Subject -like "*Carlos Enrique Barros Castro*" } | Select-Object -First 1
    if ($cert) {
        Set-AuthenticodeSignature -FilePath $exeName -Certificate $cert -ErrorAction SilentlyContinue | Out-Null
        Write-Host "Firma digital Authenticode aplicada: Carlos Enrique Barros Castro." -ForegroundColor Green
    }
} catch { }

# 3. Copia en el Escritorio
$desktopPath = [System.IO.Path]::Combine($env:USERPROFILE, "Desktop", $exeName)
try {
    Copy-Item -Path $exeName -Destination $desktopPath -Force
    Write-Host "Copia en Escritorio actualizada: $desktopPath" -ForegroundColor Green
} catch {
    Write-Host "Aviso: no se pudo copiar al Escritorio: $_" -ForegroundColor Yellow
}

Write-Host "Compilación exitosa: $exeName listo y 100% portable." -ForegroundColor Cyan
