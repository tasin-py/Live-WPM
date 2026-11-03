# Builds "Live WPM.exe" with the C# compiler that ships with Windows (.NET Framework 4.x).
# No SDK or install needed:  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }

$out = Join-Path $root "dist"
New-Item -ItemType Directory -Force $out | Out-Null
$exe = Join-Path $out "Live WPM.exe"
$ico = Join-Path $root "src\app.ico"
$src = Get-ChildItem (Join-Path $root "src") -Filter *.cs | ForEach-Object { $_.FullName }
$common = @("/nologo", "/target:winexe", "/optimize+", "/platform:anycpu",
            "/r:System.dll", "/r:System.Core.dll", "/r:System.Drawing.dll", "/r:System.Windows.Forms.dll", "/r:System.Web.Extensions.dll")
# Font files in src\fonts (TBJ Neuetra Sans) and images in src\assets are built into the exe.
$assetDir = Join-Path $root "src\assets"
if (Test-Path $assetDir) {
    Get-ChildItem $assetDir -File | ForEach-Object { $common += "/resource:$($_.FullName),$($_.Name)" }
}
$fontDir = Join-Path $root "src\fonts"
if (Test-Path $fontDir) {
    Get-ChildItem $fontDir -Include *.ttf, *.otf -Recurse | ForEach-Object { $common += "/resource:$($_.FullName),$($_.Name)" }
}

# The icon is drawn by the app's own code, so build once without it to generate it.
if (-not (Test-Path $ico)) {
    $tmp = Join-Path $out "iconmaker.exe"
    & $csc @common "/out:$tmp" @src
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    & $tmp --make-icon $ico | Out-Null
    Remove-Item $tmp
}

$manifest = Join-Path $root "src\app.manifest"   # per-monitor DPI awareness (sharp on scaled displays)
& $csc @common "/out:$exe" "/win32icon:$ico" "/win32manifest:$manifest" @src
if ($LASTEXITCODE) { exit $LASTEXITCODE }
Write-Host "Built $exe ($([math]::Round((Get-Item $exe).Length / 1KB)) KB)"
