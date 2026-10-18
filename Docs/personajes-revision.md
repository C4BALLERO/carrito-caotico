# Revisión de personajes, esqueletos y animación

Revisión del 18/10/2026 sobre `dev` (`f12d487`), Unity 6000.5.6f1, Built-in Render Pipeline. El detalle de cada esqueleto está en [esqueletos.md](esqueletos.md), generado con *Carrito > Inspeccionar esqueletos*.

## Qué hay en el proyecto

| Personaje | Modelo | Esqueleto | Animación actual |
|---|---|---|---|
| Pingüino, Zorro, Cerdo (Empujador y Agarrador) | FBX de Tripo en `06_Models`, una malla con piel | Generic sin Avatar. 67 / 59 / 52 huesos, con todas las partes estándar (caderas, columna, pecho, cuello, cabeza, hombros, brazos, manos, piernas y pies). El Cerdo usa nombres Mixamo. | Por código: `CharacterWaddle` balancea el modelo entero, `FlipperFlap` mueve las aletas del pingüino y `ArmReach` gira el brazo del Agarrador hacia su mano |
| Compradores rivales | Primitivas: cápsula, esfera y carrito pequeño | Ninguno | `RivalShopper` desliza el objeto a velocidad constante |
| Reponedor | Primitivas: cápsula, esfera y caja | Ninguno | `MovingObstacle` lo desliza entre dos puntos |
| Fantasmas (Halloween) | Primitivas: sábana, ojos y boca | Ninguno (no lo necesitan) | `MovingObstacle` con flotación |

En el proyecto **no hay** clips de animación, Animator Controllers ni Avatars. Los FBX no traen tomas de animación (`importAnimation` está desactivado y tampoco tienen ninguna). Unity Humanoid no se usa.

## Fallos encontrados

### Empujador (los tres personajes)
- **Pies que patinan:** el personaje va pegado al carrito y no da pasos; cuando el carrito avanza, los pies se deslizan por el suelo a su velocidad.
- **Manos que no agarran el asa:** el Zorro conserva su pose y el Pingüino mueve las aletas; ninguno sujeta el asa que empuja.
- **Cabeza fija:** no mira hacia donde va ni hacia las curvas.

### Agarrador
- El brazo derecho apunta bien a la mano extensible (`ArmReach`), pero **la cabeza no la sigue**: mira siempre al frente.

### Pingüino (esqueleto)
- `Neck`, `Left_Foot` y los dedos de los pies **no tienen peso** en la malla. Girarlos no deforma nada, así que la cabeza se orienta con `Head` y las piernas con `UpperLeg` y `LowerLeg`. Los pesos no se repintan: haría falta una herramienta de modelado externa.

### Compradores rivales
- No tienen piernas, brazos ni cara: **se deslizan** sin caminar.
- **Arrancan y frenan de golpe** a velocidad constante y **giran mientras avanzan**, sin girarse antes.
- Se llevan el producto **sin alcanzarlo**: aparece directamente en su carrito.
- Varios rivales **pueden ocupar el mismo sitio**, y empujan el carrito de los jugadores porque no lo esquivan.
- Todos tienen la misma forma y solo cambia el color, así que **se confunden entre sí** y con los protagonistas.

### Reponedor
- Es una cápsula que se desliza: no camina ni lleva la caja con las manos.

## Decisiones

1. **Animación procedural, sin Animator Controllers.** No hay clips ni manera de crearlos con calidad dentro del proyecto. Los estados (reposo, caminar, girar, alcanzar, gestos) se calculan en código con transiciones suaves, a partir del **movimiento real** de cada personaje. Así la animación y la física nunca compiten por la posición: el código mueve el cuerpo y la animación solo coloca los huesos.
2. **IK de dos huesos propia** (`TwoBoneIK`) para piernas y brazos. Funciona con cualquier orientación de huesos, así que sirve para los FBX de Tripo, el esqueleto Mixamo y los compradores nuevos.
3. **Pisadas plantadas** (`FootPlanter`): cada pie se queda fijo en el suelo hasta que le toca dar el paso. Elimina el patinaje del Empujador y de los compradores.
4. **Compradores caricaturescos nuevos generados por código** (`ShopperModels`). No hay herramientas de modelado 3D en el proyecto, y los generadores externos devuelven mallas sin esqueleto que Unity no importa sin paquetes extra. Por eso cada comprador es un **modelo segmentado**: piezas de primitivas unidas a una jerarquía real de huesos (caderas → columna → pecho → cuello → cabeza; hombros → brazos → manos; muslos → piernas → pies). Al ser piezas rígidas no hay pesos que se deformen mal. No son mallas con piel: es un esqueleto de transforms.
5. **Integración preparada para modelos con piel:** `ShopperRig` solo guarda referencias a huesos. Un FBX futuro con esqueleto se puede conectar asignando sus huesos, sin tocar `ShopperAnimator` ni `RivalShopper`.
6. **Se mejora la IA existente** (`RivalShopper` y `RivalManager`) en lugar de crear otra: aceleración y frenado, giro antes de caminar, separación entre rivales, cederle el paso al carrito de los jugadores y alcanzar el producto con la mano.

