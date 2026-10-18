using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CarritoCaotico.Tests
{
    /// <summary>Animación de los botones, sonidos de la partida, música y botón de sonido del menú.</summary>
    public class CarritoAudioUiTests
    {
        const string ClassicPath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";
        const string HalloweenPath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Halloween.unity";

        AudioMode previousMode;
        GameObject canvasObject;

        [SetUp]
        public void SetUp()
        {
            previousMode = GameAudio.Mode;
            GameAudio.Mode = AudioMode.Todo;
        }

        [TearDown]
        public void TearDown()
        {
            GameAudio.Mode = previousMode;
            Time.timeScale = 1f;
            if (canvasObject != null) Object.Destroy(canvasObject);
            GameSession.ReturnToMenu();
            GameInput.Scheme = ControlScheme.Normal;
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeout, string message)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(condition(), message);
        }

        static IEnumerator Load(string path)
        {
            if (!File.Exists(path)) Assert.Ignore($"La escena {path} no está generada.");
            GameSession.SetSeed(1);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator ButtonFeedback_GrowsLiftsAndSquashesEvenWhilePaused()
        {
            canvasObject = new GameObject("Canvas_Prueba", typeof(RectTransform), typeof(Canvas));
            var buttonObject = new GameObject("Boton_Prueba", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.sizeDelta = new Vector2(200f, 60f);
            var feedback = buttonObject.AddComponent<UIButtonFeedback>();
            yield return null;
            float baseY = rect.anchoredPosition.y;

            // En pausa (timeScale 0) la animación sigue: usa tiempo sin escala.
            Time.timeScale = 0f;
            var pointer = new PointerEventData(null);
            feedback.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1.06f, feedback.CurrentScale, 0.01f, "Con el mouse encima el botón crece.");
            Assert.Greater(rect.anchoredPosition.y, baseY + 3f, "Y se eleva un poco.");

            feedback.OnPointerDown(pointer);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0.94f, feedback.CurrentScale, 0.01f, "Al pulsar se aplasta.");

            feedback.OnPointerUp(pointer);
            feedback.OnPointerExit(pointer);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1f, feedback.CurrentScale, 0.01f);
            Assert.AreEqual(baseY, rect.anchoredPosition.y, 0.5f, "Vuelve a su sitio.");

            // Un botón desactivado no reacciona.
            buttonObject.GetComponent<Button>().interactable = false;
            feedback.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(1f, feedback.CurrentScale, 0.01f);
        }

        [UnityTest]
        public IEnumerator GameEvents_PlayTheirSounds()
        {
            GameSession.Begin(1, StoreId.Clasico);
            yield return Load(ClassicPath);
            var manager = Object.FindAnyObjectByType<GameManager>();
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 8f, "La partida debería empezar.");
            GameAudio audio = GameAudio.Instance;
            Assert.IsNotNull(audio, "La escena tiene GameAudio.");
            Assert.AreEqual("headinthesand", audio.Music.name);

            GrabberArm grabber = manager.Grabber;
            Product product = GrabNearHand(grabber);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(grabber.TryGrabNearest());
            Assert.AreEqual("Grab", audio.LastPlayed);

            audio.ClearHistory();
            grabber.DropHeld();
            yield return null;
            yield return null;
            CollectionAssert.Contains(audio.History, "Drop");

            yield return new WaitForSeconds(0.3f);
            GrabNearHand(grabber, product);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(grabber.TryGrabNearest());
            audio.ClearHistory();
            grabber.ThrowHeld();
            yield return null;
            yield return null;
            CollectionAssert.Contains(audio.History, "Throw");
            CollectionAssert.DoesNotContain(audio.History, "Drop", "Lanzar suena como lanzamiento, no como soltar.");

            audio.ClearHistory();
            manager.SetPaused(true);
            // Al pausar también se selecciona «Continuar» (y suena su hover).
            CollectionAssert.Contains(audio.History, "Pause");
            manager.SetPaused(false);

            // Últimos segundos: tic tac, y al acabar el tiempo, jingle de derrota.
            manager.Timer.SetRemaining(2.5f);
            yield return WaitUntil(() => audio.LastPlayed == "Tick", 2f, "Los últimos segundos hacen tic.");
            yield return WaitUntil(() => manager.State == GameState.TiempoAgotado, 4f, "Se acaba el tiempo.");
            Assert.AreEqual("Lose", audio.LastPlayed);
        }

        [UnityTest]
        public IEnumerator Silence_TurnsMusicDownAndHalloweenHasItsOwnMusic()
        {
            GameSession.Begin(1, StoreId.Halloween);
            yield return Load(HalloweenPath);
            GameAudio audio = GameAudio.Instance;
            Assert.IsNotNull(audio);
            Assert.AreEqual("creepyloop-v2", audio.Music.name, "Halloween tiene su propia música.");
            Assert.AreEqual(audio.Music, audio.MusicSource.clip);

            yield return WaitUntil(() => audio.MusicSource.volume > 0.2f, 3f, "La música sube al empezar.");
            GameAudio.Mode = AudioMode.SoloEfectos;
            yield return WaitUntil(() => audio.MusicSource.volume < 0.01f, 3f, "Solo efectos apaga la música.");
            GameAudio.Mode = AudioMode.Todo;
            yield return WaitUntil(() => audio.MusicSource.volume > 0.2f, 3f, "Y vuelve al activarla.");
        }

        [UnityTest]
        public IEnumerator MenuSoundButton_CyclesTheMode()
        {
            GameSession.ReturnToMenu();
            yield return Load(ClassicPath);
            var manager = Object.FindAnyObjectByType<GameManager>();
            Assert.AreEqual(GameState.Menu, manager.State);

            GameObject soundObject = GameObject.Find("Boton_Sonido");
            Assert.IsNotNull(soundObject, "El menú tiene el botón de sonido.");
            var button = soundObject.GetComponent<Button>();
            Text label = soundObject.GetComponentInChildren<Text>();
            Assert.AreEqual(GameAudio.ModeName(AudioMode.Todo), label.text);

            button.onClick.Invoke();
            Assert.AreEqual(AudioMode.SoloEfectos, GameAudio.Mode);
            Assert.AreEqual(GameAudio.ModeName(AudioMode.SoloEfectos), label.text);
            button.onClick.Invoke();
            Assert.AreEqual(GameAudio.ModeName(AudioMode.Silencio), label.text);

            // Pasar el mouse por un botón del menú suena (aunque esté en silencio, se registra).
            var play = GameObject.Find("Boton_Jugar").GetComponent<UIButtonFeedback>();
            int before = GameAudio.Instance.PlayedCount;
            play.OnPointerEnter(new PointerEventData(null));
            Assert.AreEqual("Hover", GameAudio.Instance.LastPlayed);
            Assert.Greater(GameAudio.Instance.PlayedCount, before);
        }

        [UnityTest]
        public IEnumerator Music_ContinuesAfterRestart()
        {
            GameSession.Begin(1, StoreId.Clasico);
            yield return Load(ClassicPath);
            var manager = Object.FindAnyObjectByType<GameManager>();
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 8f, "La partida debería empezar.");
            AudioSource music = GameAudio.Instance.MusicSource;
            if (!music.isPlaying) Assert.Ignore("No hay dispositivo de audio en este equipo (modo batch): no se puede medir la música.");
            music.time = 5f;
            yield return null;

            manager.Restart();
            yield return WaitUntil(() => !LoadingScreen.IsLoading, 8f, "La pantalla de carga debería cerrarse.");
            Assert.AreNotSame(music, GameAudio.Instance.MusicSource);
            Assert.GreaterOrEqual(GameAudio.Instance.MusicSource.time, 5f, "La música sigue donde iba, no empieza de cero.");
        }

        static Product GrabNearHand(GrabberArm grabber, Product product = null)
        {
            if (product == null)
            {
                foreach (Product p in Object.FindObjectsByType<Product>())
                    if (p.IsOnShelf)
                    {
                        product = p;
                        break;
                    }
            }
            product.transform.position = grabber.HandPosition;
            product.Body.position = grabber.HandPosition;
            product.Body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            return product;
        }
    }
}
