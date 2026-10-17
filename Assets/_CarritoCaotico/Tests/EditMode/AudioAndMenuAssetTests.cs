using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CarritoCaotico.Tests
{
    /// <summary>Modo de sonido, assets descargados (con créditos) y escenas generadas con audio y botones animados.</summary>
    public class AudioAndMenuAssetTests
    {
        const string Root = "Assets/_CarritoCaotico";
        static readonly string[] Scenes =
        {
            Root + "/00_Scenes/CarritoCaotico_Prueba.unity",
            Root + "/00_Scenes/CarritoCaotico_Halloween.unity"
        };

        AudioMode previousMode;

        [SetUp]
        public void SetUp() => previousMode = GameAudio.Mode;

        [TearDown]
        public void TearDown() => GameAudio.Mode = previousMode;

        [Test]
        public void AudioMode_CyclesThroughThreeStates()
        {
            GameAudio.Mode = AudioMode.Todo;
            Assert.IsTrue(GameAudio.MusicOn);
            Assert.IsTrue(GameAudio.EffectsOn);

            Assert.AreEqual(AudioMode.SoloEfectos, GameAudio.CycleMode());
            Assert.IsFalse(GameAudio.MusicOn, "Solo efectos apaga la música.");
            Assert.IsTrue(GameAudio.EffectsOn);

            Assert.AreEqual(AudioMode.Silencio, GameAudio.CycleMode());
            Assert.IsFalse(GameAudio.EffectsOn);

            Assert.AreEqual(AudioMode.Todo, GameAudio.CycleMode(), "Después del silencio vuelve todo.");
            Assert.AreNotEqual(GameAudio.ModeName(AudioMode.Todo), GameAudio.ModeName(AudioMode.Silencio));
        }

        [Test]
        public void AudioMode_IsSavedInPlayerPrefs()
        {
            GameAudio.Mode = AudioMode.Silencio;
            Assert.AreEqual((int)AudioMode.Silencio, PlayerPrefs.GetInt("CarritoCaotico.Sonido", -1));
        }

        [Test]
        public void TimerFormat_RoundsUpAndNeverNegative()
        {
            Assert.AreEqual("0:00", GameTimer.Format(0f));
            Assert.AreEqual("0:00", GameTimer.Format(-5f));
            Assert.AreEqual("1:00", GameTimer.Format(59.2f));
            Assert.AreEqual("2:05", GameTimer.Format(125f));
        }

        [Test]
        public void EveryAudioFile_IsCredited()
        {
            string credits = File.ReadAllText(Root + "/05_Audio/CREDITOS.md");
            StringAssert.Contains("CC0", credits);
            foreach (string file in Directory.GetFiles(Root + "/05_Audio", "*.ogg", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                StringAssert.Contains(name, credits, $"{name} no aparece en 05_Audio/CREDITOS.md.");
            }
        }

        [Test]
        public void KenneySprites_AreSlicedSprites()
        {
            foreach (string name in new[] { "boton_principal", "boton_secundario", "boton_verde", "campo_texto" })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/07_Sprites/Kenney/{name}.png");
                Assert.IsNotNull(sprite, $"{name} debería importarse como sprite.");
                Assert.Greater(sprite.border.x, 0f, $"{name} necesita borde de 9 cortes para estirarse sin deformar las esquinas.");
            }
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/07_Sprites/Kenney/flecha_derecha.png"));
        }

        [Test]
        public void GeneratedScenes_HaveAnimatedButtonsAndAudio()
        {
            foreach (string path in Scenes)
            {
                if (!File.Exists(path)) Assert.Ignore("Las escenas no están generadas.");
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    int buttons = 0;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (Button button in root.GetComponentsInChildren<Button>(true))
                        {
                            buttons++;
                            Assert.IsNotNull(button.GetComponent<UIButtonFeedback>(), $"{button.name} no tiene animación de hover.");
                        }
                    }
                    Assert.Greater(buttons, 15, "El menú, la pausa, el recibo y el panel LAN tienen botones.");

                    GameAudio audio = null;
                    foreach (GameObject root in scene.GetRootGameObjects())
                        if (audio == null) audio = root.GetComponentInChildren<GameAudio>(true);
                    Assert.IsNotNull(audio, $"{path} no tiene GameAudio.");

                    var serialized = new SerializedObject(audio);
                    SerializedProperty property = serialized.GetIterator();
                    int clips = 0;
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Assert.IsNotNull(property.objectReferenceValue, $"GameAudio.{property.name} está vacío en {path}.");
                        if (property.objectReferenceValue is AudioClip) clips++;
                    }
                    Assert.AreEqual(19, clips, "Música, 2 jingles y 16 efectos.");

                    string expectedMusic = path.Contains("Halloween") ? "creepyloop-v2" : "headinthesand";
                    Assert.AreEqual(expectedMusic, audio.Music.name);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
