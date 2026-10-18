# Carrito Caótico

Juego cooperativo para dos jugadores, en un mismo PC o en **dos PC por LAN**, hecho en Unity para la Game Jam **Better Together** de 48 horas.

Dos jugadores comparten un carrito de supermercado. Uno lo empuja y otro, sentado dentro, agarra productos con un brazo extensible. Solo quien agarra ve la lista de compras, así que tienen que hablar para encontrar los productos, meterlos en la canasta y llegar a la caja antes de que cierre la tienda.

## Tema: Better Together

- **Empujador (Jugador 1):** conduce el carrito y mueve la cámara. No ve la lista.
- **Agarrador (Jugador 2):** mueve el brazo, agarra, suelta y lanza productos. Es el único que ve la lista.

Ninguno de los dos puede ganar solo. El Agarrador sabe qué comprar y el Empujador sabe adónde ir.

## Requisitos técnicos

| Elemento | Versión |
|---|---|
| Unity Editor | **6000.5.6f1** (obligatoria) |
| Render pipeline | Built-in Render Pipeline |
| Entrada | Input System (`com.unity.inputsystem`), *Active Input Handling* = Input System Package (New) |
| UI | uGUI (`com.unity.ugui`) con la fuente integrada `LegacyRuntime.ttf` |
| Pruebas | Unity Test Framework (`com.unity.test-framework`) |
| Red (LAN) | Unity Transport (`com.unity.transport` 6.5.0, incluido en Unity 6.5) |
| Plataforma | Windows 10/11 (x64) |

