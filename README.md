# Carrito Caótico

Juego cooperativo local para dos jugadores, hecho en Unity para la Game Jam **Better Together** de 48 horas.

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
| Plataforma | Windows 10/11 (x64) |

No usa assets externos: todo se construye con primitivas, materiales de color plano y UI nativa.

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

La escena se genera en `Assets/_CarritoCaotico/Scenes/CarritoCaotico_Prueba.unity` y se añade como primera escena en *Build Settings*.

### Regenerar la escena de forma segura

Si la escena ya existe, el menú ofrece tres opciones:

- **Regenerar (con copia):** guarda una copia con fecha en `Assets/_CarritoCaotico/Scenes/Backups/` y después la reconstruye.
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

### General

| Acción | Tecla |
|---|---|
| Pausa / continuar | `Esc`, `P`, botón **Pausa** o `Start` |
| Siguiente nivel (tras completar la compra) | `N` o botón **Siguiente nivel** |
| Reintentar al terminar | `R`, botón **Reintentar** o `Start` |
| Volver al menú al terminar | `M` o botón **Menú** |

Con un solo mando, ese mando controla al Empujador y el Agarrador juega con el teclado.

### Controles que cambian

A partir del **nivel 4**, cada nivel puede cambiar los controles al azar. El esquema se anuncia en la tarjeta de preparación, los paneles muestran «¡Cambiados!» o «¡Al revés!» y las teclas del HUD se actualizan.

| Esquema | Empujador | Agarrador |
|---|---|---|
| Normal | `W` `S` `A` `D` + `Shift izq.` | Flechas + `Enter` + `Ctrl der.` |
| Intercambiado | Flechas + `Shift der.` | `W` `A` `S` `D` + `Espacio` + `F` |
| Al revés | Teclas normales con las direcciones invertidas | Teclas normales con las direcciones invertidas |

El esquema de cada nivel se mantiene al reintentarlo. Con el esquema intercambiado, también se intercambian los mandos.

## Cómo se juega

0. **Menú de inicio:** elige el nivel con `<` / `>` (solo los desbloqueados) y pulsa **Jugar**. El progreso se guarda en `PlayerPrefs`.
1. **Preparación (4 s):** el Agarrador lee la *Lista del Agarrador*, en el lado derecho de la pantalla.
2. **En juego (120 s):** el Empujador conduce hasta los pasillos y el Agarrador recoge los productos.
   - La mano se vuelve **amarilla** cuando hay un producto a su alcance y **verde** cuando sostiene uno.
   - Al soltar un producto cerca del carrito, cae dentro de la canasta.
   - Solo cuentan los productos que están **dentro de la canasta**. Uno sostenido por la mano no cuenta.
   - La carga hace el carrito más pesado: acelera y frena con más inercia.
3. **Caja:** al entrar en la zona verde junto a la caja, la compra se evalúa una sola vez y aparece el recibo.
4. **Tiempo agotado:** si el tiempo llega a 0, aparece **«¡La tienda cerró!»**.
5. **Siguiente nivel:** una compra completa (sin faltantes) desbloquea el nivel siguiente.

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

## Estructura del proyecto

```
Assets/_CarritoCaotico/
├── Scripts/                 (CarritoCaotico.Runtime.asmdef)
│   ├── GameInput.cs         Lectura de teclado/mandos con el New Input System
│   ├── Product.cs           12 tipos de producto (enum estable), catálogo y producto físico
│   ├── CartController.cs    Movimiento del carrito con fuerzas, derrape y masa con carga
│   ├── CameraController.cs  Cámara en tercera persona con órbita, suavizado y anticolisión
│   ├── GrabberArm.cs        Brazo y mano: área limitada, agarrar, soltar y lanzar
│   ├── CartContents.cs      Trigger de la canasta: contenido sin duplicados y peso
│   ├── ShoppingRules.cs     Lógica pura: generación de la lista y evaluación del recibo
│   ├── ShoppingList.cs      Lista del Agarrador: requeridas, conseguidas y faltantes
│   ├── GameTimer.cs         Cuenta regresiva de 120 s
│   ├── Checkout.cs          Zona de entrega junto a la caja
│   ├── GameManager.cs       Estados (Menú, Preparación, En juego, Compra entregada, Tiempo agotado), pausa y niveles
│   ├── GameSession.cs       Nivel actual y progreso guardado entre recargas de escena
│   ├── LevelProgression.cs  Dificultad por nivel y esquemas de control
│   ├── RivalManager.cs      Red de pasillos, objetivos y regla justa de los rivales
│   ├── RivalShopper.cs      IA de un comprador rival
│   ├── MenuUI.cs            Menú principal, selector de nivel y menú de pausa
│   └── GameUI.cs            HUD, lista, mensajes y recibo
├── Editor/                  (CarritoCaotico.Editor.asmdef)
│   ├── CarritoSceneBuilder.cs  Menú Carrito > Construir escena de prueba
│   ├── ProductModels.cs     Modelos de producto hechos con varias primitivas
│   └── HudBuilder.cs        Interfaz (tarjetas, teclas, lista, recibo) y texturas de UI
├── Tests/
│   ├── EditMode/            Pruebas de reglas de la lista y de la evaluación
│   └── PlayMode/            Pruebas de integración sobre la escena generada
├── Materials/               (generado) materiales M_* y materiales físicos PM_*
├── Prefabs/                 (generado) Producto_*.prefab, uno por tipo
├── UI/                      (generado) texturas de esquinas redondeadas, círculo y check
└── Scenes/                  (generado) CarritoCaotico_Prueba.unity y Backups/
```

