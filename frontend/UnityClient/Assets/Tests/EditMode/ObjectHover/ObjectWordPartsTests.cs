using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    public class ObjectWordPartsTests
    {
        [TestCase(2.9f, 0)]
        [TestCase(3f, 0)]
        [TestCase(3.01f, -1)]
        public void FindPart_UsesDetailOnlyInsideConfiguredDistance(float distance, int expected)
        {
            var profile = ScriptableObject.CreateInstance<ObjectWordPartsDefinition>();
            var word = ScriptableObject.CreateInstance<ObjectWordDefinition>();
            try
            {
                var fields = new SerializedObject(profile);
                SerializedProperty parts = fields.FindProperty("parts");
                parts.arraySize = 1;
                SerializedProperty part = parts.GetArrayElementAtIndex(0);
                part.FindPropertyRelative("word").objectReferenceValue = word;
                SerializedProperty regions = part.FindPropertyRelative("regions");
                regions.arraySize = 2;
                regions.GetArrayElementAtIndex(0).boundsValue = new Bounds(
                    Vector3.zero,
                    Vector3.one
                );
                regions.GetArrayElementAtIndex(1).boundsValue = new Bounds(
                    Vector3.right * 2f,
                    Vector3.one
                );
                fields.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.FindPart(Vector3.zero, distance, 3f), Is.EqualTo(expected));
                Assert.That(
                    profile.FindPart(Vector3.right * 2f, distance, 3f),
                    Is.EqualTo(expected)
                );
                Assert.That(
                    profile.FindPart(Vector3.right, distance, 3f),
                    Is.EqualTo(-1),
                    "영역 사이 빈 곳은 가구 전체다."
                );
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(word);
            }
        }
    }
}
