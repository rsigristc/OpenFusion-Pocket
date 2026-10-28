param(
    [Parameter(Mandatory=$true)][string]$Device,
    [string]$Sdk = "$env:LOCALAPPDATA/Android/Sdk",
    [string]$Jdk = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK',
    [string]$DebugKeystore = "$env:USERPROFILE/.android/debug.keystore",
    [int]$AdbPort = 5038,
    [switch]$RequireDiagnosticsReport
)
# Installs a separate, debug-signed instrumentation APK. Running it restarts the
# target app process: run before logging in. The target APK must share this signer.
$ErrorActionPreference = 'Stop'
$panelProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$panelOutput = Join-Path $panelProject 'Logs/android-panel'
$panelClasses = Join-Path $panelOutput 'classes'
$panelDex = Join-Path $panelOutput 'dex'
New-Item -ItemType Directory -Force $panelOutput,$panelClasses,$panelDex | Out-Null
$panelBuildTools = Join-Path $Sdk 'build-tools/35.0.0'
$panelAndroid = Join-Path $Sdk 'platforms/android-35/android.jar'
$panelJava = Join-Path $Jdk 'bin/java.exe'
$panelJar = Join-Path $Jdk 'bin/jar.exe'
$panelUnsigned = Join-Path $panelOutput 'checks-unsigned.apk'
$panelSigned = Join-Path $panelOutput 'checks.apk'
& "$Jdk/bin/javac.exe" -source 8 -target 8 -cp $panelAndroid -d $panelClasses "$panelProject/tests/android-panel/PanelSmoke.java"
if ($LASTEXITCODE) { throw 'Panel smoke compilation failed' }
& $panelJar cf "$panelOutput/classes.jar" -C $panelClasses .
if ($LASTEXITCODE) { throw 'Panel smoke JAR failed' }
& $panelJava -cp "$panelBuildTools/lib/d8.jar" com.android.tools.r8.D8 --min-api 23 --lib $panelAndroid --output $panelDex "$panelOutput/classes.jar"
if ($LASTEXITCODE) { throw 'Panel smoke DEX failed' }
& "$panelBuildTools/aapt.exe" package -f -M "$panelProject/tests/android-panel/AndroidManifest.xml" -I $panelAndroid -F $panelUnsigned
if ($LASTEXITCODE) { throw 'Panel smoke packaging failed' }
& python -c 'import sys,zipfile; z=zipfile.ZipFile(sys.argv[1],"a",zipfile.ZIP_DEFLATED); z.write(sys.argv[2],"classes.dex"); z.close()' $panelUnsigned "$panelDex/classes.dex"
if ($LASTEXITCODE) { throw 'Panel smoke APK assembly failed' }
& $panelJava -jar "$panelBuildTools/lib/apksigner.jar" sign --ks $DebugKeystore --ks-key-alias androiddebugkey --ks-pass pass:android --key-pass pass:android --out $panelSigned $panelUnsigned
if ($LASTEXITCODE) { throw 'Panel smoke debug signing failed' }
$panelAdb = Join-Path $Sdk 'platform-tools/adb.exe'
& $panelAdb -P $AdbPort -s $Device install -r -t $panelSigned
if ($LASTEXITCODE) { throw 'Panel smoke installation failed' }
$panelResult = & $panelAdb -P $AdbPort -s $Device shell am instrument -w -e requireReport $RequireDiagnosticsReport.IsPresent.ToString().ToLowerInvariant() com.openfusion.pocket.tests/com.openfusion.pocket.tests.PanelSmoke
$panelResult | Set-Content -Encoding utf8 (Join-Path $panelOutput 'result.txt')
$panelResult
if ($LASTEXITCODE -or ($panelResult -join "`n") -notmatch 'PASS: real Companion constructor') { throw 'Native Companion smoke check failed; see Logs/android-panel/result.txt' }