## Rendimiento

- Las piezas de los compradores comparten materiales: no hay instancias de material por comprador.
- `ShopperAnimator` deja de calcular los huesos cuando el comprador no se ve y, si está lejos, los actualiza en fotogramas alternos.
- En partida hay como mucho seis rivales y un reponedor.

## Resultado

| Problema | Solución | Cómo se comprueba |
|---|---|---|
| Pies del Empujador que patinan | `CartPusherRig` + `FootPlanter`: pasos al ritmo del carrito, pie apoyado fijo | `Pusher_WalksBehindTheCartWithoutSliding`: el pie apoyado se mueve menos de 1 cm por fotograma |
| Manos que no agarran el asa | IK de dos huesos hacia `Agarre_Asa_Izq/Der`; el tronco se inclina si el brazo es corto | Misma prueba: la mano llega al asa o se estira del todo hacia ella |
| Cabeza fija (Empujador y Agarrador) | La cabeza del Empujador sigue la curva; `HeadLook` hace que el Agarrador mire su mano | Revisión visual |
| Rivales que se deslizan | Compradores con esqueleto y `ShopperAnimator` | `Walking_FeetStayPlantedAndOnTheGround`: pie apoyado < 5 mm por fotograma y un pie siempre en el suelo |
| Arranques y giros bruscos | Aceleración, frenado y giro antes de caminar (más de 60°) en `RivalShopper`; el reponedor gira antes de moverse | `TurningInPlace_StepsAroundAndStopsSettled` |
| Producto que aparece en el carrito | Se giran hacia la balda, se agachan, lo cogen con la mano y lo dejan en su carrito | `Reaching_LowProduct_CrouchesAndTouchesIt`, `Rivals_KeepTheirDistanceAndGrabWithTheHand` |
| Rivales que se solapan | Espacio personal, frenado ante el carrito y otros rivales, rodeo tras 1,4 s | `Rivals_KeepTheirDistanceAndGrabWithTheHand`: más de 0,45 m entre rivales en marcha |
| Rivales iguales entre sí | Ocho estilos por tienda, disfraces en Halloween | `ShopperModelTests` |

En LAN, el anfitrión envía la variante, la acción y el punto de alcance de cada rival (protocolo versión 3) para que el otro PC vea la misma animación.

## Menú principal

Se rehízo con la jerarquía de la referencia (menú de un juego de acción): escena viva de fondo, título grande, opciones de texto centradas con la elegida resaltada y su descripción abajo. Ver `menu_clasico.png` y `menu_halloween.png` (capturas reales de la escena en ejecución, hechas por `CarritoMenuTests`).

## Archivos

**Nuevos**
- `01_Scripts/Characters/`: `TwoBoneIK.cs`, `FootPlanter.cs`, `ShopperRig.cs`, `ShopperAnimator.cs`, `CartPusherRig.cs`, `HeadLook.cs`.
- `01_Scripts/UI/`: `MenuList.cs`, `MenuBackdrop.cs`. `01_Scripts/Core/OptionalAttribute.cs` (referencias que pueden quedar vacías).
- `Editor/`: `ShopperModels.cs`, `ShopperPreview.cs`, `RigInspector.cs`.
- `Tests/`: `EditMode/ShopperModelTests.cs`, `PlayMode/CarritoShopperAnimationTests.cs`, `PlayMode/CarritoMenuTests.cs`.
- Generados: `02_Prefabs/Compradores/Comprador_*.prefab` (16), materiales `M_Piel_*`, `M_Ropa_*`, `M_Pelo_*`, `M_Disfraz_*` y de la cara, sprites `UI_Vineta`, `UI_Columna` y `UI_Pincelada`.
- `Docs/`: este archivo, `esqueletos.md` y las capturas.

