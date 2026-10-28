# Auditoría de sistemas frente a Legacy

Fecha: 25 de octubre de 2026; continuación de buddies/grupos: 26 de octubre. Base: v2.1; cambios locales en `codex/legacy-system-audit`.

## Resultado y alcance

La app todavía no tiene equivalencia completa con Legacy. Esta revisión compara rutas de código, estructuras de paquetes, tablas actuales de Retrobution y referencias de recursos. Incluye correcciones y regresiones ejecutables; no certifica haber recorrido todas las zonas, utilizado todas las armas o comparado visualmente cada efecto.

No se enviaron invitaciones, reportes, mensajes o transacciones a otros jugadores. No se consumieron objetos, se alteraron recompensas del servidor ni se publicaron estos cambios.

## Correcciones realizadas

- **Ignorar jugadores:** se conectaron los filtros de `ChatHud` al estado de bloqueo recibido por `SocialMenu`. Antes existían las funciones de filtrado, pero no estaban enlazadas. Se conserva el UID como identidad y se admite el ID vigente de un buddy sin depender de su modelo cargado cerca del personaje.
- **Grupos:** el éxito de salir es un paquete vacío; el guardado anterior exigía al menos 12 bytes y podía conservar miembros antiguos. Ahora se limpia correctamente y se procesan las actualizaciones periódicas `PC_GROUP_MEMBER_INFO`.
- **Identidad del grupo:** se conserva el UID de los miembros para consultas desde buddies aunque su avatar esté fuera de distancia. El grupo se restablece al entrar en otra sesión.
- **Integridad del roster:** conteos, longitud y duplicados se validan antes de sustituir un grupo válido. Un paquete truncado no deja una lista parcial.
- **Caducidad de efectos:** la máscara de salto era confundida con la de natación. También faltaba retirar la reducción de velocidad cuando caducaba. Se usan las máscaras Legacy: velocidad 1, salto 4 y ralentización 128. Los avisos de otros personajes no modifican el movimiento propio.

Archivos principales: `World/SocialMenu.cs`, `World/GroupState.cs`, `World/NanocomHud.cs`, bajo `Assets/Scripts/OpenFusion/`.

## Menú de jugadores, buddies y comercio

Existen rutas para solicitudes y respuestas de buddy/grupo, bloqueo, consulta de estado, warp de buddies y comercio directo. Las invitaciones tienen rechazo y caducidad; el comercio rechaza estados incompatibles y maneja cancelación/fallo. Esto es cobertura de código, no una prueba de intercambio entre dos cuentas.

La revisión inicial encontró **ADD** deshabilitado. La continuación local lo conecta a un editor de nombre completo y al paquete `FIND_NAME_MAKE_BUDDY` de Legacy. Se validan las capacidades de los campos UTF-16 (8 caracteres de nombre y 16 de apellido, sin contar NUL) antes de enviar; un nombre demasiado largo no se recorta hacia otro jugador. La interfaz Android usa teclado del sistema; el editor bloquea atajos y cierra el chat para evitar enviar el nombre como mensaje.

Las invitaciones por nombre usan su protocolo independiente de aceptación, UID y nombres del paquete, sin depender de avatares cercanos. Se respetan las opciones sociales, bloqueos y los mensajes Legacy de fallo de búsqueda/aceptación. El alias `Player UID` se conserva para nombres no aprobados. Ni la búsqueda ni aceptar crean un buddy local: la lista espera confirmación del servidor.

También se corrigieron duplicados y caducidad de invitaciones de buddy/grupo detrás de otros avisos, limpieza de identidades al entrar en otra sesión, snapshots parciales de buddies y respuestas incompletas de aceptación. El warp de buddies se correlaciona con una solicitud pendiente: evita salidas repetidas del grupo por respuestas duplicadas y conserva el grupo al viajar a un miembro. El destino de chat seleccionado se limpia cuando el buddy seleccionado queda bloqueado.