El escenario y los productos se construyen con primitivas y materiales de color plano. Los botones del menú y algunos sonidos de interfaz son assets **CC0** de [Kenney](https://kenney.nl); la lista está en `Assets/_CarritoCaotico/05_Audio/Kenney/CREDITOS.md`.

## Instalación y ejecución

1. Instala **Unity 6000.5.6f1** desde Unity Hub. Si no aparece en la lista, búscala en *Installs → Install Editor → Archive*.
2. Clona el repositorio:
   ```bash
   git clone https://github.com/C4BALLERO/carrito-caotico.git
   ```
3. En Unity Hub, pulsa *Add → Add project from disk* y elige la carpeta clonada. Ábrela con 6000.5.6f1.
4. Si Unity pregunta por el nuevo backend de entrada, elige **Yes** (Input System Package).
5. En el menú superior, ejecuta **Carrito → Construir escena de prueba**.
6. Pulsa **Play**.

El generador crea dos escenas en `Assets/_CarritoCaotico/00_Scenes/`: `CarritoCaotico_Prueba.unity` (clásica) y `CarritoCaotico_Halloween.unity`. Las dos se añaden al principio de *Build Settings*.

### Regenerar la escena de forma segura

Si la escena ya existe, el menú ofrece tres opciones:

- **Regenerar (con copia):** guarda una copia con fecha en `Assets/_CarritoCaotico/00_Scenes/Backups/` y después la reconstruye.
- **Cancelar:** no cambia nada.
- **Abrir la existente:** abre la escena sin modificarla.

Antes de empezar, el generador también pide guardar las escenas abiertas que tengan cambios. Los materiales y prefabs se reutilizan por nombre, así que ejecutarlo varias veces no los duplica.

Generación desde línea de comandos (útil para CI):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe" -batchmode -projectPath . -executeMethod CarritoCaotico.EditorTools.CarritoSceneBuilder.BuildFromCommandLine -logFile build-scene.log
```

## Controles

### Empujador (Jugador 1)

| Acción | Teclado | Mando 1 |
|---|---|---|
| Acelerar | `W` | Gatillo derecho / stick izq. arriba |
| Frenar y retroceder | `S` | Gatillo izquierdo / stick izq. abajo |
| Girar | `A` / `D` | Stick izquierdo |
| Derrapar | `Shift izquierdo` | `RB` / botón sur |
| Rotar cámara | Mouse | Stick derecho |

### Agarrador (Jugador 2)

| Acción | Teclado | Mando 2 |
|---|---|---|
| Mover el brazo | Flechas | Stick izquierdo |
| Agarrar / soltar | `Enter` (o `Enter` del teclado numérico) | Botón sur |
| Lanzar | `Ctrl derecho` | Gatillo derecho / botón oeste |
| Ver la lista (mantener, solo en un PC) | `Shift derecho` | `LB` |

### General

| Acción | Tecla |
|---|---|
| Pausa / continuar | `Esc`, `P`, botón **Pausa** o `Start` |
| Siguiente nivel (tras completar la compra) | `N` o botón **Siguiente nivel** |
| Reintentar al terminar | `R`, botón **Reintentar** o `Start` |
| Volver al menú al terminar | `M` o botón **Menú** |

### Un jugador con teclado y otro con mando

En el menú, el selector **Mando** decide quién usa el mando cuando solo hay uno conectado. La elección se guarda entre partidas.

| Opción | Con 1 mando | Con 2 mandos |
|---|---|---|
| Automático | El mando es del Empujador y el Agarrador usa el teclado | Un mando para cada jugador |
| Empujador | El mando es del Empujador y el Agarrador usa el teclado | Un mando para cada jugador |
| Agarrador | El mando es del Agarrador y el Empujador usa el teclado | Un mando para cada jugador |

Botones del mando:

| Empujador | Agarrador |
|---|---|
| `RT` acelerar · `LT` frenar / atrás · stick izquierdo girar · `RB` derrapar · stick derecho cámara | Stick izquierdo mover brazo · `A` agarrar / soltar · `RT` lanzar |

- `Start` pausa y continúa, y al terminar reinicia.
- `Select` / `View` salta un paso del tutorial.
- Los menús, el recibo y la pausa se navegan con el stick o la cruceta y se confirman con `A`.
- El tutorial, la tarjeta de preparación y la pausa muestran los botones del mando para el jugador que lo usa y las teclas para el otro.
- Con el esquema **Intercambiado**, el mando también pasa al otro jugador.
- Si se conecta o desconecta un mando durante la partida, aparece un aviso y las indicaciones se actualizan.

### Controles que cambian

A partir del **nivel 4**, cada nivel puede cambiar los controles al azar. El esquema se anuncia en la tarjeta de preparación, los paneles muestran «¡Cambiados!» o «¡Al revés!» y las teclas del HUD se actualizan.

| Esquema | Empujador | Agarrador |
|---|---|---|
| Normal | `W` `S` `A` `D` + `Shift izq.` | Flechas + `Enter` + `Ctrl der.` |
| Intercambiado | Flechas + `Shift der.` | `W` `A` `S` `D` + `Espacio` + `F` |
| Al revés | Teclas normales con las direcciones invertidas | Teclas normales con las direcciones invertidas |

El esquema de cada nivel se mantiene al reintentarlo. Con el esquema intercambiado, también se intercambian los mandos.

## Jugar en LAN (dos PC)

Cada jugador juega en su PC, conectados a la misma red (wifi o cable). Así la lista queda oculta de verdad: solo aparece en el PC del Agarrador.

| | Empujador | Agarrador |
|---|---|---|
| Vista | **Tercera persona o primera persona** (opcional, `V` o `Y` en el mando) | **Tercera persona** (siempre) |
| Pantalla | Reloj y progreso total, sin lista | Lista completa |
| Teclado | `W A S D` o flechas · `Shift` derrapar · mouse cámara | `W A S D` o flechas · `Enter` / `Espacio` agarrar · `F` / `Ctrl` lanzar · mouse cámara |
| Mando | El primer mando del PC, con los mismos botones de siempre | El primer mando del PC, con los mismos botones de siempre |

La vista elegida por el Empujador se guarda entre partidas. En primera persona la cámara va en sus ojos, detrás del asa, y su cuerpo solo proyecta sombra.

### Cómo conectarse

1. Compila el juego (**Carrito → Compilar para Windows (LAN)**) y copia la carpeta `Builds/Windows` a los dos PC. También vale abrir el proyecto en Unity en uno de ellos.
2. **PC 1 (anfitrión):** *Jugar en LAN* → elige tu rol (Empujador o Agarrador) → **Crear partida**. El panel muestra **tu IP en grande** (por ejemplo `192.168.0.38`).
3. **PC 2:** *Jugar en LAN*. Si los dos están en la misma wifi, **la partida aparece sola** y la IP se rellena; si no, escríbela. Pulsa **Unirse**. Le toca el otro rol.
4. El anfitrión vuelve al menú, elige supermercado y nivel y pulsa **Jugar**. Los dos PC cargan el nivel a la vez.

La IP que se muestra es la de la wifi o el cable de casa: los adaptadores virtuales (Hyper-V, WSL, VPN, Docker…) se dejan al final.

### Si no conecta

Casi siempre es Windows, que bloquea la conexión entrante en el PC anfitrión. En **los dos PC**:

1. **Red privada:** *Configuración → Red e Internet → Wi-Fi → (tu red) → Tipo de perfil de red → Privada*. Con la red como **Pública**, el Firewall bloquea el juego.
2. **Firewall:** la primera vez que creas una partida, Windows pregunta si el juego puede usar la red: marca **Redes privadas** y pulsa *Permitir*. Si lo cancelaste, ve a *Seguridad de Windows → Firewall y protección de red → Permitir una aplicación a través del firewall* y activa **Carrito Caotico** (o **Unity** si juegas desde el editor) en *Privada*.
3. Comprueba que los dos están en **la misma wifi** (no una de invitados) y usa la IP que muestra el anfitrión.

El juego usa el puerto UDP **7777** para jugar y el **47777** para anunciar la partida en la red.

### Cómo funciona

- El **anfitrión** simula la física, los rivales, el reloj y las reglas. El otro PC manda los controles de su jugador y recibe unas 50 instantáneas por segundo, que interpola con 70 ms de retraso para que todo se mueva suave.
- La mano del Agarrador se mueve en su propio PC sin esperar a la red. Agarrar, soltar y lanzar los resuelve el anfitrión donde está esa mano.
- La pausa, reintentar, el siguiente nivel y volver al menú funcionan desde cualquiera de los dos PC.
- Con el esquema **Intercambiado**, en LAN los dos PC **cambian de puesto** durante ese nivel: el Empujador pasa a agarrar y al revés, con su vista y su lista. **Al revés** invierte las direcciones como siempre.
- Si se corta la conexión, los dos PC vuelven al menú con un aviso.
- El tutorial es solo para un PC.

### Probar en un solo PC

Se pueden abrir dos copias del juego en el mismo PC con argumentos de línea de comandos:

```bash
Builds/Windows/CarritoCaotico.exe -screen-fullscreen 0 -screen-width 960 -screen-height 540 -lanHost Empujador -lanLocal -lanPlay 1
```

```bash
Builds/Windows/CarritoCaotico.exe -screen-fullscreen 0 -screen-width 960 -screen-height 540 -lanJoin 127.0.0.1
```

`-lanHost Empujador|Agarrador` crea la partida, `-lanJoin IP` se une, `-lanPort` cambia el puerto, `-lanLocal` solo acepta conexiones de este PC y `-lanPlay N` empieza el nivel N al conectarse (`-lanStore Halloween` elige el otro supermercado).

## Cómo se juega

0. **Menú de inicio:** elige el **supermercado** (Súper Caótico o Súper Embrujado), el **nivel** (solo los desbloqueados) y pulsa **Jugar**. La primera vez conviene hacer el **Tutorial**. El progreso se guarda en `PlayerPrefs`.
1. **Preparación (4 s):** el Agarrador lee la *Lista del Agarrador*, en el lado derecho de la pantalla.
2. **En juego (120 s):** el Empujador conduce hasta los pasillos y el Agarrador recoge los productos.
   - La mano se vuelve **amarilla** cuando hay un producto a su alcance y **verde** cuando sostiene uno.
   - Al soltar un producto cerca del carrito, cae dentro de la canasta.
   - Solo cuentan los productos que están **dentro de la canasta**. Uno sostenido por la mano no cuenta.
   - La carga hace el carrito más pesado: acelera y frena con más inercia.
3. **Caja:** al entrar en la zona verde junto a la caja, la compra se evalúa una sola vez y aparece el recibo.
4. **Tiempo agotado:** si el tiempo llega a 0, aparece **«¡La tienda cerró!»**.
5. **Siguiente nivel:** una compra completa (sin faltantes) desbloquea el nivel siguiente.

### Tutorial

El tutorial guía a los dos jugadores paso a paso, en el supermercado clásico y sin reloj ni rivales. Son 10 pasos:

1. Acelerar.
2. Girar.
3. Derrapar.
4. Mover la cámara.
5. Mover el brazo.
6. Agarrar un producto.
7. Soltarlo en la canasta.
8. Lanzar un producto.
9. Completar una lista de dos productos.
10. Pagar en la caja.

Cada paso muestra sus teclas y avanza solo cuando los jugadores hacen la acción; `Tab` lo salta. Al terminar se puede pasar directamente al nivel 1.

### Vida del carrito

- El carrito tiene **100 de vida**. Los choques fuertes contra estantes, paredes, rivales u obstáculos le quitan vida según la velocidad del golpe (como mucho 30 por golpe). Los roces, el suelo y los productos no hacen daño.
- La **barra de vida** está abajo a la izquierda, con el porcentaje y un color (verde, ámbar, rojo). Tiembla con cada golpe y late cuando queda poca; con un 30 % aparece un aviso.
- A cero, **el carrito explota**: bolas de fuego, humo, un destello y las piezas del carrito volando. La partida termina con el recibo «¡El carrito explotó!» y la compra no cuenta.
- En el tutorial el carrito no se rompe. En LAN la vida la calcula el anfitrión y la explosión se ve en los dos PC.

### Botones y sonido del menú

- Los botones crecen, suben un poco y suenan al pasar el mouse; con teclado o mando crecen al seleccionarlos. Al pulsarlos se aplastan. El botón principal late suavemente.
- El botón **Sonido** cambia entre *todo*, *solo efectos* y *no*, para la música, los ambientes y los efectos. La elección se guarda entre partidas.

### Interfaz durante la partida

Mientras se juega solo se ve la **Lista del Agarrador**, con el reloj y el nivel integrados en su cabecera, y avisos breves («¡Un rival se llevó algo de vuestra lista!»).

**La lista solo la mira el Agarrador:**
- **En un PC**, durante la partida va **plegada** (solo reloj y progreso total) para que el Empujador no la vea. El Agarrador la despliega **manteniendo** `Shift derecho` (`Shift izquierdo` con controles intercambiados, `LB` con mando). Mientras la mira, **el brazo se queda quieto**: hay que elegir entre leer y agarrar. En la preparación se ve entera para memorizarla, y el tutorial tiene un paso que lo enseña.
- **En LAN**, el PC del Empujador nunca la ve y el del Agarrador la ve siempre.

Los controles aparecen en tres sitios:

- el tutorial;
- la tarjeta de preparación de cada nivel, con el esquema activo;
- el menú de pausa (`Esc` / `P`).

### Supermercados

| | Súper Caótico | Súper Embrujado (Halloween) |
|---|---|---|
| Escena | `CarritoCaotico_Prueba.unity` | `CarritoCaotico_Halloween.unity` |
| Pasillos | 6 | 4 |
| Productos | 12 tipos de supermercado | 8 tipos de Halloween: calabaza, caramelos, vela, calavera de azúcar, poción, sombrero de bruja, murciélago de peluche, manzana de caramelo |
| Ambiente | Luminoso, colores frescos | Oscuro con niebla morada, calabazas iluminadas, murciélagos, telarañas y un caldero en la caja |
| Obstáculos móviles | Reponedor en la entrada | Dos fantasmas flotando por los pasillos transversales |

### Personajes

En el menú, la fila **Jugador 1 · Empujador** / **Jugador 2 · Agarrador** permite que cada jugador elija su personaje: **pingüino**, **zorro** o **cerdo**. La elección se guarda entre partidas y el carrito cambia al momento. Pueden elegir el mismo.

- **Empujador:** de pie detrás del asa. Se bambolea al ritmo del carrito, se inclina en las curvas y, si el modelo venía en pose T (pingüino y cerdo), mueve los brazos.
- **Agarrador:** de pie en el asiento, visible de pecho para arriba. Señala con el brazo derecho la mano extensible mientras la mueve.
- Por defecto: pingüino Empujador y zorro Agarrador.

Los tres personajes están colocados en los dos puestos de las dos escenas; `CartCrew` activa solo los elegidos. La posición se calcula con las medidas de cada modelo para que ninguno atraviese el carrito.

### Obstáculos

- **Charcos** (azules o de baba verde): el carrito pierde casi todo el agarre lateral y patina (`SlipperyZone`).
- **Pilas que se derriban:** cajas de promoción o calabazas decorativas.
- **Carritos abandonados:** se pueden empujar, pero pesan.
- **Palés** y **lápidas:** son fijos y están junto a las paredes.
- **Reponedor y fantasmas:** se mueven de un lado a otro y empujan el carrito (`MovingObstacle`).

### Pantallas de carga

Al cambiar de supermercado, reintentar, pasar de nivel o volver al menú aparece una pantalla de carga con una ilustración de la tienda de destino, un consejo y una barra de progreso (`LoadingScreen`).

Las imágenes se toman de `Assets/_CarritoCaotico/Resources/Carga/`:
- `<tienda>_ia_N.png`: ilustraciones generadas con IA. Tienen prioridad y el generador nunca las sobrescribe.
- `<tienda>_captura_N.png`: capturas de la escena que el generador renderiza automáticamente. Sirven como alternativa.

Para usar ilustraciones con IA, configura `OPENROUTER_API_KEY` (skill `generate-image`), genera las imágenes, guárdalas con ese nombre y ejecuta **Carrito → Configurar imágenes de carga**.

### Niveles

Los niveles se generan con `LevelProgression`. Cada nivel es más difícil que el anterior hasta el nivel 12, y a partir de ahí la dificultad ya no sube.

| Nivel | Lista (unidades) | Máx. por tipo | Tiempo | Rivales |
|---|---|---|---|---|
| 1 | 3–5 | 2 | 2:00 | 0 |
| 2 | 3–5 | 2 | 2:00 | 1 |
| 3 | 4–6 | 2 | 1:55 | 1 |
| 4 | 4–6 | 2 | 1:50 | 2 |
| 6 | 5–7 | 3 | 1:40 | 3 |
| 8 | 6–8 | 3 | 1:30 | 4 |
| 12 | 7–8 | 3 | 1:25 | 6 |

Además, con cada nivel los rivales caminan más rápido, tardan menos en agarrar, llevan más productos y prefieren los de tu lista.

### Compradores rivales

- Entran por un lateral de la tienda y recorren una red de pasillos (centros de pasillo y tres pasillos transversales). Se detienen frente a un producto y lo meten en su carrito, donde se ve.
- Cuando llenan el carrito se van y, al rato, entra otro.
- Si se llevan un producto de tu lista aparece el aviso **«¡Un rival se llevó…!»**. El chip superior derecho cuenta los rivales y los productos robados.
- **Regla justa:** un rival nunca se lleva una unidad que necesites si con eso quedarían menos unidades en las baldas de las que te faltan. Te ganan los productos cercanos, pero la lista nunca se vuelve imposible.
- Son cinemáticos: empujan el carrito si se cruzan con él.

### El supermercado

Mide unos 42 × 50 m y tiene seis pasillos, de izquierda a derecha. Cada estante está partido en dos tramos por un pasillo transversal en el centro, así que se puede cambiar de pasillo a mitad de camino. La salida está frente al pasillo 3 y la caja, con su zona de entrega verde, queda al fondo.

| Pasillo | Cara izquierda | Cara derecha |
|---|---|---|
| 1 · Lácteos | Leche: cartón blanco con franja azul y tapón | Queso: rueda amarilla con cera roja |
| 2 · Desayuno | Cereal: caja amarilla con panel rojo y tazón | Huevos: huevera con seis huevos |
| 3 · Frutas | Sandía: elipsoide verde con rayas | Manzana: roja, con rabito y hoja |
| 4 · Plátanos y pan | Plátano: racimo de tres | Pan: barra con corteza y cortes |
| 5 · Bebidas y latas | Agua: botella azul con tapón y etiqueta | Lata: roja, con etiqueta y bordes metálicos |
| 6 · Hogar | Papel higiénico: paquete rosado de cuatro rollos | Detergente: garrafa morada con asa |

Hay 12 tipos y 6 unidades de cada uno, 72 productos en total. Los productos se construyen con varias primitivas y tienen un único collider simple, para que la física sea estable. La balda inferior de cada estante lleva paquetes decorativos del color de sus productos, para reconocer el pasillo de lejos.

La lista pide entre 3 y 5 unidades, con un máximo de 2 por tipo.

### Puntuación

- +100 por cada unidad correcta.
- −50 por cada unidad que falta.
- −25 por cada unidad que sobra.
- Si no falta nada: +300, más 5 puntos por cada segundo restante.
- La puntuación nunca es negativa. Si el tiempo se agota, es 0.

Una unidad entregada cuenta como correcta o como sobrante, nunca como ambas.

## Sonido

`AudioDirector` (en *Sistemas*) pone la ambientación y la música según el supermercado: **de día** en el Súper Caótico y **tétrica, de Halloween** en el Súper Embrujado. También reproduce los efectos del carrito, el brazo, la lista, la caja, el reloj, el menú y la pausa (que silencia el juego).

Los sonidos están en `Assets/_CarritoCaotico/05_Audio/`, repartidos en `Ambiente/`, `Musica/`, `Efectos/` y `UI/`. Cada uno se llama como un campo de `SoundLibrary` en snake_case (`sfxCajaPago` → `sfx_caja_pago`). Para añadir o cambiar uno, deja el archivo con ese nombre y regenera la escena: el generador lo conecta solo y avisa en la consola de los que faltan. Si falta un sonido, simplemente no suena.

## Estructura del proyecto

```
Assets/_CarritoCaotico/
├── 00_Scenes/               (generado) CarritoCaotico_Prueba.unity, CarritoCaotico_Halloween.unity y Backups/
├── 01_Scripts/              (CarritoCaotico.Runtime.asmdef)
│   ├── Core/
│   │   ├── GameManager.cs       Estados (Menú, Preparación, En juego, Compra entregada, Tiempo agotado), pausa y niveles
│   │   ├── GameSession.cs       Nivel actual y progreso guardado entre recargas de escena
│   │   ├── GameTimer.cs         Cuenta regresiva de la partida
│   │   └── LevelProgression.cs  Dificultad por nivel y esquemas de control
│   ├── Input/
│   │   └── GameInput.cs         Lectura de teclado/mandos con el New Input System y rol de cada PC en LAN
│   ├── Net/
│   │   ├── LanSession.cs        Conexión LAN con Unity Transport (anfitrión y cliente), cargas de escena
│   │   ├── LanDiscovery.cs      Anuncio y búsqueda de partidas en la wifi, e IP local más probable
│   │   ├── LanSync.cs           Instantáneas, interpolación, controles remotos y eventos de la partida
│   │   ├── LanProtocol.cs       Mensajes LAN y su formato binario
│   │   └── NetBuffer.cs         Escritura y lectura binaria compacta
│   ├── Cart/
│   │   ├── CartController.cs    Movimiento del carrito con fuerzas, derrape y masa con carga
│   │   ├── CartHealth.cs        Vida del carrito: daño por choques y explosión
│   │   ├── ExplosionEffect.cs   Fuego, humo y destello de la explosión
│   │   ├── CartContents.cs      Trigger de la canasta: contenido sin duplicados y peso
│   │   ├── GrabberArm.cs        Brazo y mano: área limitada, agarrar, soltar y lanzar
│   │   └── CameraController.cs  Cámara en tercera persona (órbita, suavizado, anticolisión) y primera persona del Empujador
│   ├── Shopping/
│   │   ├── Product.cs           Tipos de producto (enum estable), catálogo y producto físico
│   │   ├── ShoppingList.cs      Lista del Agarrador: requeridas, conseguidas y faltantes
│   │   ├── ShoppingRules.cs     Lógica pura: generación de la lista y evaluación del recibo
│   │   ├── Checkout.cs          Zona de entrega junto a la caja
│   │   └── StoreCatalog.cs      Supermercados: escenas, nombres, consejos e imágenes de carga
│   ├── Rivals/
│   │   ├── RivalManager.cs      Red de pasillos, objetivos y regla justa de los rivales
│   │   └── RivalShopper.cs      IA de un comprador rival
│   ├── Audio/
│   │   ├── SoundLibrary.cs      Biblioteca de sonidos (ScriptableObject); un campo por archivo
│   │   └── AudioDirector.cs     Ambiente, música y efectos según los eventos del juego
│   ├── Characters/
│   │   ├── CharacterChoice.cs   Personaje elegido por cada jugador (guardado en PlayerPrefs)
│   │   ├── CartCrew.cs          Activa en el carrito los personajes elegidos
│   │   ├── CharacterWaddle.cs   Bamboleo de los personajes del carrito según su velocidad
│   │   ├── FlipperFlap.cs       Brazos que se agitan al empujar (modelos en pose T)
│   │   └── ArmReach.cs          Brazo del Agarrador que señala la mano extensible
│   ├── Obstacles/
│   │   ├── SlipperyZone.cs      Charcos resbaladizos
│   │   └── MovingObstacle.cs    Reponedor y fantasmas que van y vienen
│   ├── Audio/
│   │   ├── GameAudio.cs         Hover del mouse, explosión, avisos LAN y modo de sonido (todo, efectos, no)
│   │   └── ProceduralSounds.cs  Sonido de explosión sintetizado por código
│   └── UI/
│       ├── GameUI.cs            HUD, lista, mensajes y recibo
│       ├── MenuUI.cs            Menú principal (tienda, nivel, tutorial, LAN, sonido) y menú de pausa
│       ├── UIButtonFeedback.cs  Animación de hover y pulsación de los botones, con sonido
│       ├── TutorialController.cs  Tutorial guiado
│       └── LoadingScreen.cs     Pantalla de carga persistente con ilustración y progreso
├── 02_Prefabs/              (generado) Producto_*.prefab, uno por tipo, y Comprador_Rival.prefab
├── 03_Materials/            (generado) materiales M_* y materiales físicos PM_*
├── 05_Audio/                ambientes, música, efectos e interfaz; SoundLibrary.asset (generado); Kenney/ con sonidos CC0 y CREDITOS.md
├── 06_Models/
│   ├── Pinguino/            personaje: modelo FBX con huesos y textura de color
│   ├── Zorro/               personaje con pose propia (no en pose T)
│   └── Cerdo/               personaje con esqueleto de nombres Mixamo
├── 07_Sprites/              texturas generadas (esquinas, círculo, check) y Kenney/ con botones, flechas y campo de texto
├── Editor/                  (CarritoCaotico.Editor.asmdef)
│   ├── CarritoSceneBuilder.cs  Menú Carrito > Construir escena de prueba
│   ├── ProductModels.cs     Modelos de producto hechos con varias primitivas
│   ├── CharacterModels.cs   Importación y colocación de los personajes con modelo 3D
│   ├── SoundAssets.cs       Rellena la biblioteca de sonidos con los archivos de 05_Audio
│   ├── HudBuilder.cs        Interfaz (tarjetas, teclas, lista, recibo, panel LAN) y texturas de UI
│   ├── AudioBuilder.cs      Objeto Audio de cada escena con los sonidos de Kenney
│   └── PlayerBuilder.cs     Menú Carrito > Compilar para Windows (LAN)
├── Resources/Carga/         imágenes de las pantallas de carga (capturas generadas e ilustraciones con IA)
└── Tests/
    ├── EditMode/            Reglas, protocolo LAN, controles por rol, modo de sonido, créditos y escenas generadas
    └── PlayMode/            Integración sobre la escena: LAN por 127.0.0.1, sonidos, animación de botones y jugabilidad
```

Las carpetas siguen la numeración de la asignatura (`00_Scenes`, `01_Scripts`, `02_Prefabs`, `03_Materials`, `04_Fonts`/`04_Shaders`, `05_Audio`, `06_Models`, `07_Sprites`). Las que aún no tienen contenido se crean cuando hagan falta. `Editor` y `Resources` conservan su nombre porque Unity los trata de forma especial. Los scripts se agrupan por responsabilidad, no por tipo técnico.

## Interfaz

Diseñada con la skill *UI/UX Pro Max*, que recomendó un estilo claymorphism para juegos casuales y una paleta de supermercado.

- **Estilo:** tarjetas redondeadas con borde de 3 px y sombra suave.
- **Colores:** verde fresco `#047857`, ámbar `#F59E0B` y rojo `#DC2626` sobre blanco. Cada jugador tiene su color: azul el Empujador y morado el Agarrador, como sus personajes.
- **Contraste:** al menos 4.5:1 en todo el texto.
- **Estados con texto o icono, no solo con color:** casilla con check y «¡Listo!», punto de la mano acompañado de texto, avisos «(!)» y palabras de estado en el recibo.
- **Temporizador:** una barra de tiempo restante que pasa de verde a ámbar (30 s) y a rojo (15 s), con un pulso suave en los últimos 10 s.
- **Controles:** se dibujan como teclas, con un medidor de carga que cambia de color según el peso.
- **Tipografía:** la fuente integrada de Unity en negrita. La skill propone Fredoka/Nunito, pero habría que importarlas.
- **Botones:** sprites del *UI Pack* de Kenney con relieve: amarillo para la acción principal, gris para el resto y flechas en los selectores.
- **Animación:** al pasar el mouse o seleccionar con teclado o mando, el botón crece un 6 %, sube 4 px y suena. Al pulsarlo se aplasta. El botón principal late suavemente. Funciona también en pausa.

## Pruebas

Desde Unity, abre *Window → General → Test Runner* y ejecuta las pestañas **EditMode** y **PlayMode**. Las pruebas PlayMode necesitan la escena generada.

Las pruebas LAN (`CarritoLanTests`) abren conexiones reales en `127.0.0.1` (puertos 47811 y 47812): una sesión hace de este PC y otra, sin escena, hace del otro PC.

Desde la línea de comandos:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml -logFile tests-editmode.log
```

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml -logFile tests-playmode.log
```

## Compilación para Windows

1. Genera la escena con **Carrito → Construir escena de prueba**. Así queda como escena 0 en *Build Settings*.
2. Ejecuta **Carrito → Compilar para Windows (LAN)**. El juego queda en `Builds/Windows/CarritoCaotico.exe` (carpeta excluida de Git).

El comando activa *Run In Background*: en LAN el anfitrión debe seguir simulando aunque su ventana no tenga el foco. Desde la línea de comandos:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe" -batchmode -projectPath . -executeMethod CarritoCaotico.EditorTools.PlayerBuilder.BuildWindowsFromCommandLine -logFile build-player.log
```

## Estado del desarrollo

MVP jugable de principio a fin: preparación, lista aleatoria, conducción, brazo, canasta con peso, lista actualizada, temporizador, caja, recibo y reinicio.

## Problemas conocidos

- Los rivales siguen una red fija de pasillos sin NavMesh: no esquivan el carrito de los jugadores, lo empujan.
- En un solo PC la separación de la lista es solo visual: está en el lado derecho de la pantalla y el Empujador no debe mirarla. En LAN el Empujador no la ve.
- La LAN es para dos PC en la misma red; no hay juego por Internet. El Empujador del PC cliente nota unos 100 ms de retraso al conducir, porque la física la calcula el anfitrión.
- Los productos dentro de la canasta pueden salir volando en curvas muy cerradas o al derrapar. Es intencionado, pero todavía no está equilibrado.
- Los personajes y el brazo son solo visuales, sin colisión, y pueden atravesar los estantes.
- La UI usa `UnityEngine.UI.Text` con la fuente integrada en lugar de TextMeshPro, para no tener que importar recursos.

## Próximas tareas

- Juego por Internet (Unity Relay) además de la LAN.
- Sonidos propios del carrito (ruedas, choques) y de la caja registradora.
- Más productos, ofertas y obstáculos dinámicos, como clientes o reponedores.
- Menú principal y selección de dificultad.
- Ajuste fino de la física de la canasta.

## Créditos

- Diseño y desarrollo: equipo de Carrito Caótico (Game Jam Better Together).
- Modelos 3D de los personajes (pingüino, zorro y cerdo) generados con [Tripo AI](https://studio.tripo3d.ai/).
- Efectos de sonido y ambientes generados con ElevenLabs Sound Effects v2, y música generada con Stable Audio 2.5, ambos a través de [fal.ai](https://fal.ai/).
- Desarrollado con la ayuda de Claude Code.

## Licencia

Todavía no se ha elegido una licencia. Hasta que se añada un archivo `LICENSE`, todos los derechos están reservados por sus autores.
