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
    /// tarjetas redondeadas con borde grueso y sombra suave, contraste mínimo 4.5:1 en texto y
    /// estados que nunca dependen solo del color. Durante la partida solo se muestra la Lista del
    /// Agarrador; los controles viven en el tutorial, la preparación y la pausa.
    /// Las texturas de la interfaz (esquinas redondeadas, círculo y check) se generan aquí.
    /// </summary>
    internal static class HudBuilder
    {
        const string UiFolder = CarritoSceneBuilder.RootFolder + "/07_Sprites";

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
        static readonly Color32 Shadow = new Color32(15, 23, 42, 60);

        const float BorderWidth = 3f;
        const float ShadowOffset = 6f;

        /// <summary>Colores que cambian con el tema del supermercado.</summary>
        struct Theme
        {
            public Color Accent;        // cabecera de la lista y del menú (texto blanco encima)
            public Color MenuBack;
            public Color MenuStripe;
            public Color MenuSubtitle;
        }

        static Font font;
        static Sprite rounded;
        static Sprite circle;
        static Sprite check;
        static int elementCount;
        static Theme theme;

        public static int ElementCount => elementCount;

        /// <summary>Construye el HUD, el tutorial, el menú principal y el menú de pausa.</summary>
        public static (GameUI ui, MenuUI menu, TutorialController tutorial) Build(Font uiFont, StoreId store)
        {
            font = uiFont;
            elementCount = 0;
            theme = store == StoreId.Halloween
                ? new Theme
                {
                    Accent = new Color32(194, 65, 12, 255),       // naranja calabaza oscuro (5.2:1 con blanco)
                    MenuBack = new Color32(30, 16, 46, 255),
                    MenuStripe = new Color32(46, 24, 70, 255),
                    MenuSubtitle = new Color32(254, 215, 170, 255)
                }
                : new Theme
                {
                    Accent = new Color32(124, 58, 237, 255),      // morado del Agarrador
                    MenuBack = new Color32(236, 253, 245, 255),
                    MenuStripe = new Color32(209, 250, 229, 255),
                    MenuSubtitle = new Color32(209, 250, 229, 255)
                };
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
            var tutorialRefs = new Dictionary<string, object>();

            // Durante la partida solo se ve la lista (y avisos breves).
            Transform game = Container(root, "Juego");
            refs["hudRoot"] = game.gameObject;
            ShoppingListRow[] rows = BuildListPanel(game, refs);
            BuildMessage(game, refs);

            BuildTutorial(root, tutorialRefs);
            BuildPreparation(root, refs);
            BuildReceipt(root, refs);
            BuildPause(root, refs, menuRefs);
            BuildMenu(root, menuRefs, store);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var ui = canvasGo.AddComponent<GameUI>();
            Wire(ui, refs);
            WireRows(ui, rows);
            var menu = canvasGo.AddComponent<MenuUI>();
            Wire(menu, menuRefs);
            var tutorial = canvasGo.AddComponent<TutorialController>();
            Wire(tutorial, tutorialRefs);
            return (ui, menu, tutorial);
        }

        static Transform Container(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return go.transform;
        }

        // ======================================================================
        // Partida: solo la Lista del Agarrador
        // ======================================================================

        static ShoppingListRow[] BuildListPanel(Transform root, Dictionary<string, object> refs)
        {
            Image fill = CardBox(root, "Panel_Agarrador", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(540f, 616f), Card, 1f);

            // Cabecera: título + reloj integrado.
            Image band = Box(fill.transform, "Cabecera", theme.Accent, circleSprite: false, ppu: 1.2f);
            Anchor(band.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(8f, -80f), new Vector2(-8f, -8f));
            Text title = Label(band.transform, "Titulo", "Lista del Agarrador", 30, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-160f, 0f));
            Image timerPill = Box(band.transform, "Reloj", Color.white, circleSprite: false, ppu: 1.6f);
            Anchor(timerPill.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, -25f), new Vector2(-12f, 25f));
            Text timerText = Label(timerPill.transform, "Tiempo", "2:00", 30, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(timerText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            timerText.resizeTextForBestFit = true;
            timerText.resizeTextMinSize = 16;
            timerText.resizeTextMaxSize = 30;

            Text subtitle = Label(fill.transform, "Subtitulo", "Nivel 1", 19, MutedInk, TextAnchor.MiddleLeft, FontStyle.Italic);
            Place(subtitle.rectTransform, new Vector2(22f, -88f), new Vector2(500f, 28f));
            subtitle.resizeTextForBestFit = true;
            subtitle.resizeTextMinSize = 13;
            subtitle.resizeTextMaxSize = 19;
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitle.verticalOverflow = VerticalWrapMode.Truncate;

            int rowCount = LevelProgression.MaxListUnits;
            var rows = new ShoppingListRow[rowCount];
            for (int i = 0; i < rowCount; i++)
                rows[i] = ListRow(fill.transform, i, -124f - i * 46f);

            Text extra = Label(fill.transform, "Sobrantes", "", 20, WarningText, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(extra.rectTransform, new Vector2(22f, -496f), new Vector2(500f, 46f));
            extra.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text progress = Label(fill.transform, "Progreso", "Conseguido: 0 de 0 unidades", 22, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(progress.rectTransform, new Vector2(22f, -546f), new Vector2(500f, 30f));
            Image track = Box(fill.transform, "Barra_Progreso", Neutral, circleSprite: false, ppu: 3f);
            Place(track.rectTransform, new Vector2(22f, -582f), new Vector2(496f, 18f));
            Image bar = Box(track.transform, "Relleno", Success, circleSprite: false, ppu: 3f);
            Anchor(bar.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);

            refs["listSubtitleText"] = subtitle;
            refs["timerText"] = timerText;
            refs["timerBackground"] = timerPill;
            refs["listExtraText"] = extra;
            refs["listProgressText"] = progress;
            refs["listProgressFill"] = bar.rectTransform;
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
            Place(name.rectTransform, new Vector2(48f, 0f), new Vector2(336f, 42f));
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 16;
            name.resizeTextMaxSize = 24;

            Image pill = Box(go.transform, "Cantidad", Neutral, circleSprite: false, ppu: 2f);
            Place(pill.rectTransform, new Vector2(390f, -5f), new Vector2(106f, 32f));
            Text count = Label(pill.transform, "Texto", "0/1", 20, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(count.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return new ShoppingListRow { root = go, checkBox = box, checkMark = mark, nameText = name, countPill = pill, countText = count };
        }

        static void BuildMessage(Transform root, Dictionary<string, object> refs)
        {
            Image fill = CardBox(root, "Mensaje", new Vector2(0.5f, 1f), new Vector2(-260f, -24f), new Vector2(760f, 78f), Ink, 1f);
            Text text = Label(fill.transform, "Texto", "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = 30;
            refs["messagePanel"] = fill.transform.parent.gameObject;
            refs["messageText"] = text;
            refs["messageBackground"] = fill;
        }

        // ======================================================================
        // Tutorial
        // ======================================================================

        static void BuildTutorial(Transform root, Dictionary<string, object> refs)
        {
            Image card = CardBox(root, "Tutorial", new Vector2(0.5f, 0f), new Vector2(-250f, 30f), new Vector2(1000f, 262f), Card, 1f);

            Image who = Box(card.transform, "Jugador", PusherBlue, circleSprite: false, ppu: 2f);
            Place(who.rectTransform, new Vector2(22f, -18f), new Vector2(320f, 40f));
            Text whoText = Label(who.transform, "Texto", "EMPUJADOR · Jugador 1", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(whoText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text progress = Label(card.transform, "Progreso", "Paso 1 de 10", 18, MutedInk, TextAnchor.MiddleRight, FontStyle.Bold);
            Place(progress.rectTransform, new Vector2(500f, -18f), new Vector2(380f, 40f));

            Image done = Box(card.transform, "Hecho", Success, circleSprite: true, ppu: 1f);
            Anchor(done.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-84f, -82f), new Vector2(-22f, -20f));
            var doneMark = new GameObject("Check", typeof(RectTransform)).AddComponent<Image>();
            doneMark.transform.SetParent(done.transform, false);
            doneMark.sprite = check;
            doneMark.raycastTarget = false;
            Anchor(doneMark.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            elementCount++;

            Text title = Label(card.transform, "Titulo", "Empuja el carrito", 36, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(22f, -66f), new Vector2(860f, 48f));
            Text body = Label(card.transform, "Texto", "", 23, Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(body.rectTransform, new Vector2(22f, -118f), new Vector2(940f, 64f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Fila de teclas: el controlador copia la plantilla para cada tecla del paso.
            var keysGo = new GameObject("Teclas", typeof(RectTransform));
            keysGo.transform.SetParent(card.transform, false);
            Place((RectTransform)keysGo.transform, new Vector2(22f, -190f), new Vector2(940f, 52f));
            var layout = keysGo.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image template = KeyCapChip(keysGo.transform, "W", 60f, out Text templateText);
            templateText.fontSize = 22;
            var element = template.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 60f;
            element.preferredHeight = 48f;
            template.gameObject.name = "Plantilla_Tecla";

            // Tarjeta final.
            Transform overlay = Overlay(root, "Tutorial_Completado", new Color(0.06f, 0.09f, 0.16f, 0.62f));
            Image doneCard = CardBox(overlay, "Tarjeta", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 400f), Card, 1f);
            Text doneTitle = Label(doneCard.transform, "Titulo", "¡Tutorial completado!", 50, Success, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(doneTitle.rectTransform, new Vector2(20f, -30f), new Vector2(660f, 70f));
            Text doneBody = Label(doneCard.transform, "Texto", "Ya saben conducir, agarrar, soltar, lanzar y pagar.\nEn partida solo verán la lista: ¡a comunicarse!", 24, MutedInk, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place(doneBody.rectTransform, new Vector2(30f, -110f), new Vector2(640f, 90f));
            doneBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            Button play = CardButton(doneCard.transform, "Boton_Jugar", "Jugar nivel 1", "Enter", Warning, Ink, new Vector2(0.5f, 1f), new Vector2(-120f, -240f), new Vector2(380f, 84f), 28, 1f);
            Button menu = CardButton(doneCard.transform, "Boton_Menu", "Menú", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(200f, -240f), new Vector2(200f, 84f), 28, 1f);
            overlay.gameObject.SetActive(false);

            card.transform.parent.gameObject.SetActive(false);
            refs["panel"] = card.transform.parent.gameObject;
            refs["whoChip"] = who;
            refs["whoText"] = whoText;
            refs["titleText"] = title;
            refs["bodyText"] = body;
            refs["progressText"] = progress;
            refs["keysRow"] = keysGo.transform;
            refs["keyTemplate"] = template.gameObject;
            refs["doneBadge"] = done;
            refs["completePanel"] = overlay.gameObject;
            refs["playButton"] = play;
            refs["menuButton"] = menu;
        }

        // ======================================================================
        // Preparación, recibo y pausa
        // ======================================================================

        static void BuildPreparation(Transform root, Dictionary<string, object> refs)
        {
            Image fill = CardBox(root, "Preparacion", new Vector2(0.5f, 0.5f), new Vector2(-250f, 30f), new Vector2(900f, 470f), Card, 1f);
            Text title = Label(fill.transform, "Titulo", "Nivel 1 · ¡Prepárense!", 46, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(20f, -22f), new Vector2(860f, 60f));
            Text count = Label(fill.transform, "Cuenta", "4", 104, Success, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(count.rectTransform, new Vector2(20f, -78f), new Vector2(860f, 116f));
            Text hint = Label(fill.transform, "Pista", "Agarrador: lee la lista y guía al Empujador.", 23, MutedInk, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place(hint.rectTransform, new Vector2(30f, -198f), new Vector2(840f, 70f));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.lineSpacing = 1.15f;

            Image controlsBack = Box(fill.transform, "Controles", KeyCap, circleSprite: false, ppu: 1.5f);
            Place(controlsBack.rectTransform, new Vector2(24f, -282f), new Vector2(852f, 160f));
            Text controlsTitle = Label(controlsBack.transform, "Titulo", "CONTROLES", 18, MutedInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(controlsTitle.rectTransform, new Vector2(18f, -10f), new Vector2(400f, 26f));
            Text controls = Label(controlsBack.transform, "Texto", "", 20, Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(controls.rectTransform, new Vector2(18f, -42f), new Vector2(816f, 110f));
            controls.horizontalOverflow = HorizontalWrapMode.Wrap;
            controls.lineSpacing = 1.3f;

            refs["preparationPanel"] = fill.transform.parent.gameObject;
            refs["preparationTitleText"] = title;
            refs["preparationCountText"] = count;
            refs["preparationHintText"] = hint;
            refs["preparationControlsText"] = controls;
        }

        static void BuildReceipt(Transform root, Dictionary<string, object> refs)
        {
            // Fondo atenuado a pantalla completa (el test busca el objeto "Recibo").
            Transform overlay = Overlay(root, "Recibo", new Color(0.06f, 0.09f, 0.16f, 0.62f));

            Image paper = CardBox(overlay, "Papel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 920f), Paper, 1f);
            Image header = Box(paper.transform, "Cabecera", Success, circleSprite: false, ppu: 1f);
            Anchor(header.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(10f, -96f), new Vector2(-10f, -10f));
            Text title = Label(header.transform, "Titulo", "Recibo", 42, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
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

            // Botones con atajo visible. "Siguiente nivel" solo aparece si la compra está completa.
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

            overlay.gameObject.SetActive(false);

            refs["receiptPanel"] = overlay.gameObject;
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

        static void BuildPause(Transform root, Dictionary<string, object> uiRefs, Dictionary<string, object> menuRefs)
        {
            Transform overlay = Overlay(root, "Pausa", new Color(0.06f, 0.09f, 0.16f, 0.62f));
            Image card = CardBox(overlay, "Tarjeta", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f), Card, 1f);
            Text title = Label(card.transform, "Titulo", "Pausa", 58, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(20f, -24f), new Vector2(860f, 74f));
            Text hint = Label(card.transform, "Pista", "El tiempo y los rivales están detenidos.", 22, MutedInk, TextAnchor.MiddleCenter, FontStyle.Normal);
            Place(hint.rectTransform, new Vector2(20f, -96f), new Vector2(860f, 32f));

            Image controlsBack = Box(card.transform, "Controles", KeyCap, circleSprite: false, ppu: 1.5f);
            Place(controlsBack.rectTransform, new Vector2(30f, -140f), new Vector2(840f, 150f));
            Text controlsTitle = Label(controlsBack.transform, "Titulo", "CONTROLES DE ESTE NIVEL", 18, MutedInk, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(controlsTitle.rectTransform, new Vector2(18f, -10f), new Vector2(600f, 26f));
            Text controls = Label(controlsBack.transform, "Texto", "", 20, Ink, TextAnchor.UpperLeft, FontStyle.Normal);
            Place(controls.rectTransform, new Vector2(18f, -42f), new Vector2(804f, 100f));
            controls.horizontalOverflow = HorizontalWrapMode.Wrap;
            controls.lineSpacing = 1.3f;

            Button resume = CardButton(card.transform, "Boton_Continuar", "Continuar", "Esc", Warning, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(460f, 84f), 30, 1f);
            Button restart = CardButton(card.transform, "Boton_Reintentar", "Reintentar nivel", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -424f), new Vector2(460f, 72f), 26, 1f);
            Button menu = CardButton(card.transform, "Boton_Menu", "Menú principal", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -516f), new Vector2(460f, 72f), 26, 1f);
            overlay.gameObject.SetActive(false);

            uiRefs["pauseControlsText"] = controls;
            menuRefs["pausePanel"] = overlay.gameObject;
            menuRefs["resumeButton"] = resume;
            menuRefs["pauseRestartButton"] = restart;
            menuRefs["pauseMenuButton"] = menu;
        }

        // ======================================================================
        // Menú principal
        // ======================================================================

        static void BuildMenu(Transform root, Dictionary<string, object> refs, StoreId store)
        {
            Transform overlay = Overlay(root, "Menu_Principal", theme.MenuBack);
            for (int i = 0; i < 8; i++)
            {
                Image stripe = Box(overlay, "Franja", theme.MenuStripe, circleSprite: false, ppu: 8f);
                Anchor(stripe.rectTransform, new Vector2(i / 8f, 0f), new Vector2(i / 8f + 0.0625f, 1f), Vector2.zero, Vector2.zero);
            }

            Image card = CardBox(overlay, "Tarjeta", new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(1000f, 960f), Card, 1f);
            Image band = Box(card.transform, "Cabecera", store == StoreId.Halloween ? (Color)theme.Accent : (Color)Success, circleSprite: false, ppu: 1f);
            Anchor(band.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(10f, -176f), new Vector2(-10f, -10f));
            Text title = Label(band.transform, "Titulo", "Carrito Caótico", 86, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0f, 0.32f), Vector2.one, Vector2.zero, Vector2.zero);
            Text subtitle = Label(band.transform, "Subtitulo", "Cooperativo local para dos  ·  Better Together", 26, theme.MenuSubtitle, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(subtitle.rectTransform, Vector2.zero, new Vector2(1f, 0.36f), new Vector2(0f, 12f), Vector2.zero);

            // Selectores de supermercado, nivel y mando.
            var (previousStore, storeText, nextStore, storeDetail) = Selector(card.transform, "SUPERMERCADO", -190f, StoreCatalog.DisplayName(store), 420f);
            var (previousLevel, levelText, nextLevel, levelDetail) = Selector(card.transform, "NIVEL", -332f, "Nivel 1", 300f);
            var (previousPad, padText, nextPad, padDetail) = Selector(card.transform, "MANDO (UN JUGADOR CON TECLADO Y OTRO CON MANDO)", -474f, "Mando: automático", 420f);

            Button play = CardButton(card.transform, "Boton_Jugar", "Jugar", "Enter", Warning, Ink, new Vector2(0.5f, 1f), new Vector2(-130f, -634f), new Vector2(420f, 92f), 40, 1f);
            Button tutorial = CardButton(card.transform, "Boton_Tutorial", "Tutorial", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(220f, -634f), new Vector2(240f, 92f), 30, 1f);
            Button quit = CardButton(card.transform, "Boton_Salir", "Salir", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -742f), new Vector2(240f, 58f), 24, 1.5f);

            Text roles = Label(card.transform, "Roles",
                "<b>Jugador 1 · Empujador:</b> conduce el carrito.   <b>Jugador 2 · Agarrador:</b> lee la lista y agarra.\n" +
                "¿Primera vez? Hagan el <b>Tutorial</b>: en partida solo se ve la lista de compras.",
                20, Ink, TextAnchor.UpperCenter, FontStyle.Normal);
            Place(roles.rectTransform, new Vector2(40f, -820f), new Vector2(920f, 110f));
            roles.lineSpacing = 1.25f;
            roles.horizontalOverflow = HorizontalWrapMode.Wrap;

            overlay.gameObject.SetActive(false);
            refs["menuPanel"] = overlay.gameObject;
            refs["playButton"] = play;
            refs["tutorialButton"] = tutorial;
            refs["previousStoreButton"] = previousStore;
            refs["nextStoreButton"] = nextStore;
            refs["storeText"] = storeText;
            refs["storeDetailText"] = storeDetail;
            refs["previousLevelButton"] = previousLevel;
            refs["nextLevelButton"] = nextLevel;
            refs["levelText"] = levelText;
            refs["levelDetailText"] = levelDetail;
            refs["previousPadButton"] = previousPad;
            refs["nextPadButton"] = nextPad;
            refs["padText"] = padText;
            refs["padDetailText"] = padDetail;
            refs["quitButton"] = quit;
        }

        /// <summary>Selector "&lt; valor &gt;" con título y línea de detalle.</summary>
        static (Button previous, Text value, Button next, Text detail) Selector(Transform parent, string caption, float top, string initial, float valueWidth)
        {
            Text captionText = Label(parent, "Titulo_" + caption, caption, 19, MutedInk, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(captionText.rectTransform, new Vector2(20f, top), new Vector2(960f, 26f));

            float half = valueWidth * 0.5f;
            Button previous = CardButton(parent, "Anterior_" + caption, "<", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(-half - 52f, top - 32f), new Vector2(70f, 70f), 36, 1.5f);
            Image box = Box(parent, "Valor_" + caption, KeyCap, circleSprite: false, ppu: 1.5f);
            Anchor(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-half, top - 102f), new Vector2(half, top - 32f));
            Text value = Label(box.transform, "Texto", initial, 36, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(value.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 20;
            value.resizeTextMaxSize = 36;
            Button next = CardButton(parent, "Siguiente_" + caption, ">", null, Card, Ink, new Vector2(0.5f, 1f), new Vector2(half + 52f, top - 32f), new Vector2(70f, 70f), 36, 1.5f);

            Text detail = Label(parent, "Detalle_" + caption, "", 20, MutedInk, TextAnchor.UpperCenter, FontStyle.Normal);
            Place(detail.rectTransform, new Vector2(40f, top - 108f), new Vector2(920f, 28f));
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            return (previous, value, next, detail);
        }

        // ======================================================================
        // Componentes reutilizables
        // ======================================================================

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
        // Texturas generadas (idempotente: solo se reescriben si cambian)
        // ======================================================================

        static void EnsureSprites()
        {
            if (!AssetDatabase.IsValidFolder(UiFolder))
                AssetDatabase.CreateFolder(CarritoSceneBuilder.RootFolder, "07_Sprites");

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
