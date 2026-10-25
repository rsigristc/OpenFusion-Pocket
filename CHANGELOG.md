# Changelog

# OpenFusion:Pocket v2.0

## English — Changes since v1.5

- **Controller navigation:** use the stick/D-pad, A/B and L1/R1 in the launcher and Settings. A cursor is available for existing in-game interfaces.
- **Saved login:** optional encrypted password storage using Android Keystore, with controls to forget saved logins.
- **Touch controls:** layout editor, three profile slots and Classic/Controller presets. Gameplay controls remain hidden in launcher/loading/character-selection screens.
- **Graphics:** optional experimental Vulkan; OpenGL ES remains the default. No specific FPS improvement is promised.
- **Character creation and tutorial:** fixed Retrobution name/creation flow and Quit returning to the launcher. Added the opening cinematic and guided local Tech Square practice, with fixes for tutorial illustrations, Computress notifications, interface restrictions, health recovery and Nano Power.
- **DS Companion:** corrected mission details and added Guide/Player E-Mail folders, unread indicators, message reading and server-backed attachment actions. Compose/Reply reuses the original game interface.
- **Interface fixes:** corrected repeated map/menu toggles and delayed area-name notifications until loading completes.
- **Launcher compatibility:** applicable OpenFusion Launcher 2.3.2 features, including `ofl://server` links, runtime information and shareable diagnostics. Pocket already uses a native ARM64 Unity 2022 runtime on supported devices; no Windows Unity 4 engine is installed.
- **Retrobution 3.0.3 / Halloween 2026:** server-driven seasonal ambience, green rim lighting, sky/audio and updated NPC hats, including Black Hat Top Hats for Warp Frogs and removal of Billy Disguise from the Spawn rotation. Mystery Mech assets are compatible; loot remains server-controlled.
- **App artwork:** Fusion Spawn launcher icon resources for normal, round and adaptive Android icons.

### Install / update

Download **OpenFusion-Pocket-v2.0.apk**, or use the in-app updater. Android 6.0+ / API 23+, ARMv7 and ARM64. Version 2.0 / build 4 keeps the same package and signing certificate as v1.0/v1.5: install over the existing app to retain data and cached resources. Do not uninstall first.

### Scope and remaining checks

Retrobution remains the main testing target. Vulkan and device/display support vary by hardware. Tech Square uses isolated local practice and a condensed cinematic adaptation, not a claim of complete Legacy parity. DS Chat still opens the existing interface. Final Halloween device coverage and live player-mail attachment transactions are incomplete. Icon display in third-party launchers still needs verification; an older icon was reported on AynThor despite the correct resources in the installed APK.

## Español — Cambios desde v1.5

- **Navegación con mando:** stick/cruceta, A/B y L1/R1 en el launcher y Settings. Cursor disponible para las interfaces existentes del juego.
- **Inicio de sesión:** guardado opcional de contraseña cifrada con Android Keystore y opción para olvidar los datos guardados.
- **Controles táctiles:** editor de distribución, tres perfiles y diseños Classic/Controller. Los controles de juego permanecen ocultos en launcher, carga y selección de personajes.
- **Gráficos:** Vulkan experimental opcional; OpenGL ES continúa como predeterminado. No se promete una mejora concreta de FPS.
- **Creación y tutorial:** corregidos el nombre/creación en Retrobution y el regreso al launcher mediante Quit. Agregadas la cinemática inicial y la práctica local guiada de Tech Square, con correcciones de ilustraciones, notificaciones de Computress, restricciones de interfaz, recuperación de vida y Nano Power.
- **DS Companion:** corregidos los detalles de misiones y agregadas las carpetas de correo Guide/Player, indicadores de mensajes sin leer, lectura y acciones de adjuntos validadas por el servidor. Redactar/Responder utiliza la interfaz original.
- **Interfaz:** corregida la apertura/cierre repetido de mapa/menú y retrasado el nombre de zona hasta terminar la carga.
- **Compatibilidad del launcher:** funciones aplicables de OpenFusion Launcher 2.3.2, incluyendo enlaces `ofl://server`, información del motor y diagnóstico compartible. Pocket ya utiliza Unity 2022 nativo ARM64 en dispositivos compatibles; no instala el motor Unity 4 de Windows.
- **Retrobution 3.0.3 / Halloween 2026:** ambiente estacional controlado por el servidor, borde verde, cielo/audio y sombreros actualizados; Top Hat de Black Hat para Warp Frogs y retirada de Billy Disguise de la rotación de Spawns. Recursos Mystery Mech compatibles; las recompensas dependen del servidor.
- **Arte de la app:** recursos del icono Fusion Spawn en formatos Android normal, circular y adaptativo.