`Leave Group` ya no modifica una bandera local antes de la respuesta: usa la pertenencia confirmada por `GroupState`, limita envíos repetidos y permite reintentar cuando el servidor rechaza salir. Los fallos de invitar/unirse y el rechazo de invitación muestran los mensajes Legacy 59, 60 y 62; un fallo de salida no borra miembros.

Se necesita una prueba controlada de dos cuentas para validar aceptación/rechazo, desconexión durante invitación, comercio cancelado/confirmado, bloqueo de mensajes privados, teclado Android y cambio de grupo mientras un miembro está fuera de distancia. Las pruebas sintéticas siguientes cubren rutas concretas, no sustituyen ese intercambio real.

### Recorrido pendiente con dos cuentas

1. A y B en el mismo servidor: A abre BUDDY LIST → ADD e introduce el nombre completo de B. B debe recibir una sola invitación aunque A pulse repetidamente.
2. Rechazar y dejar caducar una invitación, también detrás de un aviso informativo. No debe crearse un buddy local ni quedar un bloqueo de controles.
3. Aceptar: ambas listas deben actualizarse sólo tras la confirmación del servidor. Repetir con solicitudes de buddy desactivadas y con el solicitante bloqueado.
4. Cambiar online/offline, salir y volver a entrar con otro personaje. No deben heredarse nombres, UID, selección de mensaje ni invitaciones de la sesión anterior.
5. Probar mensajes privados con bloqueo confirmado y después de desbloquear. No se enviarán mensajes de prueba a cuentas ajenas.
6. A invita a B al grupo: aceptar/rechazar/caducar, alejarse del rango de streaming, salir y reinvitar. Ante un rechazo del servidor al salir, el grupo debe conservarse y el botón admitir reintento.
7. Warp a un miembro del grupo debe conservarlo; warp a otro buddy sigue el flujo de confirmación/salida de Legacy. Contrastar la composición posterior con los paquetes reales.
8. Group Recall: punto confirmado dentro de una instancia, aceptar/rechazar/caducar y cambiar de zona con una invitación pendiente. Comprobar destino, energía, cooldown y recuperación de controles por servidor.
9. Repetir el editor ADD con teclado Android, cancelación, pantalla individual y DS. La fidelidad visual y la navegación física todavía requieren comprobación en dispositivo.

### User Stores

No están portadas. Hay constantes `STREETSTALL` y estado `StreetStore`, pero no constituyen una implementación: faltan las rutas de abrir, registrar objetos, consultar stock, iniciar venta, comprar y reconciliar respuestas/fallos. Las referencias Legacy son `cnStore`, `Panel_UserStore` y `eStoreState`.

La disponibilidad depende además del servidor. Se debe verificar cada confirmación, inventario y moneda del servidor antes de habilitar operaciones. No se añadió una tienda estática ni compras simuladas.

## Muerte, respawn y Use Item

Los tipos de solicitud 1, 3 y 4 tienen rutas de respawn normal, Phoenix y Phoenix de grupo. La interfaz conserva **USE ITEM** deshabilitado y `RespawnRequest` no admite el tipo 6.

Legacy envía tipo 6 con localización de inventario 1 e índice real del objeto de reanimación; su clasificación procede de `GeneralItemTable`, tipo 10. El catálogo actual contiene un ID de esa clase. No es suficiente enviar el tipo 6: debe existir validación y consumo por el servidor.

