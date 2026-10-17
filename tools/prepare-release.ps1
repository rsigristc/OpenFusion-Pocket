param(
    [string]$Apk = 'Builds/OpenFusion-release.apk',
    [string]$Output = 'Builds/release'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $projectRoot 'Assets/Scripts/OpenFusion/Api/PocketRelease.cs') -Raw
$version = [regex]::Match($source, 'const string AppVersion = "([0-9.]+)"').Groups[1].Value
$codeText = [regex]::Match($source, 'const int BuildCode = (\d+)').Groups[1].Value
if (-not $version -or -not $codeText) { throw 'Cannot read release version and build code.' }
$apkPath = if ([IO.Path]::IsPathRooted($Apk)) { $Apk } else { Join-Path $projectRoot $Apk }
$outPath = if ([IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $projectRoot $Output }
if (-not (Test-Path -LiteralPath $apkPath -PathType Leaf)) { throw 'Build the APK before preparing release assets.' }
New-Item -ItemType Directory -Force -Path $outPath | Out-Null
$asset = "OpenFusion-Pocket-v$version.apk"
$target = Join-Path $outPath $asset
if ([IO.Path]::GetFullPath($apkPath) -ne [IO.Path]::GetFullPath($target)) { Copy-Item -LiteralPath $apkPath -Destination $target -Force }
$hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
    schema = 1
    version = $version
    version_code = [int]$codeText
    package = 'com.openfusion.android'
    apk_asset = $asset
    size = (Get-Item -LiteralPath $target).Length
    sha256 = $hash
}
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $outPath 'update.json'), (($manifest | ConvertTo-Json) + "`n"), $utf8)
$manifestHash = (Get-FileHash -LiteralPath (Join-Path $outPath 'update.json') -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $outPath 'SHA256SUMS.txt'), "$hash  $asset`n$manifestHash  update.json`n", $utf8)
Write-Output "Prepared version $version, Android build $codeText in $outPath"
Write-Output "SHA-256: $hash"
