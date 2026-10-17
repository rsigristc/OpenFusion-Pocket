<#
 Compila / valida el proyecto desde la línea de comandos (Windows, Unity Hub).
 Uso (PowerShell, desde la carpeta del proyecto):
   .\tools\unity-build.ps1 compile     # sólo comprueba que todo el código compila (la primera vez descarga paquetes: minutos)
   .\tools\unity-build.ps1 shaders    # compila los 8 shaders y escribe Logs\openfusion-shader-report.txt
   .\tools\unity-build.ps1 apk        # APK de Android (debug) en Builds\OpenFusion-debug.apk
   .\tools\unity-build.ps1 release    # APK sin símbolos de depuración
 Opciones: -UnityPath "C:\ruta\Unity.exe"  (si no, busca la 2022.3 más reciente en Unity Hub; si no hay, la más reciente)
#>
param(
  [ValidateSet('compile','shaders','apk','release')][string]$Task = 'compile',
  [string]$UnityPath = '',
  [string]$Project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)
$ErrorActionPreference = 'Stop'
if (-not $UnityPath) {
  $roots = @("$env:ProgramFiles\Unity\Hub\Editor", "${env:ProgramFiles(x86)}\Unity\Hub\Editor", "$env:LOCALAPPDATA\Unity\Hub\Editor") | Where-Object { Test-Path $_ }
  $all = foreach ($r in $roots) { Get-ChildItem $r -Directory -ErrorAction SilentlyContinue | Where-Object { Test-Path "$($_.FullName)\Editor\Unity.exe" } }
  $versionFile = Join-Path $Project 'ProjectSettings\ProjectVersion.txt'
  $version = ((Get-Content $versionFile | Select-String '^m_EditorVersion: (.+)$').Matches.Groups[1].Value)
  $pick = $all | Where-Object { $_.Name -eq $version } | Select-Object -First 1
  if (-not $pick) { throw "No encuentro Unity. Instálalo con Unity Hub (2022.3 LTS + Android Build Support) o pasa -UnityPath." }
  $UnityPath = "$($pick.FullName)\Editor\Unity.exe"
}
$method = switch ($Task) { 'compile' { 'OpenFusion.EditorTools.Validation.CompileCheck' } 'shaders' { 'OpenFusion.EditorTools.Validation.Batch' } 'apk' { 'OpenFusion.EditorTools.BuildScript.AndroidDebug' } 'release' { 'OpenFusion.EditorTools.BuildScript.AndroidRelease' } }
New-Item -ItemType Directory -Force -Path (Join-Path $Project 'Logs') | Out-Null
$log = Join-Path $Project "Logs\unity-$Task.log"
Write-Host "Unity: $UnityPath`nProyecto: $Project`nTarea: $Task -> $method`nLog: $log"
$p = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-nographics','-projectPath',"`"$Project`"",'-executeMethod',$method,'-logFile',"`"$log`"") -WindowStyle Hidden -Wait -PassThru
Write-Host "Código de salida: $($p.ExitCode)"
if (Test-Path $log) {
  $lines = Select-String -Path $log -Pattern 'error CS\d+|: error |Error building|OpenFusion:|Scripts have compiler errors|Fatal Error|BuildFailedException' | Select-Object -ExpandProperty Line -Unique
  if ($lines) { Write-Host "`n--- líneas relevantes del log ---"; $lines | Select-Object -First 80 | ForEach-Object { Write-Host $_ } }
}
if ($Task -eq 'shaders' -and (Test-Path (Join-Path $Project 'Logs\openfusion-shader-report.txt'))) { Write-Host "`n--- informe de shaders ---"; Get-Content (Join-Path $Project 'Logs\openfusion-shader-report.txt') }
exit $p.ExitCode
