# Fusion Spawn app icon — v1

An AI-generated interpretation of the Fusion Spawn from FusionFall for the OpenFusion:Pocket Android launcher. It is new artwork, not a texture extracted from the original client. FusionFall and its characters belong to their respective owners; existing project credits remain applicable.

Generated with the built-in image generation tool (not the API/CLI fallback). The generated PNGs are preserved without raster postprocessing. Unity imports/resizes them for the Android icon densities. The flat navy adaptive background is generated deterministically by `Assets/Editor/PocketAppIcon.cs`.

- `FusionSpawnIcon-v1.png`: full square legacy/default/round artwork.
- `FusionSpawnForeground-v1.png`: transparent, inset adaptive foreground with room for Android masks.
- `FusionSpawnBackground-v1.png`: opaque midnight navy adaptive background.

## Generation prompts

Original:

> Create one production-ready square 1024x1024 Android launcher app icon for OpenFusion:Pocket, based closely on the iconic Fusion Spawn enemy from Cartoon Network FusionFall. Subject: a simple round lime-green blob of glowing Fusion Matter with one short hooked/curling dorsal antenna at the top, menacing featureless angular RED eyes and a broad dark jagged serrated mouth. No arms, no legs, no extra horns. Expressive mischievous menace, recognizable original game silhouette, polished cel-shaded cartoon videogame illustration with bold clean dark outlines, restrained highlights and very subtle dimensional shading, highly readable at 48 pixels. Center the complete creature inside the middle 62 percent of the square, including antenna, with generous empty space around it so Android circular/squircle adaptive masks cannot crop it. Backdrop: opaque deep midnight navy with a subtle teal radial glow behind the blob, mostly flat and uncluttered. Palette lime/neon green, deep navy, small red eye accents, faint cyan lighting. Full bleed square canvas; do NOT draw a phone, frame, border, drop shadow outside an icon, rounded corners, text, initials, numbers, logos, or watermark. This is the actual icon artwork, not a mockup or presentation sheet.

Adaptive foreground edit (original PNG supplied as the edit target):

> Edit this image into the transparent foreground layer of an Android adaptive launcher icon. Preserve the exact same lime-green Fusion Spawn character, its curled antenna, red eyes, jagged dark mouth, clean cel-shaded illustration and color design. Remove the navy backdrop and all the surrounding cyan glow completely: genuine alpha transparency, no checkerboard baked in. Scale the COMPLETE creature down so its entire silhouette INCLUDING antenna occupies only the central 54 percent of the square's height, with at least 23 percent transparent empty margin on each edge (width can be narrower). Center the total silhouette both vertically and horizontally, not just the round body. Do not crop, redesign, add text or a border. No shadow, no background. Square image. This deliberately generous padding is required for Android adaptive icon masks.

Generated originals are 1254 × 1254 pixels. Foreground alpha is present; the visible silhouette was inspected before integration. Icon artwork is included in Player Settings for all supported Android densities at build time. No GitHub publication is part of this change.

Validation: all existing build checks and Android compilation passed in `Logs/fusion-spawn-icon-build.log`. Inspected the exported Android foreground PNG and adaptive XML: foreground retains alpha and the background is opaque navy; normal/round icons and both adaptive layers are generated for each density. Local APK: `Builds/OpenFusion-Pocket-controls-preview.apk`, SHA256 `A85726B1DB31AAD21E8F7BA5F1657286B890E0287BDCC4DE87218D67224697FB`. This icon build has not been installed over the user's running game session.
