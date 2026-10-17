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
| Liberar o capturar el cursor | `Esc` | — |

### Agarrador (Jugador 2)

| Acción | Teclado | Mando 2 |
|---|---|---|
| Mover el brazo | Flechas | Stick izquierdo |
| Agarrar / soltar | `Enter` (o `Enter` del teclado numérico) | Botón sur |
| Lanzar | `Ctrl derecho` | Gatillo derecho / botón oeste |

### General

| Acción | Tecla |
|---|---|
| Reiniciar al terminar | `R`, botón **Reiniciar** o `Start` |

Con un solo mando, ese mando controla al Empujador y el Agarrador juega con el teclado.

## Cómo se juega

1. **Preparación (4 s):** el Agarrador lee la *Lista del Agarrador*, en el lado derecho de la pantalla.
2. **En juego (120 s):** el Empujador conduce hasta los pasillos y el Agarrador recoge los productos.
   - La mano se vuelve **amarilla** cuando hay un producto a su alcance y **verde** cuando sostiene uno.
   - Al soltar un producto cerca del carrito, cae dentro de la canasta.
   - Solo cuentan los productos que están **dentro de la canasta**. Uno sostenido por la mano no cuenta.
   - La carga hace el carrito más pesado: acelera y frena con más inercia.
3. **Caja:** al entrar en la zona verde junto a la caja, la compra se evalúa una sola vez y aparece el recibo.
4. **Tiempo agotado:** si el tiempo llega a 0, aparece **«¡La tienda cerró!»**.

### Pasillos

| Pasillo | Cara izquierda | Cara derecha |
|---|---|---|
| 1 · Lácteos | Leche (cilindro blanco) | Huevos (cubo beige) |
| 2 · Desayuno | Cereal (cubo amarillo) | Lata (cilindro rojo) |
| 3 · Fruta y hogar | Sandía (esfera verde) | Papel higiénico (cilindro rosado) |

Hay 4 unidades de cada tipo. La lista pide entre 3 y 5 unidades, con un máximo de 2 por tipo.

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
│   ├── Product.cs           Tipos de producto (enum estable), catálogo y producto físico
│   ├── CartController.cs    Movimiento del carrito con fuerzas, derrape y masa con carga
│   ├── CameraController.cs  Cámara en tercera persona con órbita, suavizado y anticolisión
│   ├── GrabberArm.cs        Brazo y mano: área limitada, agarrar, soltar y lanzar
│   ├── CartContents.cs      Trigger de la canasta: contenido sin duplicados y peso
│   ├── ShoppingRules.cs     Lógica pura: generación de la lista y evaluación del recibo
│   ├── ShoppingList.cs      Lista del Agarrador: requeridas, conseguidas y faltantes
│   ├── GameTimer.cs         Cuenta regresiva de 120 s
│   ├── Checkout.cs          Zona de entrega junto a la caja
│   ├── GameManager.cs       Estados: Preparación, En juego, Compra entregada, Tiempo agotado
│   └── GameUI.cs            HUD, lista, mensajes y recibo
├── Editor/                  (CarritoCaotico.Editor.asmdef)
│   └── CarritoSceneBuilder.cs  Menú Carrito > Construir escena de prueba
├── Tests/
│   ├── EditMode/            Pruebas de reglas de la lista y de la evaluación
│   └── PlayMode/            Pruebas de integración sobre la escena generada
├── Materials/               (generado) materiales M_* y materiales físicos PM_*
├── Prefabs/                 (generado) Producto_*.prefab
└── Scenes/                  (generado) CarritoCaotico_Prueba.unity y Backups/
```

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
