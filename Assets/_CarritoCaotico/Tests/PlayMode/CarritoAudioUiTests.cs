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
    /// <summary>Animación de los botones, botón de sonido del menú (que respeta el AudioDirector) y sonido de la explosión.</summary>
    public class CarritoAudioUiTests
    {
        const string ClassicPath = "Assets/_CarritoCaotico/00_Scenes/CarritoCaotico_Prueba.unity";

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

        static AudioSource DirectorSource(string name)
        {
            GameObject go = GameObject.Find("Audio_" + name);
            Assert.IsNotNull(go, $"El AudioDirector debería tener la fuente Audio_{name}.");
            return go.GetComponent<AudioSource>();
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

            // Seleccionado con teclado o mando también crece.
            feedback.OnSelect(new BaseEventData(null));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1.06f, feedback.CurrentScale, 0.01f);
            feedback.OnDeselect(new BaseEventData(null));

            // Un botón desactivado no reacciona.
            buttonObject.GetComponent<Button>().interactable = false;
            feedback.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1f, feedback.CurrentScale, 0.01f);
        }

        [UnityTest]
        public IEnumerator SoundMode_AlsoMutesTheAudioDirector()
        {
            GameSession.Begin(1, StoreId.Clasico);
            yield return Load(ClassicPath);
            Assert.IsNotNull(GameAudio.Instance, "La escena tiene GameAudio.");
            Assert.IsNotNull(Object.FindAnyObjectByType<AudioDirector>(), "Y el AudioDirector con la música.");
            AudioSource music = DirectorSource("Musica");
            AudioSource effects = DirectorSource("Efectos");

            yield return null;
            Assert.IsFalse(music.mute);
            GameAudio.Mode = AudioMode.SoloEfectos;
            yield return null;
            Assert.IsTrue(music.mute, "Solo efectos apaga la música.");
            Assert.IsFalse(effects.mute);
            GameAudio.Mode = AudioMode.Silencio;
            yield return null;
            Assert.IsTrue(effects.mute, "Silencio apaga también los efectos.");
            GameAudio.Mode = AudioMode.Todo;
            yield return null;
            Assert.IsFalse(music.mute);
            Assert.IsFalse(effects.mute);
        }

        [UnityTest]
        public IEnumerator Explosion_HasItsOwnSound()
        {
            GameSession.Begin(1, StoreId.Clasico);
            yield return Load(ClassicPath);
            var manager = Object.FindAnyObjectByType<GameManager>();
            yield return WaitUntil(() => manager.State == GameState.EnJuego, 8f, "La partida debería empezar.");
            GameAudio.Instance.ClearHistory();
            manager.Health.ApplyDamage(1000f);
            CollectionAssert.Contains(GameAudio.Instance.History, "Explosion");
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

            // Pasar el mouse por un botón del menú suena (en silencio se registra, pero no se oye).
            var play = GameObject.Find("Boton_Jugar").GetComponent<UIButtonFeedback>();
            int before = GameAudio.Instance.PlayedCount;
            play.OnPointerEnter(new PointerEventData(null));
            Assert.AreEqual("Hover", GameAudio.Instance.LastPlayed);
            Assert.Greater(GameAudio.Instance.PlayedCount, before);
        }
    }
}
