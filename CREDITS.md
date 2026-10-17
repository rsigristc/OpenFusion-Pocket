# Credits

## Project direction and Android adaptation

**Rodrigo Sigrist / [rsigristc](https://github.com/rsigristc)**: project direction, integration, Android device testing, issue reproduction and release preparation for OpenFusion:Pocket.

**AI assistance**: OpenAI Codex was used substantially for legacy-client analysis, format readers, implementation, debugging and documentation. Human direction and device testing remain part of the development process. This acknowledgement does not imply endorsement or verification by OpenAI.

## FusionFall and its creators

Credit belongs to the original FusionFall developers, artists, animators, designers, composers, performers and other contributors for the original game and its content. Cartoon Network, the original developers and other respective rights holders retain their rights in the game, characters, artwork, music, font data and trademarks.

The client behavior, formats and data studied during reconstruction are part of that legacy. This project does not claim authorship of the original game or ownership of its resources.

## OpenFusion and preservation work

- **[OpenFusion](https://github.com/OpenFusionProject/OpenFusion)** and its contributors: open-source server implementation, protocol research and community preservation work.
- **[OpenFusionLauncher](https://github.com/OpenFusionProject/OpenFusionLauncher)** and its contributors: launcher/server/version conventions and built-in version names used by this client. `Assets/Scripts/OpenFusion/Api/KnownGameVersions.cs` retains the upstream MIT notice, copyright 2020–2025 OpenFusion Contributors.
- **[FusionFall Retrobution](https://www.ffretrobution.net/)** and its team: the community server and game-content updates used as the primary compatibility and testing target. Server services, accounts and resource hosting are operated independently of this project.
- Community testers, researchers and preservation contributors who have kept FusionFall accessible and documented its behavior.

## Tools and dependencies

- **[Unity](https://unity.com/)** and Universal Render Pipeline: the current engine and rendering foundation. Unity packages retain their own licenses.
- **[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)**: JSON handling, used through Unity's package. MIT license; copyright James Newton-King and contributors.
- **Igor Pavlov / [LZMA SDK](https://www.7-zip.org/sdk.html)**: the public-domain algorithm on which the compact legacy-container decoder is based. This is not a claim that all of 7-Zip is public domain.
- Android SDK, NDK and OpenJDK: build tooling, subject to their respective licenses.

The earlier Winlator-based [OpenFusion Android](https://github.com/rsigristc/OpenFusion_Android) project has separate provenance and licensing. OpenFusion:Pocket uses a native Unity client rather than that Windows compatibility runtime.

## Resource attribution

Embedded interface images, icons, portraits, launcher art, help images, sky resources, bitmap-font atlases and extracted UI data under `Assets/Resources/OpenFusion/` originate from legacy game/client resources or the relevant community client. Their presence does not transfer ownership or place them under this project's MIT license.

Downloaded game bundles are not committed to this repository. They remain subject to their original rights and the selected server's terms. Please preserve all existing notices when redistributing or contributing.
