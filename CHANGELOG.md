# Changelog

# OpenFusion:Pocket v3.0

## Español — Cambios desde v2.1

- **Export Logs:** exporta diagnósticos para reportar errores sin incluir contraseñas, conversaciones ni datos de la cuenta. Nada se sube automáticamente.
- **Movimiento más cercano a Legacy:** salto variable al mantener pulsado el botón y deslizamiento en pendientes demasiado pronunciadas.
- **Nano Dash:** desplazamiento horizontal con detección de obstáculos y sin saltos hacia techos al finalizar.
- **Monkey Skyway:** corregidos el tamaño, la aparición y las animaciones del Monkey durante los viajes.
- **Teletransporte:** corregida la desaparición del personaje después de usar «Go to time machine now».
- **Iluminación y cámara:** mejorada la iluminación del terreno y ajustada la cámara para reducir inestabilidad cerca de estructuras.
- **Nanos y combate:** restaurados los indicadores de afinidad y objetivos de habilidades; corregida la preparación de Drain y las animaciones de armas combinadas según su apariencia.
- **Interfaz y DS Companion:** corregida la apertura del Companion, restaurados los controles de zoom del mapa, añadido zoom de cámara y corregida la información del Nano después de revivir.
- **Correo y funciones sociales:** corregidas las listas de destinatarios entre cuentas, añadido desplazamiento para listas largas y mejorados buddies, grupos y Recall.
- **Assets y avisos:** recuperación de recursos faltantes de NPCs, ajustes de colisiones y cañones y restauración del indicador de inventario lleno.

### Instalar / actualizar

Descarga **OpenFusion-Pocket-v3.0.apk** o utiliza el actualizador de la app. Android 6.0+ / API 23+, ARMv7 y ARM64. La versión **3.0 / build 6** conserva el paquete y la firma: instala encima de la versión anterior para mantener datos y caché. No desinstales primero.

### Validación y pendientes

Se comprobaron en AynThor la iluminación, el salto, Export Logs, el personaje visible tras usar la máquina del tiempo, el deslizamiento en pendientes y el modelo animado de Monkey Skyway. La compilación ejecuta regresiones de juego, movimiento, renderizado, interfaz, DS y actualizador. La auditoría adicional utiliza los esqueletos reales masculino y femenino para probar el montaje del Monkey.

Algunos ajustes requieren más pruebas en distintos dispositivos y zonas, especialmente los cañones, la cámara de City Station, el recorrido de cajas de PCC Fort, Drain con distintos mobs y las interacciones sociales con dos cuentas. Se mantiene la comprobación pendiente de selección inicial de poder de Nano indicada en v2.1. No se afirma equivalencia completa con Legacy.

## English — Changes since v2.1

- **Export Logs:** export diagnostic reports without passwords, conversations or account data. Nothing uploads automatically.
- **Movement closer to Legacy:** variable jump height when holding the jump button, plus sliding on excessively steep slopes.
- **Nano Dash:** horizontal travel with obstacle detection and no snapping onto roofs when the dash ends.
- **Monkey Skyway:** fixed the Monkey's size, visibility and animations during trips.
- **Teleportation:** fixed the player model disappearing after using “Go to time machine now.”
- **Lighting and camera:** improved terrain lighting and adjusted the camera to reduce instability near structures.
- **Nanos and combat:** restored matchup and skill-target indicators; corrected Drain charging and combined-weapon animations to follow the appearance weapon.
- **Interface and DS Companion:** fixed Companion opening, restored map zoom controls, added camera zoom and corrected Nano information after respawning.
- **Email and social features:** fixed recipient lists carrying over between accounts, added scrolling for long lists and improved buddies, groups and Recall.
- **Assets and notifications:** added recovery for missing NPC resources, adjusted collisions and launchers and restored the full-inventory indicator.

### Install / update

Download **OpenFusion-Pocket-v3.0.apk**, or use the in-app updater. Android 6.0+ / API 23+, ARMv7 and ARM64. Version **3.0 / build 6** retains the package and signing identity: install over the previous version to keep app data and cached resources. Do not uninstall first.

### Validation and remaining checks

Lighting, jumping, Export Logs, avatar visibility after time-machine warping, steep-slope sliding and the animated Monkey Skyway model were checked on AynThor. The build runs gameplay, movement, rendering, interface, DS and updater regressions. A separate asset audit tests the Monkey attachment using the real male and female skeletons.

