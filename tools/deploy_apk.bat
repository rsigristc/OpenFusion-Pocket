@echo off
rem Instala el APK en el movil por ADB inalambrico.  Uso:  deploy_apk.bat [ip:puerto] [ruta.apk]
rem Indica el serial o IP:puerto de tu dispositivo. APK por defecto: Builds\OpenFusion-debug.apk.
setlocal
cd /d "%~dp0.."
set DEV=%~1
if "%DEV%"=="" ( echo Uso: deploy_apk.bat ^<serial-o-IP:puerto^> [ruta.apk] & exit /b 1 )
set APK=%~2
if "%APK%"=="" set APK=Builds\OpenFusion-debug.apk
set ADB=
for %%P in ("%ANDROID_HOME%\platform-tools\adb.exe" "%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe" "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe") do if exist %%P if "%ADB%"=="" set ADB=%%~P
if "%ADB%"=="" ( where adb >nul 2>&1 && set ADB=adb )
if "%ADB%"=="" ( echo No encuentro adb.exe. Instala platform-tools o ajusta la ruta en este .bat. & exit /b 1 )
if not exist "%APK%" ( echo No existe %APK% - compila antes ^(menu OpenFusion ^> 4 Compilar APK^). & exit /b 1 )
echo adb: %ADB%
"%ADB%" connect %DEV%
"%ADB%" -s %DEV% install -r "%APK%"
if errorlevel 1 ( echo FALLO la instalacion. Si dice "unauthorized" acepta la huella RSA en el movil; si "offline" repite adb connect con el puerto actual de Depuracion inalambrica. & exit /b 1 )
"%ADB%" -s %DEV% shell monkey -p com.openfusion.android -c android.intent.category.LAUNCHER 1 >nul 2>&1
echo Instalado y lanzado. Registro en vivo: "%ADB%" -s %DEV% logcat -s Unity
endlocal
