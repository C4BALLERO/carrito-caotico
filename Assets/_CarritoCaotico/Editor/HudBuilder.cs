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

        public static GameUI Build(Font uiFont)
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
            var refs = new Dictionary<string, Object>();

            BuildTimer(root, refs);
            BuildPusherPanel(root, refs);
            ShoppingListRow[] rows = BuildGrabberPanel(root, refs);
            BuildMessage(root, refs);
            BuildPreparation(root, refs);
            BuildReceipt(root, refs);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var ui = canvasGo.AddComponent<GameUI>();
            Wire(ui, refs, rows);
            return ui;
        }

        // ======================================================================
        // Secciones
        // ======================================================================

        static void BuildTimer(Transform root, Dictionary<string, Object> refs)
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

        static void BuildPusherPanel(Transform root, Dictionary<string, Object> refs)
        {
            Image fill = CardBox(root, "Panel_Empujador", new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(560f, 312f), Card, 1f);
            Header(fill.transform, "EMPUJADOR", "Jugador 1", PusherBlue, 64f);

            float y = -84f;
            KeyRow(fill.transform, y, ("W", "Acelerar"), ("S", "Frenar / atrás"));
            KeyRow(fill.transform, y - 46f, ("A  D", "Girar"), ("Shift izq.", "Derrapar"));
            KeyRow(fill.transform, y - 92f, ("Mouse", "Cámara"), ("Esc", "Cursor"));

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

        static ShoppingListRow[] BuildGrabberPanel(Transform root, Dictionary<string, Object> refs)
        {
            Image fill = CardBox(root, "Panel_Agarrador", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(540f, 748f), Card, 1f);
            Header(fill.transform, "Lista del Agarrador", "Jugador 2", GrabberPurple, 72f, 32);

            Text note = Label(fill.transform, "Aviso", "Solo el Agarrador mira esta lista y guía al Empujador.", 19, MutedInk, TextAnchor.MiddleLeft, FontStyle.Italic);
            Place(note.rectTransform, new Vector2(22f, -84f), new Vector2(500f, 28f));

            const int rowCount = 5; // la lista nunca pide más de 5 tipos
            var rows = new ShoppingListRow[rowCount];
            for (int i = 0; i < rowCount; i++)
                rows[i] = ListRow(fill.transform, i, -124f - i * 56f);

            Text extra = Label(fill.transform, "Sobrantes", "", 20, WarningText, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(extra.rectTransform, new Vector2(22f, -410f), new Vector2(500f, 52f));
            extra.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text progress = Label(fill.transform, "Progreso", "Conseguido: 0 de 0 unidades", 22, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(progress.rectTransform, new Vector2(22f, -468f), new Vector2(500f, 30f));
            Image track = Box(fill.transform, "Barra_Progreso", Neutral, circleSprite: false, ppu: 3f);
            Place(track.rectTransform, new Vector2(22f, -504f), new Vector2(496f, 18f));
            Image bar = Box(track.transform, "Relleno", Success, circleSprite: false, ppu: 3f);
            Anchor(bar.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            // Estado de la mano: punto de color + texto.
            Image handChip = Box(fill.transform, "Estado_Mano", KeyCap, circleSprite: false, ppu: 2f);
            Place(handChip.rectTransform, new Vector2(22f, -538f), new Vector2(496f, 48f));
            Image dot = Box(handChip.transform, "Punto", MutedInk, circleSprite: true, ppu: 1f);
            Place(dot.rectTransform, new Vector2(16f, -14f), new Vector2(20f, 20f));
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
            Place(divider.rectTransform, new Vector2(22f, -602f), new Vector2(496f, 3f));
            KeyRow(fill.transform, -614f, ("Flechas", "Mover brazo"));
            KeyRow(fill.transform, -656f, ("Enter", "Agarrar / soltar"));
            KeyRow(fill.transform, -698f, ("Ctrl der.", "Lanzar"));

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
            Place((RectTransform)go.transform, new Vector2(22f, y), new Vector2(496f, 48f));

            Image box = Box(go.transform, "Casilla", Neutral, circleSprite: false, ppu: 2f);
            Place(box.rectTransform, new Vector2(0f, -6f), new Vector2(36f, 36f));
            var mark = new GameObject("Check", typeof(RectTransform)).AddComponent<Image>();
            mark.transform.SetParent(box.transform, false);
            mark.sprite = check;
            mark.color = Color.white;
            mark.raycastTarget = false;
            Anchor(mark.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            mark.enabled = false;
            elementCount++;

            Text name = Label(go.transform, "Nombre", "1 × Producto", 26, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(name.rectTransform, new Vector2(52f, 0f), new Vector2(330f, 48f));

            Image pill = Box(go.transform, "Cantidad", Neutral, circleSprite: false, ppu: 2f);
            Place(pill.rectTransform, new Vector2(390f, -6f), new Vector2(106f, 36f));
            Text count = Label(pill.transform, "Texto", "0/1", 22, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(count.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return new ShoppingListRow { root = go, checkBox = box, checkMark = mark, nameText = name, countPill = pill, countText = count };
        }

        static void BuildMessage(Transform root, Dictionary<string, Object> refs)
        {
            Image fill = CardBox(root, "Mensaje", new Vector2(0.5f, 1f), new Vector2(10f, -164f), new Vector2(740f, 78f), Ink, 1f);
            Text text = Label(fill.transform, "Texto", "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            refs["messagePanel"] = fill.transform.parent.gameObject;
            refs["messageText"] = text;
            refs["messageBackground"] = fill;
        }

        static void BuildPreparation(Transform root, Dictionary<string, Object> refs)
        {
            Image fill = CardBox(root, "Preparacion", new Vector2(0.5f, 0.5f), new Vector2(-120f, 60f), new Vector2(680f, 320f), Card, 1f);
            Text title = Label(fill.transform, "Titulo", "¡Prepárense!", 52, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(20f, -24f), new Vector2(640f, 64f));
            Text count = Label(fill.transform, "Cuenta", "4", 120, Success, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(count.rectTransform, new Vector2(20f, -88f), new Vector2(640f, 130f));
            Text hint = Label(fill.transform, "Pista", "Agarrador: lee la lista de la derecha y dile al Empujador adónde ir.", 24, MutedInk, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place(hint.rectTransform, new Vector2(30f, -226f), new Vector2(620f, 70f));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            refs["preparationPanel"] = fill.transform.parent.gameObject;
            refs["preparationCountText"] = count;
        }

        static void BuildReceipt(Transform root, Dictionary<string, Object> refs)
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

            // Botón principal con atajo de teclado visible.
            Image buttonFill = CardBox(paper.transform, "Boton_Reiniciar", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(400f, 84f), Warning, 1f);
            var button = buttonFill.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonFill;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.8f);
            colors.selectedColor = new Color(1f, 0.93f, 0.8f);
            colors.pressedColor = new Color(0.85f, 0.75f, 0.6f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            Text buttonText = Label(buttonFill.transform, "Texto", "Jugar otra vez", 32, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(buttonText.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, 0f), new Vector2(-110f, 0f));
            Image key = KeyCapChip(buttonFill.transform, "R", 50f);
            Anchor(key.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-84f, -22f), new Vector2(-34f, 22f));

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
            refs["restartButton"] = button;
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
        static void Header(Transform parent, string title, string player, Color color, float height, int titleSize = 28)
        {
            Image band = Box(parent, "Cabecera", color, circleSprite: false, ppu: 1.2f);
            Anchor(band.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(8f, -8f - height), new Vector2(-8f, -8f));
            Text label = Label(band.transform, "Titulo", title, titleSize, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-150f, 0f));

            Image chip = Box(band.transform, "Jugador", Color.white, circleSprite: false, ppu: 2f);
            Anchor(chip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-142f, -17f), new Vector2(-14f, 17f));
            Text chipText = Label(chip.transform, "Texto", player, 19, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(chipText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        /// <summary>Fila de controles: teclas dibujadas como teclas reales y su acción.</summary>
        static void KeyRow(Transform parent, float y, params (string key, string action)[] items)
        {
            float x = 20f;
            foreach (var (keyLabel, action) in items)
            {
                float width = Mathf.Max(42f, 18f + keyLabel.Length * 13f);
                Image key = KeyCapChip(parent, keyLabel, width);
                Place(key.rectTransform, new Vector2(x, y), new Vector2(width, 38f));
                x += width + 10f;

                Text text = Label(parent, "Accion_" + action, action, 21, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
                float textWidth = 22f + action.Length * 10.5f;
                Place(text.rectTransform, new Vector2(x, y), new Vector2(textWidth, 38f));
                x += textWidth + 18f;
            }
        }

        static Image KeyCapChip(Transform parent, string label, float width)
        {
            // Tecla: borde de tinta + cara clara desplazada hacia arriba (efecto de relieve).
            Image edge = Box(parent, "Tecla_" + label, Ink, circleSprite: false, ppu: 2.5f);
            edge.rectTransform.sizeDelta = new Vector2(width, 38f);
            Image face = Box(edge.transform, "Cara", KeyCap, circleSprite: false, ppu: 2.75f);
            Anchor(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 5f), new Vector2(-2f, -2f));
            Text text = Label(face.transform, "Texto", label, 19, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
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

        static void Wire(GameUI ui, Dictionary<string, Object> refs, ShoppingListRow[] rows)
        {
            var serialized = new SerializedObject(ui);
            foreach (var pair in refs)
            {
                SerializedProperty property = serialized.FindProperty(pair.Key);
                if (property == null)
                {
                    Debug.LogError($"[Carrito] GameUI no tiene el campo '{pair.Key}'.");
                    continue;
                }
                property.objectReferenceValue = pair.Value;
            }

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
