using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CarritoCaotico.EditorTools
{
    /// <summary>
    /// Construye la interfaz de Carrito Caótico con estilo "claymorphism" para juegos casuales:
    /// tarjetas redondeadas con borde grueso y sombra suave, paleta de supermercado (verde fresco
    /// + ámbar), contraste mínimo 4.5:1 en texto y estados que nunca dependen solo del color.
    /// Las texturas de la interfaz (esquinas redondeadas, círculo y check) se generan aquí.
    /// </summary>
    internal static class HudBuilder
    {
        const string UiFolder = CarritoSceneBuilder.RootFolder + "/UI";

        // ---------- Tokens de diseño ----------
        static readonly Color32 Ink = new Color32(15, 23, 42, 255);          // texto principal y bordes
        static readonly Color32 MutedInk = new Color32(71, 85, 105, 255);    // texto secundario (7:1 sobre blanco)
        static readonly Color32 Card = new Color32(255, 255, 255, 255);
        static readonly Color32 Paper = new Color32(255, 253, 247, 255);
        static readonly Color32 Neutral = new Color32(226, 232, 240, 255);   // pistas de barras, casillas vacías
        static readonly Color32 KeyCap = new Color32(241, 245, 249, 255);
        static readonly Color32 Success = new Color32(4, 120, 87, 255);      // verde fresco oscuro (5.5:1 con blanco)
        static readonly Color32 Warning = new Color32(245, 158, 11, 255);    // ámbar (8:1 con tinta)
        static readonly Color32 WarningText = new Color32(180, 83, 9, 255);
        static readonly Color32 Danger = new Color32(220, 38, 38, 255);
        static readonly Color32 PusherBlue = new Color32(37, 99, 235, 255);  // Jugador 1 (igual que su personaje)
        static readonly Color32 GrabberPurple = new Color32(124, 58, 237, 255); // Jugador 2
        static readonly Color32 Shadow = new Color32(15, 23, 42, 60);

        const float BorderWidth = 3f;
        const float ShadowOffset = 6f;

        static Font font;
        static Sprite rounded;
        static Sprite circle;
        static Sprite check;
        static int elementCount;

        public static int ElementCount => elementCount;

        /// <summary>Construye el HUD, el menú principal y el menú de pausa.</summary>
        public static (GameUI ui, MenuUI menu) Build(Font uiFont)
        {
            font = uiFont;
            elementCount = 0;
            EnsureSprites();

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform root = canvasGo.transform;
            var refs = new Dictionary<string, object>();
            var menuRefs = new Dictionary<string, object>();

            // Paneles de juego en un contenedor que se oculta en el menú principal.
            Transform game = Container(root, "Juego");
            refs["hudRoot"] = game.gameObject;
            BuildTimer(game, refs);
            BuildStatusChips(game, refs, menuRefs);
            BuildPusherPanel(game, refs);
            ShoppingListRow[] rows = BuildGrabberPanel(game, refs);
            BuildMessage(game, refs);

            BuildPreparation(root, refs);
            BuildReceipt(root, refs);
            BuildPause(root, menuRefs);
            BuildMenu(root, menuRefs);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var ui = canvasGo.AddComponent<GameUI>();
            Wire(ui, refs);
            WireRows(ui, rows);
            var menu = canvasGo.AddComponent<MenuUI>();
            Wire(menu, menuRefs);
            return (ui, menu);
        }

        static Transform Container(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return go.transform;
        }

        // ======================================================================
        // Secciones
        // ======================================================================

        static void BuildTimer(Transform root, Dictionary<string, object> refs)
        {
            Image fill = CardBox(root, "Temporizador", new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(300f, 120f), Success, 1f);
            Text digits = Label(fill.transform, "Tiempo", "2:00", 66, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(digits.rectTransform, new Vector2(0f, 0.25f), Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, -4f));

            // Barra de tiempo restante: progreso lineal, sin depender solo del color.
            Image track = Box(fill.transform, "Barra", new Color(1f, 1f, 1f, 0.35f), circleSprite: false, ppu: 3f);
            Anchor(track.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 16f), new Vector2(-22f, 30f));
            Image bar = Box(track.transform, "Relleno", Color.white, circleSprite: false, ppu: 3f);
            Anchor(bar.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            refs["timerText"] = digits;
            refs["timerBackground"] = fill;
            refs["timerBarFill"] = bar.rectTransform;
        }

        static void BuildStatusChips(Transform root, Dictionary<string, object> refs, Dictionary<string, object> menuRefs)
        {
            Image level = CardBox(root, "Nivel", new Vector2(0.5f, 1f), new Vector2(-265f, -24f), new Vector2(190f, 52f), Card, 1.5f);
            Text levelText = Label(level.transform, "Texto", "NIVEL 1", 24, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(levelText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Button pause = CardButton(root, "Boton_Pausa", "Pausa", "P", Card, Ink, new Vector2(0.5f, 1f), new Vector2(-265f, -88f), new Vector2(190f, 50f), 22, 1.5f);

            Image rivals = CardBox(root, "Rivales", new Vector2(0.5f, 1f), new Vector2(275f, -24f), new Vector2(210f, 52f), Card, 1.5f);
            Text rivalsText = Label(rivals.transform, "Texto", "Sin rivales", 19, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(rivalsText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            rivalsText.resizeTextForBestFit = true;
            rivalsText.resizeTextMinSize = 13;
            rivalsText.resizeTextMaxSize = 19;
            rivalsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            rivalsText.verticalOverflow = VerticalWrapMode.Truncate;

            refs["levelText"] = levelText;
            refs["rivalsText"] = rivalsText;
            menuRefs["hudPauseButton"] = pause;
        }

        static void BuildPusherPanel(Transform root, Dictionary<string, object> refs)
        {
            Image fill = CardBox(root, "Panel_Empujador", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(560f, 312f), Card, 1f);
            var (pusherChip, pusherChipText) = Header(fill.transform, "EMPUJADOR", "Jugador 1", PusherBlue, 64f);

            float y = -84f;
            var keys = new List<Text>();
            keys.AddRange(KeyRow(fill.transform, y, ("W", "Acelerar", 48f), ("S", "Frenar / atrás", 48f)));
            keys.AddRange(KeyRow(fill.transform, y - 46f, ("A  D", "Girar", 72f), ("Shift izq.", "Derrapar", 0f)));
            KeyRow(fill.transform, y - 92f, ("Mouse", "Cámara", 0f), ("Esc / P", "Pausa", 0f));
            refs["pusherKeyLabels"] = keys.ToArray();
            refs["pusherChip"] = pusherChip;
            refs["pusherChipText"] = pusherChipText;

            Text caption = Label(fill.transform, "Carga_Titulo", "CARGA DEL CARRITO", 18, MutedInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(caption.rectTransform, new Vector2(20f, -232f), new Vector2(260f, 26f));
            Text value = Label(fill.transform, "Carga", "0.0 kg  ·  0 productos", 22, Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            Place(value.rectTransform, new Vector2(240f, -232f), new Vector2(300f, 26f));

            Image track = Box(fill.transform, "Medidor", Neutral, circleSprite: false, ppu: 3f);
            Place(track.rectTransform, new Vector2(20f, -266f), new Vector2(520f, 18f));
            Image bar = Box(track.transform, "Relleno", Success, circleSprite: false, ppu: 3f);
            Anchor(bar.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            refs["cargoText"] = value;
            refs["cargoBarFill"] = bar.rectTransform;
            refs["cargoBarImage"] = bar;
        }

        static ShoppingListRow[] BuildGrabberPanel(Transform root, Dictionary<string, object> refs)
        {
            Image fill = CardBox(root, "Panel_Agarrador", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(540f, 812f), Card, 1f);
            var (grabberChip, grabberChipText) = Header(fill.transform, "Lista del Agarrador", "Jugador 2", GrabberPurple, 72f, 32);
            refs["grabberChip"] = grabberChip;
            refs["grabberChipText"] = grabberChipText;

            Text note = Label(fill.transform, "Aviso", "Solo el Agarrador mira esta lista y guía al Empujador.", 19, MutedInk, TextAnchor.MiddleLeft, FontStyle.Italic);
            Place(note.rectTransform, new Vector2(22f, -84f), new Vector2(500f, 28f));

            // La lista nunca tiene más de LevelProgression.MaxListUnits tipos distintos.
            int rowCount = LevelProgression.MaxListUnits;
            var rows = new ShoppingListRow[rowCount];
            for (int i = 0; i < rowCount; i++)
                rows[i] = ListRow(fill.transform, i, -120f - i * 46f);

            Text extra = Label(fill.transform, "Sobrantes", "", 20, WarningText, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(extra.rectTransform, new Vector2(22f, -492f), new Vector2(500f, 48f));
            extra.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text progress = Label(fill.transform, "Progreso", "Conseguido: 0 de 0 unidades", 22, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(progress.rectTransform, new Vector2(22f, -544f), new Vector2(500f, 30f));
            Image track = Box(fill.transform, "Barra_Progreso", Neutral, circleSprite: false, ppu: 3f);
            Place(track.rectTransform, new Vector2(22f, -578f), new Vector2(496f, 18f));
            Image bar = Box(track.transform, "Relleno", Success, circleSprite: false, ppu: 3f);
            Anchor(bar.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            // Estado de la mano: punto de color + texto.
            Image handChip = Box(fill.transform, "Estado_Mano", KeyCap, circleSprite: false, ppu: 2f);
            Place(handChip.rectTransform, new Vector2(22f, -606f), new Vector2(496f, 46f));
            Image dot = Box(handChip.transform, "Punto", MutedInk, circleSprite: true, ppu: 1f);
            Place(dot.rectTransform, new Vector2(16f, -13f), new Vector2(20f, 20f));
            Text hand = Label(handChip.transform, "Texto", "Mano vacía: acércala a un producto", 20, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            Anchor(hand.rectTransform, Vector2.zero, Vector2.one, new Vector2(46f, 0f), new Vector2(-10f, 0f));
            // El texto se reduce si no cabe, en lugar de salirse del panel.
            hand.horizontalOverflow = HorizontalWrapMode.Wrap;
            hand.verticalOverflow = VerticalWrapMode.Truncate;
            hand.resizeTextForBestFit = true;
            hand.resizeTextMinSize = 14;
            hand.resizeTextMaxSize = 20;

            // Controles del Agarrador.
            Image divider = Box(fill.transform, "Separador", Neutral, circleSprite: false, ppu: 8f);
            Place(divider.rectTransform, new Vector2(22f, -666f), new Vector2(496f, 3f));
            var grabberKeys = new List<Text>();
            grabberKeys.AddRange(KeyRow(fill.transform, -678f, ("Flechas", "Mover brazo", 112f)));
            grabberKeys.AddRange(KeyRow(fill.transform, -720f, ("Enter", "Agarrar / soltar", 112f)));
            grabberKeys.AddRange(KeyRow(fill.transform, -762f, ("Ctrl der.", "Lanzar", 112f)));
            refs["grabberKeyLabels"] = grabberKeys.ToArray();

            refs["listExtraText"] = extra;
            refs["listProgressText"] = progress;
            refs["listProgressFill"] = bar.rectTransform;
            refs["handText"] = hand;
            refs["handDot"] = dot;
            return rows;
        }

        static ShoppingListRow ListRow(Transform parent, int index, float y)
        {
            var go = new GameObject($"Fila_{index + 1}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, new Vector2(22f, y), new Vector2(496f, 42f));

            Image box = Box(go.transform, "Casilla", Neutral, circleSprite: false, ppu: 2f);
            Place(box.rectTransform, new Vector2(0f, -5f), new Vector2(32f, 32f));
            var mark = new GameObject("Check", typeof(RectTransform)).AddComponent<Image>();
            mark.transform.SetParent(box.transform, false);
            mark.sprite = check;
            mark.color = Color.white;
            mark.raycastTarget = false;
            Anchor(mark.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            mark.enabled = false;
            elementCount++;

            Text name = Label(go.transform, "Nombre", "1 × Producto", 24, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(name.rectTransform, new Vector2(48f, 0f), new Vector2(330f, 42f));

            Image pill = Box(go.transform, "Cantidad", Neutral, circleSprite: false, ppu: 2f);
            Place(pill.rectTransform, new Vector2(390f, -5f), new Vector2(106f, 32f));
            Text count = Label(pill.transform, "Texto", "0/1", 20, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(count.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return new ShoppingListRow { root = go, checkBox = box, checkMark = mark, nameText = name, countPill = pill, countText = count };
        }

        static void BuildMessage(Transform root, Dictionary<string, object> refs)
        {
            Image fill = CardBox(root, "Mensaje", new Vector2(0.5f, 1f), new Vector2(10f, -164f), new Vector2(740f, 78f), Ink, 1f);
            Text text = Label(fill.transform, "Texto", "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            refs["messagePanel"] = fill.transform.parent.gameObject;
            refs["messageText"] = text;
            refs["messageBackground"] = fill;
        }

        static void BuildPreparation(Transform root, Dictionary<string, object> refs)
        {
            Image fill = CardBox(root, "Preparacion", new Vector2(0.5f, 0.5f), new Vector2(-110f, 10f), new Vector2(700f, 340f), Card, 1f);
            Text title = Label(fill.transform, "Titulo", "Nivel 1 · ¡Prepárense!", 46, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(20f, -22f), new Vector2(660f, 60f));
            Text count = Label(fill.transform, "Cuenta", "4", 110, Success, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(count.rectTransform, new Vector2(20f, -80f), new Vector2(660f, 124f));
            Text hint = Label(fill.transform, "Pista", "Agarrador: lee la lista y guía al Empujador.", 23, MutedInk, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place(hint.rectTransform, new Vector2(30f, -214f), new Vector2(640f, 104f));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.lineSpacing = 1.15f;
            refs["preparationPanel"] = fill.transform.parent.gameObject;
            refs["preparationTitleText"] = title;
            refs["preparationCountText"] = count;
            refs["preparationHintText"] = hint;
        }

        static void BuildReceipt(Transform root, Dictionary<string, object> refs)
        {
            // Fondo atenuado a pantalla completa (el test busca el objeto "Recibo").
            var overlayGo = new GameObject("Recibo", typeof(RectTransform));
            overlayGo.transform.SetParent(root, false);
            var overlay = overlayGo.AddComponent<Image>();
            overlay.color = new Color(0.06f, 0.09f, 0.16f, 0.62f);
            Anchor(overlay.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            elementCount++;

            Image paper = CardBox(overlayGo.transform, "Papel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 920f), Paper, 1f);
            Image header = Box(paper.transform, "Cabecera", Success, circleSprite: false, ppu: 1f);
            Anchor(header.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(10f, -96f), new Vector2(-10f, -10f));
            Text title = Label(header.transform, "Titulo", "Recibo · Súper Caótico", 42, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(title.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text time = Label(paper.transform, "Tiempo", "", 22, MutedInk, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place(time.rectTransform, new Vector2(30f, -108f), new Vector2(800f, 34f));

            // Tabla por columnas: cada columna es un Text con el mismo interlineado.
            float tableTop = -156f;
            Image tableBack = Box(paper.transform, "Tabla", Card, circleSprite: false, ppu: 1.5f);
            Place(tableBack.rectTransform, new Vector2(30f, tableTop), new Vector2(800f, 396f));
            Text ColumnHeader(string label, float x, float width, TextAnchor align)
            {
                Text h = Label(tableBack.transform, "Encabezado_" + label, label.ToUpperInvariant(), 18, MutedInk, align, FontStyle.Bold);
                Place(h.rectTransform, new Vector2(x, -12f), new Vector2(width, 26f));
                return h;
            }
            Text Column(string name, float x, float width, TextAnchor align, FontStyle style)
            {
                Text c = Label(tableBack.transform, name, "", 24, Ink, align, style);
                Place(c.rectTransform, new Vector2(x, -46f), new Vector2(width, 340f));
                c.lineSpacing = 1.15f;
                return c;
            }
            ColumnHeader("Producto", 24f, 300f, TextAnchor.MiddleLeft);
            ColumnHeader("Pedido", 330f, 130f, TextAnchor.MiddleCenter);
            ColumnHeader("Entregado", 460f, 150f, TextAnchor.MiddleCenter);
            ColumnHeader("Estado", 610f, 170f, TextAnchor.MiddleRight);
            Text names = Column("Productos", 24f, 300f, TextAnchor.UpperLeft, FontStyle.Bold);
            Text required = Column("Pedidos", 330f, 130f, TextAnchor.UpperCenter, FontStyle.Normal);
            Text delivered = Column("Entregados", 460f, 150f, TextAnchor.UpperCenter, FontStyle.Normal);
            Text status = Column("Estados", 610f, 170f, TextAnchor.UpperRight, FontStyle.Bold);

            // Tres casillas de resumen.
            float tilesTop = tableTop - 412f;
            Text Tile(string label, float x, Color numberColor)
            {
                Image tile = Box(paper.transform, "Resumen_" + label, Card, circleSprite: false, ppu: 1.5f);
                Place(tile.rectTransform, new Vector2(x, tilesTop), new Vector2(256f, 104f));
                Text number = Label(tile.transform, "Numero", "0", 46, numberColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                Anchor(number.rectTransform, new Vector2(0f, 0.35f), Vector2.one, Vector2.zero, Vector2.zero);
                Text caption = Label(tile.transform, "Etiqueta", label, 19, MutedInk, TextAnchor.MiddleCenter, FontStyle.Bold);
                Anchor(caption.rectTransform, Vector2.zero, new Vector2(1f, 0.38f), new Vector2(0f, 4f), Vector2.zero);
                return number;
            }
            Text correct = Tile("Correctos", 30f, Success);
            Text missing = Tile("Faltantes", 302f, Danger);
            Text extra = Tile("Sobrantes", 574f, WarningText);

            Text scoreLabel = Label(paper.transform, "Puntuacion_Titulo", "PUNTUACIÓN", 20, MutedInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(scoreLabel.rectTransform, new Vector2(40f, tilesTop - 124f), new Vector2(200f, 60f));
            Text score = Label(paper.transform, "Puntuacion", "0", 56, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(score.rectTransform, new Vector2(196f, tilesTop - 120f), new Vector2(200f, 66f));

            Image badge = Box(paper.transform, "Resultado", Success, circleSprite: false, ppu: 1f);
            Place(badge.rectTransform, new Vector2(400f, tilesTop - 122f), new Vector2(430f, 62f));
            Text result = Label(badge.transform, "Texto", "", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(result.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));

            // Botones con atajo de teclado visible. El de "Siguiente nivel" solo aparece si la
            // compra está completa; el grupo horizontal recoloca los demás automáticamente.
            var buttonsGo = new GameObject("Botones", typeof(RectTransform));
            buttonsGo.transform.SetParent(paper.transform, false);
            Anchor((RectTransform)buttonsGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 34f), new Vector2(-20f, 120f));
            var layout = buttonsGo.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 22f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            Button next = CardButton(buttonsGo.transform, "Boton_Siguiente", "Siguiente nivel", "N", Warning, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 80f), 26, 1f);
            Button retry = CardButton(buttonsGo.transform, "Boton_Reintentar", "Reintentar", "R", Card, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 80f), 26, 1f);
            Button menu = CardButton(buttonsGo.transform, "Boton_Menu", "Menú", "M", Card, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 80f), 26, 1f);

            overlayGo.SetActive(false);

            refs["receiptPanel"] = overlayGo;
            refs["receiptTitleText"] = title;
            refs["receiptTimeText"] = time;
            refs["receiptNamesText"] = names;
            refs["receiptRequiredText"] = required;
            refs["receiptDeliveredText"] = delivered;
            refs["receiptStatusText"] = status;
            refs["receiptCorrectText"] = correct;
            refs["receiptMissingText"] = missing;
            refs["receiptExtraText"] = extra;
            refs["receiptScoreText"] = score;
            refs["receiptResultText"] = result;
            refs["receiptResultBadge"] = badge;
            refs["restartButton"] = retry;
            refs["nextLevelButton"] = next;
            refs["menuButton"] = menu;
        }

        static void BuildPause(Transform root, Dictionary<string, object> refs)
        {
            Transform overlay = Overlay(root, "Pausa", new Color(0.06f, 0.09f, 0.16f, 0.62f));
            Image card = CardBox(overlay, "Tarjeta", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 470f), Card, 1f);
            Text title = Label(card.transform, "Titulo", "Pausa", 58, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(20f, -26f), new Vector2(520f, 74f));
            Text hint = Label(card.transform, "Pista", "El tiempo y los rivales están detenidos.", 22, MutedInk, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place(hint.rectTransform, new Vector2(20f, -98f), new Vector2(520f, 32f));

            Button resume = CardButton(card.transform, "Boton_Continuar", "Continuar", "Esc", Warning, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(420f, 80f), 28, 1f);
            Button restart = CardButton(card.transform, "Boton_Reintentar", "Reintentar nivel", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(420f, 70f), 26, 1f);
            Button menu = CardButton(card.transform, "Boton_Menu", "Menú principal", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(420f, 70f), 26, 1f);
            overlay.gameObject.SetActive(false);

            refs["pausePanel"] = overlay.gameObject;
            refs["resumeButton"] = resume;
            refs["pauseRestartButton"] = restart;
            refs["pauseMenuButton"] = menu;
        }

        static void BuildMenu(Transform root, Dictionary<string, object> refs)
        {
            // Fondo verde claro de supermercado con franjas suaves de "baldosas".
            Transform overlay = Overlay(root, "Menu_Principal", new Color32(236, 253, 245, 255));
            for (int i = 0; i < 8; i++)
            {
                Image stripe = Box(overlay, "Franja", new Color32(209, 250, 229, 255), circleSprite: false, ppu: 8f);
                Anchor(stripe.rectTransform, new Vector2(i / 8f, 0f), new Vector2(i / 8f + 0.0625f, 1f), Vector2.zero, Vector2.zero);
            }

            Image card = CardBox(overlay, "Tarjeta", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(960f, 780f), Card, 1f);
            Image band = Box(card.transform, "Cabecera", Success, circleSprite: false, ppu: 1f);
            Anchor(band.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(10f, -186f), new Vector2(-10f, -10f));
            Text title = Label(band.transform, "Titulo", "Carrito Caótico", 88, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0f, 0.32f), Vector2.one, Vector2.zero, Vector2.zero);
            Text subtitle = Label(band.transform, "Subtitulo", "Cooperativo local para dos  ·  Better Together", 26, new Color32(209, 250, 229, 255), TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(subtitle.rectTransform, Vector2.zero, new Vector2(1f, 0.36f), new Vector2(0f, 12f), Vector2.zero);

            // Selector de nivel.
            Text caption = Label(card.transform, "Nivel_Titulo", "ELIGE EL NIVEL", 20, MutedInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(caption.rectTransform, new Vector2(20f, -210f), new Vector2(920f, 30f));
            Button previous = CardButton(card.transform, "Nivel_Anterior", "<", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(-230f, -250f), new Vector2(80f, 80f), 40, 1.5f);
            Image levelBox = Box(card.transform, "Nivel", KeyCap, circleSprite: false, ppu: 1.5f);
            Anchor(levelBox.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-170f, -330f), new Vector2(170f, -250f));
            Text levelText = Label(levelBox.transform, "Texto", "Nivel 1", 44, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(levelText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Button next = CardButton(card.transform, "Nivel_Siguiente", ">", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(230f, -250f), new Vector2(80f, 80f), 40, 1.5f);
            Text detail = Label(card.transform, "Detalle", "", 22, MutedInk, TextAnchor.UpperCenter, FontStyle.Normal);
            Place(detail.rectTransform, new Vector2(40f, -348f), new Vector2(880f, 64f));
            detail.lineSpacing = 1.2f;

            Button play = CardButton(card.transform, "Boton_Jugar", "Jugar", "Enter", Warning, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(460f, 96f), 40, 1f);
            Button quit = CardButton(card.transform, "Boton_Salir", "Salir", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -546f), new Vector2(260f, 64f), 26, 1.5f);

            // Resumen de roles.
            Text roles = Label(card.transform, "Roles",
                "<b>Jugador 1 · Empujador:</b> conduce con WASD y mira con el mouse.\n" +
                "<b>Jugador 2 · Agarrador:</b> lee la lista y agarra con flechas + Enter.\n" +
                "Cuidado: los compradores rivales se llevan productos y los controles pueden cambiar.",
                21, Ink, TextAnchor.UpperCenter, FontStyle.Normal);
            Place(roles.rectTransform, new Vector2(40f, -640f), new Vector2(880f, 110f));
            roles.lineSpacing = 1.2f;

            overlay.gameObject.SetActive(false);
            refs["menuPanel"] = overlay.gameObject;
            refs["playButton"] = play;
            refs["previousLevelButton"] = previous;
            refs["nextLevelButton"] = next;
            refs["levelText"] = levelText;
            refs["levelDetailText"] = detail;
            refs["quitButton"] = quit;
        }

        static Transform Overlay(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            Anchor(image.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            elementCount++;
            return go.transform;
        }

        /// <summary>
        /// Botón "clay" con atajo de teclado opcional. El componente Button va en la raíz de la
        /// tarjeta, así al ocultar el botón se oculta también su sombra y su borde.
        /// </summary>
        static Button CardButton(Transform parent, string name, string label, string key, Color fill, Color textColor,
            Vector2 anchor, Vector2 position, Vector2 size, int fontSize, float ppu)
        {
            Image body = CardBox(parent, name, anchor, position, size, fill, ppu);
            GameObject root = body.transform.parent.gameObject;
            var button = root.AddComponent<Button>();
            button.targetGraphic = body;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.95f, 1f);
            colors.selectedColor = new Color(0.94f, 0.96f, 1f);
            colors.pressedColor = new Color(0.78f, 0.82f, 0.88f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            float keyWidth = key == null ? 0f : Mathf.Max(46f, 22f + key.Length * 13f);
            Text text = Label(body.transform, "Texto", label, fontSize, textColor, key == null ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(key == null ? 12f : 28f, 0f), new Vector2(key == null ? -12f : -(keyWidth + 30f), 0f));
            if (key != null)
            {
                Image keyCap = KeyCapChip(body.transform, key, keyWidth, out _);
                Anchor(keyCap.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-keyWidth - 20f, -19f), new Vector2(-20f, 19f));
            }
            return button;
        }

        // ======================================================================
        // Componentes reutilizables
        // ======================================================================

        /// <summary>
        /// Tarjeta "clay": sombra desplazada + borde de tinta + relleno. Devuelve el relleno,
        /// que es donde se coloca el contenido. pivot y anclaje coinciden con <paramref name="anchor"/>.
        /// </summary>
        static Image CardBox(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color fill, float ppu)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image shadow = Box(go.transform, "Sombra", Shadow, circleSprite: false, ppu: ppu);
            Anchor(shadow.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -ShadowOffset), new Vector2(0f, -ShadowOffset));
            Image border = Box(go.transform, "Borde", Ink, circleSprite: false, ppu: ppu);
            Anchor(border.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image body = Box(go.transform, "Relleno", fill, circleSprite: false, ppu: ppu * 1.1f);
            Anchor(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(BorderWidth, BorderWidth), new Vector2(-BorderWidth, -BorderWidth));
            body.raycastTarget = true;
            return body;
        }

        /// <summary>Cabecera de color con título y chip del jugador.</summary>
        static (Image chip, Text text) Header(Transform parent, string title, string player, Color color, float height, int titleSize = 28)
        {
            Image band = Box(parent, "Cabecera", color, circleSprite: false, ppu: 1.2f);
            Anchor(band.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(8f, -8f - height), new Vector2(-8f, -8f));
            Text label = Label(band.transform, "Titulo", title, titleSize, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-150f, 0f));

            Image chip = Box(band.transform, "Jugador", Color.white, circleSprite: false, ppu: 2f);
            Anchor(chip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-142f, -17f), new Vector2(-14f, 17f));
            Text chipText = Label(chip.transform, "Texto", player, 19, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(chipText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return (chip, chipText);
        }

        /// <summary>
        /// Fila de controles: teclas dibujadas como teclas reales y su acción. width = 0 calcula
        /// el ancho de la tecla según la etiqueta. Devuelve los textos de las teclas.
        /// </summary>
        static List<Text> KeyRow(Transform parent, float y, params (string key, string action, float width)[] items)
        {
            var labels = new List<Text>();
            float x = 20f;
            foreach (var (keyLabel, action, fixedWidth) in items)
            {
                float width = fixedWidth > 0f ? fixedWidth : Mathf.Max(42f, 18f + keyLabel.Length * 13f);
                Image key = KeyCapChip(parent, keyLabel, width, out Text keyText);
                Place(key.rectTransform, new Vector2(x, y), new Vector2(width, 38f));
                labels.Add(keyText);
                x += width + 10f;

                Text text = Label(parent, "Accion_" + action, action, 21, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
                float textWidth = 22f + action.Length * 10.5f;
                Place(text.rectTransform, new Vector2(x, y), new Vector2(textWidth, 38f));
                x += textWidth + 18f;
            }
            return labels;
        }

        static Image KeyCapChip(Transform parent, string label, float width, out Text text)
        {
            // Tecla: borde de tinta + cara clara desplazada hacia arriba (efecto de relieve).
            Image edge = Box(parent, "Tecla_" + label, Ink, circleSprite: false, ppu: 2.5f);
            edge.rectTransform.sizeDelta = new Vector2(width, 38f);
            Image face = Box(edge.transform, "Cara", KeyCap, circleSprite: false, ppu: 2.75f);
            Anchor(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 5f), new Vector2(-2f, -2f));
            text = Label(face.transform, "Texto", label, 19, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return edge;
        }

        static Image Box(Transform parent, string name, Color color, bool circleSprite, float ppu)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.sprite = circleSprite ? circle : rounded;
            if (!circleSprite)
            {
                image.type = Image.Type.Sliced;
                // Mayor valor = esquinas más pequeñas.
                image.pixelsPerUnitMultiplier = ppu;
            }
            elementCount++;
            return image;
        }

        static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor alignment, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            elementCount++;
            return label;
        }

        static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        // ======================================================================
        // Referencias
        // ======================================================================

        static void Wire(Object target, Dictionary<string, object> refs)
        {
            var serialized = new SerializedObject(target);
            foreach (var pair in refs)
            {
                SerializedProperty property = serialized.FindProperty(pair.Key);
                if (property == null)
                {
                    Debug.LogError($"[Carrito] {target.GetType().Name} no tiene el campo '{pair.Key}'.");
                    continue;
                }
                if (pair.Value is Object[] array)
                {
                    property.arraySize = array.Length;
                    for (int i = 0; i < array.Length; i++)
                        property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                }
                else
                {
                    property.objectReferenceValue = (Object)pair.Value;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireRows(GameUI ui, ShoppingListRow[] rows)
        {
            var serialized = new SerializedObject(ui);
            SerializedProperty list = serialized.FindProperty("listRows");
            list.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                element.FindPropertyRelative("checkBox").objectReferenceValue = rows[i].checkBox;
                element.FindPropertyRelative("checkMark").objectReferenceValue = rows[i].checkMark;
                element.FindPropertyRelative("nameText").objectReferenceValue = rows[i].nameText;
                element.FindPropertyRelative("countPill").objectReferenceValue = rows[i].countPill;
                element.FindPropertyRelative("countText").objectReferenceValue = rows[i].countText;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ======================================================================
        // Texturas generadas (idempotente: se reescriben con el mismo contenido)
        // ======================================================================

        static void EnsureSprites()
        {
            if (!AssetDatabase.IsValidFolder(UiFolder))
                AssetDatabase.CreateFolder(CarritoSceneBuilder.RootFolder, "UI");

            rounded = EnsureSprite("UI_Redondeado", 96, RoundedAlpha, 32);
            circle = EnsureSprite("UI_Circulo", 64, CircleAlpha, 0);
            check = EnsureSprite("UI_Check", 64, CheckAlpha, 0);
        }

        static Sprite EnsureSprite(string name, int size, Func<float, float, int, float> alpha, int border)
        {
            string path = $"{UiFolder}/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f, size));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            texture.SetPixels32(pixels);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", path);
            bool changed = !File.Exists(fullPath) || !BytesEqual(File.ReadAllBytes(fullPath), png);
            if (changed)
            {
                File.WriteAllBytes(fullPath, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = importer.textureType != TextureImporterType.Sprite
                         || importer.spriteBorder != new Vector4(border, border, border, border)
                         || importer.mipmapEnabled
                         || !importer.alphaIsTransparency;
            if (dirty)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>Rectángulo con esquinas de radio 30 px (sobre 96 px) y borde suavizado.</summary>
        static float RoundedAlpha(float x, float y, int size)
        {
            const float radius = 30f;
            float cx = Mathf.Clamp(x, radius, size - radius);
            float cy = Mathf.Clamp(y, radius, size - radius);
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            return radius - distance + 0.5f;
        }

        static float CircleAlpha(float x, float y, int size)
        {
            float r = size * 0.5f - 1f;
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(size * 0.5f, size * 0.5f));
            return r - distance + 0.5f;
        }

        /// <summary>Marca de verificación gruesa de dos segmentos.</summary>
        static float CheckAlpha(float x, float y, int size)
        {
            float s = size / 64f;
            Vector2 p = new Vector2(x, y);
            Vector2 a = new Vector2(14f, 33f) * s;
            Vector2 b = new Vector2(27f, 19f) * s;
            Vector2 c = new Vector2(51f, 46f) * s;
            float distance = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, c));
            float halfWidth = 5.5f * s;
            return halfWidth - distance + 0.5f;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
