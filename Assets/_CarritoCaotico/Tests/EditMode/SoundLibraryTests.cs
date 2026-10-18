using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CarritoCaotico.Tests
{
    /// <summary>Pruebas de la convención de nombres de la biblioteca de sonidos.</summary>
    public class SoundLibraryTests
    {
        [Test]
        public void FileNameFor_ConvertsFieldNamesToSnakeCase()
        {
            Assert.AreEqual("amb_dia", SoundLibrary.FileNameFor("ambDia"));
            Assert.AreEqual("sfx_ruedas_loop", SoundLibrary.FileNameFor("sfxRuedasLoop"));
            Assert.AreEqual("sfx_aviso_30s", SoundLibrary.FileNameFor("sfxAviso30s"));
            Assert.AreEqual("ui_navegar", SoundLibrary.FileNameFor("uiNavegar"));
        }

        [Test]
        public void EveryClipField_HasUniqueFileNameWithKnownPrefix()
        {
            var names = new HashSet<string>();
            foreach (FieldInfo field in typeof(SoundLibrary).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(AudioClip)) continue;
                string fileName = SoundLibrary.FileNameFor(field.Name);
                Assert.IsTrue(names.Add(fileName), $"Nombre repetido: {fileName}");
                StringAssert.IsMatch("^(amb|mus|sfx|ui|voz)_[a-z0-9_]+$", fileName);
            }
            Assert.GreaterOrEqual(names.Count, 30);
        }
    }
}