## Interfaz

Diseñada con la skill *UI/UX Pro Max*, que recomendó un estilo claymorphism para juegos casuales y una paleta de supermercado.

- **Estilo:** tarjetas redondeadas con borde de 3 px y sombra suave.
- **Colores:** verde fresco `#047857`, ámbar `#F59E0B` y rojo `#DC2626` sobre blanco. Cada jugador tiene su color: azul el Empujador y morado el Agarrador, como sus personajes.
- **Contraste:** al menos 4.5:1 en todo el texto.
- **Estados con texto o icono, no solo con color:** casilla con check y «¡Listo!», punto de la mano acompañado de texto, avisos «(!)» y palabras de estado en el recibo.
- **Temporizador:** una barra de tiempo restante que pasa de verde a ámbar (30 s) y a rojo (15 s), con un pulso suave en los últimos 10 s.
- **Controles:** se dibujan como teclas, con un medidor de carga que cambia de color según el peso.
- **Tipografía:** la fuente integrada de Unity en negrita. La skill propone Fredoka/Nunito, pero habría que importarlas.

## Pruebas

Desde Unity, abre *Window → General → Test Runner* y ejecuta las pestañas **EditMode** y **PlayMode**. Las pruebas PlayMode necesitan la escena generada.

Desde la línea de comandos:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml -logFile tests-editmode.log
```

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml -logFile tests-playmode.log
```

## Compilación para Windows

1. Genera la escena con **Carrito → Construir escena de prueba**. Así queda como escena 0 en *Build Settings*.
2. Abre *File → Build Profiles*, elige **Windows** y pulsa **Build**.
3. Elige una carpeta `Builds/`, que está excluida de Git.

## Estado del desarrollo

MVP jugable de principio a fin: preparación, lista aleatoria, conducción, brazo, canasta con peso, lista actualizada, temporizador, caja, recibo y reinicio.

## Problemas conocidos

- Los rivales siguen una red fija de pasillos sin NavMesh: no esquivan el carrito de los jugadores, lo empujan.
- La separación de la lista entre jugadores es solo visual: la lista está en el lado derecho de la pantalla y el Empujador no debe mirarla.
- Los productos dentro de la canasta pueden salir volando en curvas muy cerradas o al derrapar. Es intencionado, pero todavía no está equilibrado.
- Los personajes y el brazo son solo visuales, sin colisión, y pueden atravesar los estantes.
- La UI usa `UnityEngine.UI.Text` con la fuente integrada en lugar de TextMeshPro, para no tener que importar recursos.

## Próximas tareas

- Pantalla dividida o UI separada para ocultar de verdad la lista al Empujador.
- Sonido: carrito, caja registradora y música.
- Más productos, ofertas y obstáculos dinámicos, como clientes o reponedores.
- Menú principal y selección de dificultad.
- Ajuste fino de la física de la canasta.

## Créditos

- Diseño y desarrollo: equipo de Carrito Caótico (Game Jam Better Together).
- Desarrollado con la ayuda de Claude Code.

## Licencia

Todavía no se ha elegido una licencia. Hasta que se añada un archivo `LICENSE`, todos los derechos están reservados por sus autores.