El servidor público de referencia no tiene una rama completa de consumo de ese objeto en su manejador de reanimación. Esto **no demuestra** el comportamiento del backend privado de Retrobution. Queda pendiente confirmar su soporte antes de habilitar el botón y comprobar consumo único, falta de objeto, rechazo, latencia y restauración de posición/HP. Referencia: [OpenFusion PlayerManager.cpp](https://github.com/OpenFusionProject/OpenFusion/blob/master/src/PlayerManager.cpp).

## Grupos y habilidades de grupo

Las correcciones cubren pertenencia, UID, actualización y salida. La app no demuestra todavía equivalencia del HUD completo de miembros, compañeros NPC, estados, distancias ni todos los efectos de grupo.

Validación pendiente: líder que sale, desconexión, reinvitación, grupo lleno, miembro fuera de streaming, entrada a instancia, curación/protección/Recall de grupo y muerte simultánea. La autoridad de composición y resultados corresponde al servidor. Referencia adicional de paquetes: [OpenFusion Groups.cpp](https://github.com/OpenFusionProject/OpenFusion/blob/master/src/Groups.cpp).

## Nanos: poderes, condiciones y efectos

El catálogo enumera **42 Nanos y 126 referencias distintas de poder**. Las referencias de modelo de esos Nanos se resolvieron incluyendo el bundle Tutorial. Clasificación de las 126 entradas:

- 53 son pasivas o no usan la solicitud activa.
- 69 tienen ruta de solicitud activa; esto no confirma su resultado jugable en todas las condiciones.
- 4 corresponden a Recall/Group Recall: poderes de **Edd, Dexter, Mandy y Belladonna**. La auditoría encontró que estaban bloqueados incondicionalmente; la continuación local habilita su solicitud bajo las condiciones de Legacy. Su resultado en Retrobution sigue pendiente de prueba jugable.

La continuación conecta instancia, punto confirmado y cooldown con la solicitud propia. El servidor sigue siendo responsable del destino, energía y teletransporte. Se bloquea el movimiento durante una espera acotada y se libera al recibir GOTO, warp, rechazo o al vencer el plazo. No se altera la posición al pulsar el poder ni al leer las coordenadas del registro.

También se corrigió la interpretación de `REGIST_RXCOM`: su primer campo es el número de mapa, no un ID de NPC. Se exige la respuesta completa de 16 bytes y el aviso de registro aparece al recibirla. El NPC solicitado se conserva por separado para evitar registros repetidos. Las solicitudes de Group Recall usan el objetivo propio como Legacy; la composición del grupo la resuelve el servidor. La invitación recibida valida resultados completos, admite una sola ventana y caduca incluso detrás de otro aviso. Se descarta al reubicarse o cambiar de sesión y aceptar reproduce el proyectil de Recall 93 sin anticipar el movimiento.

Referencias de servidor para el flujo de reubicación: [OpenFusion Nanos.cpp](https://github.com/OpenFusionProject/OpenFusion/blob/master/src/Nanos.cpp) y [Abilities.cpp](https://github.com/OpenFusionProject/OpenFusion/blob/master/src/Abilities.cpp). No se asume que el backend privado de Retrobution sea idéntico.

Los tipos de resultado referenciados actualmente por Nanos y NPC tienen tamaño conocido por `SkillResults`. ExtraBank (tipo 36) aparece en las tablas generales, pero no en esas referencias: no se presenta como una habilidad de Nano rota. Los poderes de Nano enumerados tienen `m_iBuffEffect=0`; la ausencia de uso de ese campo en runtime no prueba un efecto perdido en esos poderes.

**Matriz jugable pendiente:** cada poder con/sin energía, cooldown, boost y gumball; activo/pasivo; objetivo propio/aliado/enemigo; cono/área/distancia; grupo dentro/fuera de streaming; protección de infección; expiración, muerte, cambio de Nano, cambio de mapa y respuesta rechazada. Deben contrastarse HP, energía y condiciones recibidas con animación, sonido, iconos y partículas. La corrección de salto/ralentización tiene regresiones propias, pero no sustituye esa matriz.

## Armas y efectos de mobs

Se enumeraron **777 filas de armas**, no 777 pruebas de combate. Hay rutas para combate normal, cohetes, animación remota y habilidades de NPC; las regresiones existentes cubren paquetes, feedback, alineación de cohetes, audio y varios estados letales/de drenaje/curación.

Se encontraron **11 referencias distintas a habilidades activas/de soporte de NPC**. El inventario combinado de habilidades de Nano/NPC y sus proyectiles derivados resolvió **15 IDs de efectos compartidos**, sin referencias ES sin resolver ni índices de proyectil inválidos dentro de ese alcance. No es un escaneo de todos los efectos ambientales o modelos de todos los mapas.

Falta validar en juego rifle/cohete/melee/armas arrojadizas y familias de modelos, quieto/en movimiento/en aire, distintos ángulos, distancias, obstáculos, target muerto o retirado, boost/gumball, crítico/bloqueo, daño múltiple y respuesta tardía. Para mobs: preparación, ataque, impacto, drenaje, stun/mez/slow, muerte y retirada, incluidos jugadores remotos. Una referencia resuelta no garantiza material, orientación, duración o audio correctos.

## Infected zones y Fusion Lairs

El inventario reproduce **151 filas de instancia**, **322 destinos warp** y **173 bundles Map_**. Las filas contienen prototipos y los bundles incluyen regiones: **ninguno de esos números es un conteo de infected zones completamente probadas**. Se registran restricciones de nivel, misión, objeto y tarifa de warp.

Las regresiones de mundo, misiones, infección, recuperación y aterrizaje verifican rutas concretas. Las comprobaciones previas de KND Training Area/Practice Pranks aportan evidencia sobre ese recorrido, no sobre todas las instancias.

Para cerrar cada zona se requiere: entrada/salida y restricciones reales; spawn sobre colisión; streaming/LOD; infección y protección; plataformas/interruptores; marcador de misión; contador de mobs; lair solo/grupo; boss, recompensa y adquisición de Nano; Recall; muerte/respawn; retorno al mundo y desconexión. El catálogo marca `gameplayValidated=false` deliberadamente para no convertir disponibilidad de datos en validación jugable.

## Cinemática y tutorial

La introducción utiliza actores, animaciones, voces y una secuencia adaptada. `TutorialIntroScreen` monta una escena de previsualización `CharStage` de 512×512 con encuadre y shake propios: no reproduce todavía toda la puesta en escena/cámara de Legacy de forma demostrada.

Tech Square tiene entrenamiento local aislado, coherente con el tutorial offline de Legacy. Emite paquetes de práctica hacia los sistemas normales; no debe confundirse con recompensas online confirmadas. Parte de las recompensas se obtiene de las tablas de misión y hay recuperación local de HP. La secuencia de lecciones, coordenadas e interacción de combate es adaptada; el primer Nano y el ejercicio de poder están ligados a Buttercup/skill 1.

Pendiente de equivalencia completa: comparar de principio a fin todos los eventos, cámaras, tiempos, diálogo/subtítulos/voz, iconos de teclado/mando/touch, bloqueos de ventanas, colisión, AI/animaciones/efectos de mobs, requisitos de lección y cantidades/elección de recompensas. Verificar salto de introducción, cancelación, reentrada, inventario lleno y entrega final al servidor sin duplicaciones. Las regresiones del tutorial son verificaciones acotadas, no una reproducción interactiva completa.

## Fidelidad visual

Unity 2022/URP utiliza conversión de materiales y partículas Legacy. `ShaderTargets` contiene rutas genéricas de material y `LegacyParticles` aproxima varias formas con emisores modernos. `GraphicsFx` conserva una opción soft vegetation cuyo ajuste original no aplica directamente en URP. Esas conversiones necesitan comparación visual, aunque compilen y resuelvan recursos.

No se certifica fidelidad completa. Hace falta capturar Legacy y Android con cámara, hora, ambiente, equipamiento y estado equivalentes para comparar personajes, transparencias, luz, rimlight estacional, niebla, agua, partículas, UI, LOD y estabilidad lejana. Repetir en OpenGL y Vulkan y en pantalla individual/DS sin compartir targets de render incorrectamente.

## Validación ejecutada y reproducción

- `SocialGroupRegressionChecks`: paquetes sintéticos de join/update/leave, UID fuera de streaming, roster truncado/duplicado, reinicio de sesión, bloqueo/desbloqueo y burbuja de chat; caducidad de velocidad/salto/slow propia y de otro PC. Comprueba que no se envíen operaciones sociales.
- La prueba anterior se incorpora al flujo normal `OpenFusionBuildRequest`.
- `RecallRegressionChecks`: registro confirmado/truncado, restricciones de instancia/punto, paquetes de ambos poderes, cooldown y duplicados, posición/energía sin predicción, liberación por rechazo/GOTO/plazo, invitaciones completas/truncadas, aceptación y caducidad detrás de otro aviso. Paquetes sintéticos y emisor capturado; pendiente del servidor real.
- `BuddyNameRegressionChecks`: nombres y límites, offsets UTF-16 de búsqueda y aceptación, rechazo de nombres inválidos, aceptación manual, duplicados, opciones sociales, bloqueo y mensajes privados, estados online/offline, fallos Legacy, snapshots truncados, aceptar/rechazar/caducar grupos, nueva sesión y warp de buddies con/sin pertenencia al grupo. No envía invitaciones reales; intercepta el emisor.
- `LegacySystemAudit.Batch`: enumera datos actuales y resuelve las referencias compartidas. Exporta `Logs/legacy-audit/catalog.json` y `catalog-summary.txt`; descarga recursos a caché local ignorada por Git.
- La suite completa del build local terminó y la APK compiló: **0 errores, 17 avisos**. Log: `Logs/legacy-audit/build.log`. APK: `Builds/OpenFusion-Pocket-controls-preview.apk`.
- La APK es una compilación local de diagnóstico con `POCKET_INPUT_PREVIEW`; no es un release ni se publicó. No se instaló durante esta auditoría.
- Tras integrar Recall se repitió la suite completa y el build: **0 errores, 17 avisos**, `Logs/legacy-audit/recall-build.log`. La APK local anterior se sustituyó por esa compilación. `recall-checks.txt` conserva el resultado de las regresiones de Recall. El puerto ADB anterior no responde; faltan conexión actualizada y un Nano adecuado para probar contra Retrobution.
- El 26 de octubre se completó ADD por nombre y la continuación de buddies/grupos. La suite completa y el build final pasaron con **0 errores, 17 avisos**: `Logs/legacy-audit/buddy-build-final.log`. APK local de diagnóstico: `Builds/OpenFusion-Pocket-social-preview.apk`. Las pruebas incluyen además mensajes privados bloqueados/desbloqueados, estados online/offline, aceptar grupo, fallos Legacy, warp al grupo y salida rechazada/reintentada sin perder miembros. Falta instalación y validación con dos cuentas; no se publicó ni se enviaron invitaciones reales.

Para repetir el inventario con Unity cerrado, ejecutar Unity en batch con el proyecto y `-executeMethod OpenFusion.EditorTools.LegacySystemAudit.Batch`. Los resultados dependen de las tablas que devuelva la API de Retrobution en ese momento. El inventario no se ejecuta automáticamente en cada build ni reemplaza pruebas de servidor/dispositivo.

## Orden propuesto para cerrar brechas

1. Validar Recall en instancia, Group Recall y ADD por nombre implementados localmente con dos cuentas; validar muerte/respawn y grupos con esas cuentas.
2. Confirmar soporte de servidor para Use Item y User Stores; implementar transacciones sólo con confirmaciones reales.
3. Comparar y completar la secuencia exacta de introducción/Tech Square y sus recompensas.
4. Ejecutar la matriz de habilidades/armas/mobs y el recorrido por cada instancia disponible; corregir hallazgos sin saltar restricciones del servidor.
5. Cerrar diferencias visuales mediante capturas equivalentes de Legacy y Android y pruebas en ambos backends gráficos.
