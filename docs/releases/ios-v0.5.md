# OpenFusion:Pocket — iOS v0.5 (experimental)

## Español

- Primera versión pública **nativa de iOS**, ARM64 y Metal, sin Husk ni JIT externo.
- Launcher, conexión, selección de personaje y entrada al mundo; controles táctiles en una sola pantalla.
- Contraseñas opcionales en el Keychain del dispositivo y exportación de diagnósticos mediante la hoja de compartir de iOS.
- Comprobación automática de actualizaciones de iOS con changelog de GitHub, validación de versión/bundle, tamaño y SHA-256 de la IPA. La descarga se vuelve a verificar antes de compartirla.
- Fuente de **AltStore Classic** para detectar futuras versiones. La firma e instalación se realizan mediante AltStore o la herramienta elegida por el usuario; no hay instalación silenciosa.
- Limpieza en los límites de carga y menos buffers temporales al leer assets. Incluye las correcciones de Nanos, viaje al pasado y accesos de boost/zoom descritas en Android v3.1.

**Instalación:** iOS 16.0+, dispositivo ARM64. Descarga **OpenFusion-Pocket-iOS-v0.5.ipa** y fírmala con AltStore u otra herramienta compatible. Versión 0.5 / build 8. [Guía y fuente AltStore](https://github.com/rsigristc/OpenFusion-Pocket/blob/main/docs/IOS_INSTALL.md).

**Estado experimental:** una compilación nativa anterior entró al mundo en un iPhone SE de tercera generación con iOS 18.5. También hubo un cierre inicial sin causa confirmada y una advertencia de memoria; la validación repetida de carga y la hoja de compartir del actualizador siguen pendientes. No incluye DS Companion para iOS ni se garantiza estabilidad, rendimiento o equivalencia completa con Android/Legacy. Las previews privadas identificadas como 3.0 deben actualizarse manualmente a esta nueva numeración 0.5.

## English

- First public **native iOS** build, using ARM64 and Metal without Husk or external JIT.
- Launcher, connection, character selection and world entry; single-screen touch controls.
- Optional device-local Keychain password storage and diagnostic export through the iOS share sheet.
- Automatic iOS update checks with GitHub changelogs and IPA version/bundle, size and SHA-256 validation. Downloads are rechecked before sharing.
- **AltStore Classic source** for detecting future versions. Signing and installation use AltStore or the user's chosen tool; silent installation is not supported.
- Loading-boundary cleanup and fewer temporary asset-reading buffers. Includes the Nano, past-travel and boost/zoom fixes listed for Android v3.1.

**Installation:** iOS 16.0+, ARM64 device. Download **OpenFusion-Pocket-iOS-v0.5.ipa** and sign it with AltStore or another compatible tool. Version 0.5 / build 8. [Installation and AltStore source](https://github.com/rsigristc/OpenFusion-Pocket/blob/main/docs/IOS_INSTALL.md).

**Experimental status:** an earlier native build entered the world on an iPhone SE (3rd generation), iOS 18.5. An initial unexplained close and a memory warning also occurred; repeated-load validation and the updater's file-sharing path remain pending. No iOS DS Companion support or guarantee of stability, performance or full Android/Legacy parity. Private previews labeled 3.0 need a manual update to the new 0.5 version series.

---
Unofficial community project. Thanks to the original FusionFall developers, OpenFusion, Retrobution and community contributors. [Credits](https://github.com/rsigristc/OpenFusion-Pocket/blob/main/CREDITS.md) · [Support Me on Ko-fi](https://ko-fi.com/rodrigosigrist)