Some adjustments need further testing across devices and locations, especially launchers, the City Station camera, the PCC Fort cardboard route, Drain across different mobs and social interactions with two accounts. The pending live initial Nano-power selection check documented in v2.1 remains. This release does not claim complete Legacy parity.

---

Unofficial community project. Thanks to the original FusionFall developers, OpenFusion, Retrobution and community contributors. Full attribution: [CREDITS](https://github.com/rsigristc/OpenFusion-Pocket/blob/main/CREDITS.md).

[Support Me on Ko-fi](https://ko-fi.com/rodrigosigrist)


# OpenFusion:Pocket v2.1

## English — Changes since v2.0

- Fixed **Practice Pranks** progression when entering KND Training Area and reaching the lair objectives.
- Improved destination detection, enemy/collection counters and transitions between mission objectives, using server-confirmed progress.
- Fixed characters briefly appearing partially buried or popping upward after teleporting onto a platform.
- Fixed **Fusion Numbuh Two's magenta placeholder**: NPCs now recover their models when their resources finish loading, retaining their current health, position and movement state.
- Added **Nano acquisition presentation** using Legacy's original models, animation clips, creation effects, bitmap fonts and power-selection interface.
- Corrected the initial power-selection request and added recovery of unfinished selections when logging in. Already assigned powers are preserved.

### Install / update

Download **OpenFusion-Pocket-v2.1.apk**, or use the in-app updater. Android 6.0+ / API 23+, ARMv7 and ARM64. Version **2.1 / build 5** retains the existing package and signing identity: install over the previous version to keep app data and cached resources. Do not uninstall first.

### Validation and remaining check

Mission progression, teleport placement, boss-model recovery and the Nano presentation were checked on AynThor. Automated regressions cover mission objectives, delayed NPC resources, Nano selection requests and acknowledgements, along with existing gameplay, tutorial, DS and updater checks. **A live initial-power acknowledgement with the corrected request still needs confirmation when the next Nano without an assigned power is acquired.** Existing device/server limitations remain; this release does not claim complete Legacy parity.

## Español — Cambios desde v2.0

- Corregido el avance de **Practice Pranks** al entrar en KND Training Area y alcanzar los objetivos de la guarida.
- Mejoradas la detección de destinos, los contadores de enemigos/objetos y las transiciones entre objetivos, utilizando el progreso confirmado por el servidor.
- Corregida la aparición del personaje parcialmente enterrado o el salto de altura después de usar un teletransportador sobre una plataforma.
- Corregido el **modelo magenta de Fusion Numbuh Two**: los NPC recuperan su modelo cuando terminan de cargar sus recursos, conservando vida, posición y estado de movimiento.
- Añadida la **presentación de obtención de Nanos** con modelos, animaciones, efectos de creación, fuentes e interfaz de selección de poder originales de Legacy.
- Ajustada la petición de selección inicial de poder y añadida la recuperación de elecciones pendientes al iniciar sesión. Se conservan los poderes ya asignados.

### Instalar / actualizar

Descarga **OpenFusion-Pocket-v2.1.apk** o utiliza el actualizador de la app. Android 6.0+ / API 23+, ARMv7 y ARM64. La versión **2.1 / build 5** conserva el paquete y la firma existentes: instala encima de la versión anterior para mantener datos y caché. No desinstales primero.

### Validación y comprobación pendiente

Se comprobaron en AynThor el avance de misiones, la colocación tras teletransportarse, la recuperación del modelo del jefe y la presentación del Nano. Las regresiones automatizadas cubren objetivos de misión, carga tardía de NPC, peticiones y confirmaciones de selección de poder, junto con las comprobaciones existentes de juego, tutorial, DS y actualizador. **Falta confirmar en el servidor real la selección inicial con la petición corregida al obtener el próximo Nano sin habilidad asignada.** Se mantienen las limitaciones existentes de dispositivos y servidores; esta versión no afirma equivalencia completa con Legacy.

---

Unofficial community project. Thanks to the original FusionFall developers, OpenFusion, Retrobution and community contributors. Full attribution: [CREDITS](https://github.com/rsigristc/OpenFusion-Pocket/blob/main/CREDITS.md).

[Support Me on Ko-fi](https://ko-fi.com/rodrigosigrist)


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
