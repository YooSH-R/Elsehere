using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordDefinitionTests
    {
        [Test]
        public void SharedDefinition_EditMeaning_IsVisibleFromEveryReferencingObject()
        {
            var definition = ScriptableObject.CreateInstance<ObjectWordDefinition>();
            var source = ScriptableObject.CreateInstance<CatalogObjectWordSource>();
            var first = new GameObject("First");
            var second = new GameObject("Second");
            first.SetActive(false);
            second.SetActive(false);
            try
            {
                JsonUtility.FromJsonOverwrite(
                    "{\"details\":{\"key\":\"bench\",\"languageCode\":\"en-US\",\"text\":\"bench\",\"meaningKo\":\"벤치\"}}",
                    definition
                );
                HoverableObject left = Bind(first, definition);
                HoverableObject right = Bind(second, definition);
                Assert.That(left.UnityKey, Is.EqualTo("bench"));
                Assert.That(right.Word, Is.SameAs(left.Word));

                var fields = new SerializedObject(definition);
                fields.FindProperty("details.meaningKo").stringValue = "긴 의자";
                fields.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(source.TryGetWord(left.Word, out ObjectWord leftWord), Is.True);
                Assert.That(source.TryGetWord(right.Word, out ObjectWord rightWord), Is.True);
                Assert.That(leftWord.MeaningKo, Is.EqualTo("긴 의자"));
                Assert.That(rightWord.MeaningKo, Is.EqualTo("긴 의자"));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(source);
            }
        }

        private static HoverableObject Bind(GameObject item, ObjectWordDefinition definition)
        {
            var target = item.AddComponent<HoverableObject>();
            var fields = new SerializedObject(target);
            fields.FindProperty("word").objectReferenceValue = definition;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }
    }
}
