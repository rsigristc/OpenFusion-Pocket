# iOS installation and updates / Instalación y actualizaciones

## English

Download **OpenFusion-Pocket-iOS-v0.5.ipa** from the [iOS release](https://github.com/rsigristc/OpenFusion-Pocket/releases/tag/ios-v0.5). Requires iOS 16.0+ and an ARM64 iPhone/iPad. The IPA must be signed for your device using AltStore Classic or another compatible sideloading tool. Use your Apple account only in that tool, never in Pocket or a bug report. No Husk or external JIT is needed.

In AltStore Classic, use **My Apps → +** to select the IPA from Files. Install updates over the same app with the same signing account to preserve its data. Developer Mode may be required. Private previews previously labeled 3.0 need this first 0.5 installation manually; subsequent releases use the independent iOS version series.

For future updates, add this source in AltStore's Sources screen:

https://raw.githubusercontent.com/rsigristc/OpenFusion-Pocket/main/altstore.json

Pocket also checks GitHub automatically in the launcher. **Updates** shows the iOS changelog, downloads to private cache and verifies the IPA size/SHA-256. **Share IPA** rechecks the file and opens the system share sheet: save it to Files and import it into your installer. Pocket does not silently install or bypass Apple's signing requirements. AltStore manages its own source downloads and installation; Pocket's hash verification applies to the in-app download path.

This is an experimental single-screen edition, not an App Store/TestFlight release. DS Companion is Android-only. Repeated-load, memory and file-sharing validation remain pending across devices. See [release scope](releases/ios-v0.5.md).

## Español

Descarga la IPA de la [versión iOS v0.5](https://github.com/rsigristc/OpenFusion-Pocket/releases/tag/ios-v0.5). Requiere iOS 16.0+ y un iPhone/iPad ARM64. Fírmala para tu dispositivo con AltStore Classic u otra herramienta compatible; introduce tu cuenta Apple únicamente en esa herramienta. No requiere Husk ni JIT externo.

En AltStore usa **My Apps → +** y selecciona la IPA en Archivos. Actualiza sobre la misma app y con la misma cuenta de firma para conservar datos. Puede requerir Modo de desarrollador. Las previews privadas numeradas 3.0 deben instalar manualmente esta primera versión 0.5.

Agrega en Sources de AltStore la fuente enlazada arriba para detectar futuras versiones. Pocket también comprueba su canal iOS: muestra el changelog y descarga/verifica la IPA. **Share IPA** permite guardarla en Archivos para importarla en AltStore u otro instalador. La firma e instalación requieren la herramienta elegida; no son silenciosas.

La versión es experimental y usa una sola pantalla. DS Companion continúa disponible solo en Android. La validación repetida de carga, memoria y compartir archivos sigue pendiente.

References: [AltStore sources](https://faq.altstore.io/developers/make-a-source), [AltStore updates](https://faq.altstore.io/developers/updating-apps), [Apple device distribution](https://developer.apple.com/documentation/xcode/distributing-your-app-to-registered-devices).