**Modificados**
- `RivalShopper.cs` (reescrito por fases), `RivalManager.cs` (variantes, carrito del jugador, producto en la mano), `MovingObstacle.cs` (giro y estado para la animación).
- `LanProtocol.cs` y `LanSync.cs` (campos nuevos del rival), `MenuUI.cs`, `GameInput.cs` (izquierda/derecha del menú), `UIButtonFeedback.cs` (opción de lista), `GameAudio.cs` (nombre del modo en mayúsculas).
- `CarritoSceneBuilder.cs`, `CharacterModels.cs`, `HudBuilder.cs`.
- Las dos escenas y los prefabs de producto (regenerados).

**Eliminado**
- `02_Prefabs/Comprador_Rival.prefab`: lo sustituyen los compradores nuevos.

## Compradores KayKit (fusión de `claude/charming-dijkstra-j8u1a5`)

Otra rama sustituía los rivales por personajes KayKit (CC0) con malla con piel y clips. Se fusionó **mezclando** los dos tipos en cada tienda: KayKit y caricaturescos son variantes del mismo `RivalShopper` (`RivalManager.rivalPrefabs`).

**Fallos encontrados en esa rama y corregidos:**
- **Los clips no movían ningún hueso.** Las curvas de `NPC_Animaciones.fbx` cuelgan del armature (rutas `""` y `root/hips/…`), pero el Animator estaba en la raíz del modelo, donde las rutas son `Rig/root/…`. Solo casaba 1 de 42 rutas. Ahora el Animator va en `Rig` (42/42).
- **Las texturas se importaban como Cubemap.** Los `.meta` del pack solo traen el GUID; ahora se fijan como textura 2D.
- **Dos clases `CarritoCaotico.ShopperAnimator`.** La suya se sustituye por `ClipShopperAnimator`, que lee la misma IA (`IShopperBrain`) que los caricaturescos.
- **`updateWhenOffscreen` activado** en todas las mallas: se cambia por límites ampliados una sola vez.

**Animación de los KayKit (`ClipShopperAnimator`):**
- **Caminar** va al ritmo de la velocidad real. El generador mide en los propios pies del clip a qué velocidad pisa: 0,49 m/s a escala 0,72.
- **Pies bloqueados:** mientras el clip apoya un pie, ese pie se bloquea con IK de pierna.
- **Medición** (pie apoyado que resbala respecto a lo que avanza el cuerpo):

  | Velocidad | Resbala |
  |---|---|
  | 1,1 m/s | 5 % |
  | 2,2 m/s (velocidad base de los rivales) | 12 % |
  | 3 m/s | 15 % |
  | 4,5 m/s | 36 % |

  Sin ajustar el ritmo ni bloquear los pies, el pie se movía con el cuerpo: 100 %.
- **Recoger** sigue el progreso del gesto, de modo que la mano llega al punto más bajo del clip (39 % de su duración) justo cuando la IA coge el producto. Encima, el IK lleva esa mano al producto, la otra queda en el asa y la cabeza mira el producto.
- **Pruebas:**
  - `KayKit_WalkClipKeepsTheFootPlanted`: menos del 15 % a 2,2 m/s.
  - `KayKit_HoldsTheHandleAndReachesTheProduct`: manos a 2,4 cm del asa y mano a 6,6 cm del producto.
  - `KayKitShoppers_UseTheSharedClipsAndHoldTheCart`.

Capturas en partida, hechas con una cámara junto al rival:
- `kaykit_clasico_caminando.png` y `kaykit_halloween_caminando.png`: empujando el carrito con las manos en el asa.
- `kaykit_clasico_cogiendo.png` y `kaykit_halloween_cogiendo.png`: con el producto en la mano.

## Pendiente

- Los compradores son modelos segmentados, no mallas con piel. Para usar modelos de un artista: importar el FBX con esqueleto, asignar sus huesos en `ShopperRig` y quitar las piezas generadas; `ShopperAnimator` y `RivalShopper` no cambian.
- Los KayKit no tienen clip de correr. A la velocidad de los niveles altos (4,5 m/s), su pie apoyado aún resbala alrededor de un tercio. Faltaría `Running_A` del pack original, recortado con `Tools/recortar_kaykit.py`.
- Los pesos del pingüino (cuello y pies) solo se arreglan en un programa 3D.
- El cerdo no llega del todo al asa con sus brazos cortos.