### Instalar / actualizar

Descarga **OpenFusion-Pocket-v2.0.apk** o utiliza el actualizador de la app. Android 6.0+ / API 23+, ARMv7 y ARM64. La versión 2.0 / build 4 conserva el paquete y la firma de v1.0/v1.5: instala encima para mantener datos y caché. No desinstales primero.

### Alcance y comprobaciones pendientes

Retrobution sigue siendo el principal servidor de pruebas. Vulkan y el soporte de pantallas dependen del dispositivo. Tech Square utiliza una práctica local aislada y una adaptación resumida de la cinemática; no se afirma equivalencia completa con Legacy. Chat DS continúa abriendo la interfaz existente. La cobertura física final de Halloween y las transacciones reales de adjuntos de jugadores están incompletas. Falta verificar el icono en launchers de terceros: se reportó el icono anterior en AynThor pese a que la APK instalada contiene los recursos correctos.

---

Unofficial community project. Thanks to the original FusionFall developers, OpenFusion, Retrobution and community contributors. Full attribution: [CREDITS](https://github.com/rsigristc/OpenFusion-Pocket/blob/main/CREDITS.md).

[Support Me on Ko-fi](https://ko-fi.com/rodrigosigrist)


## v1.5 — DS Companion

Changes since the published v1.0 (build 2):

- **DS Companion:** optional dual-screen support for compatible physical dual-screen devices and unfolded foldables, with Automatic / Single Screen / Dual Screen preferences. Conventional phones and tablets keep the full single-screen interface. Supported fold/unfold and display transitions retain the game session.
- **Map:** a companion map with original game icons, local/world views, filters, pan, zoom and live position, plus Nano Boost, Attack Boost and Taros counters.
- **Nanos:** an animated equipped-Nano panel and collection, real energy bars and assigned-power icons, tap-to-summon, and a compatible Gumball shortcut. Nano Station remains visible on the main screen while the companion supports server-validated equipment and power changes, including resource requirements and progress bars.
- **Equipment:** character preview with touch rotation, equipped slots, inventory, live stats and guide portrait. Selecting an item opens the existing Legacy-style details/actions on the main screen.
- **Missions:** categorized active/completed lists with real objectives, progress, NPC information and rewards; tracking and confirmed abandonment use the existing journal and server acknowledgements.
- **Gameplay fixes:** corrected combined-item appearance from Croc Pot, restored the Legacy-style death/respawn flow and server-provided respawn location, and improved Nano conditions, cooldowns, skill results and combat-effect handling.
- **Stability and presentation:** improved DS session/input handling, remote-player model recovery, portrait rendering and icon transparency; corrected My Stuff alignment and an idle-grounding loop that caused camera shake.

Chat and E-Mail still open their existing game interfaces; dedicated native companion panels are not included yet. DS depends on usable Android fold/display/touch capabilities and falls back to single screen when support is unavailable. Retrobution remains the main testing target; full device, server, tutorial and mission-playthrough coverage is ongoing.

Android package: `com.openfusion.android`. Version: **1.5**, build code **3**. Install over a matching v1.0 installation to retain app data and cached resources. The GitHub updater uses the release notes, `update.json` and SHA-256-verified APK.

## v1.0 — Native Android client

Initial public native Unity Android release, followed by build 2 adding the GitHub project link, release-note changelog, verified APK downloads and Android update installation. See the [v1.0 release](https://github.com/rsigristc/OpenFusion-Pocket/releases/tag/v1.0) for its original notes.
