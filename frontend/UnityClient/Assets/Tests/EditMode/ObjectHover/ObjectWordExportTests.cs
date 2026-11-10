using System.Linq;
using A305.EditorTools.ObjectHover;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordExportTests
    {
        private ObjectWordDefinition definition;

        [SetUp]
        public void CreateWord()
        {
            definition = ScriptableObject.CreateInstance<ObjectWordDefinition>();
            JsonUtility.FromJsonOverwrite(
                "{\"details\":{\"key\":\"chair\",\"languageCode\":\"en-US\",\"text\":\"owner's chair\",\"meaningKo\":\"주인의 의자\",\"example\":\"It's a chair.\",\"exampleKo\":\"의자예요.\",\"pronunciationAudioKey\":\"\"}}",
                definition
            );
        }

        [TearDown]
        public void DestroyWord() => Object.DestroyImmediate(definition);

        [Test]
        public void SerializeWords_AssetContents_RoundTripsForBackend()
        {
            string json = ObjectWordExport.SerializeWords(new[] { definition });
            StringAssert.Contains("\"words\"", json);
            var catalog = ObjectWordCatalog.Parse(json);
            Assert.That(catalog.TryGetDetails("chair", out ObjectWordDetails word), Is.True);
            Assert.That(word.Text, Is.EqualTo("owner's chair"));
            Assert.That(word.MeaningKo, Is.EqualTo("주인의 의자"));
            Assert.That(word.ExampleKo, Is.EqualTo("의자예요."));
            Assert.That(word.PronunciationAudioKey, Is.Empty);
        }

        [TestCase("key")]
        [TestCase("languageCode")]
        [TestCase("text")]
        [TestCase("meaningKo")]
        [TestCase("example")]
        [TestCase("exampleKo")]
        public void Export_MissingRequiredField_RejectsBothOutputs(string field)
        {
            var fields = new UnityEditor.SerializedObject(definition);
            fields.FindProperty("details." + field).stringValue = " ";
            fields.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<System.ArgumentException>(() =>
                ObjectWordExport.SerializeWords(new[] { definition })
            );
            Assert.Throws<System.ArgumentException>(() =>
                ObjectWordExport.BuildSqlSection(new[] { definition })
            );
        }

        [Test]
        public void Export_DuplicateKey_RejectsAmbiguousAssets()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectWordExport.SerializeWords(new[] { definition, definition })
            );
        }

        [Test]
        public void Export_SameLanguageAndTextWithDifferentKeys_RejectsDuplicateVocabulary()
        {
            var duplicate = Object.Instantiate(definition);
            try
            {
                var fields = new UnityEditor.SerializedObject(duplicate);
                fields.FindProperty("details.key").stringValue = "another_chair";
                fields.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<System.ArgumentException>(() =>
                    ObjectWordExport.SerializeWords(new[] { definition, duplicate })
                );
            }
            finally
            {
                Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public void Export_EmptyCatalog_RejectsAccidentalEmptyPublication()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectWordExport.SerializeWords(new ObjectWordDefinition[0])
            );
        }

        [Test]
        public void BuildSqlSection_ApostropheAndMissingAudio_EscapesTextAndUsesNull()
        {
            string sql = ObjectWordExport.BuildSqlSection(new[] { definition });
            StringAssert.Contains("'owner''s chair'", sql);
            StringAssert.Contains("'It''s a chair.'", sql);
            StringAssert.Contains("'MASTER', NULL)", sql);
            StringAssert.Contains("ON CONFLICT (language_code, text) DO UPDATE", sql);
        }

        // 빌드에 넣은 장소 씬과 매장 실내 씬은 모두 단어 등록·내보내기가 읽어야 한다.
        // 마을 매장을 실내 씬으로 나눴을 때 내보내기가 장소 씬 6개만 읽어 실내 단어가 빠졌다. 입장 씬에는 사물이 없다.
        [Test]
        public void WordScenes_IncludeEveryBuildSceneButTheEntryScene()
        {
            string[] buildScenes = EditorBuildSettings
                .scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !path.EndsWith("/EntryScene.unity"))
                .ToArray();

            Assert.That(ObjectWordAuthoring.WordScenePaths(), Is.EquivalentTo(buildScenes));
        }
    }
}
