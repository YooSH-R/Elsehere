using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace A305.Dialogue.Tests
{
    public class DialoguePanelAssetTests
    {
        private const string PanelPrefabPath = "Assets/Prefabs/Dialogue/DialoguePanel.prefab";

        // 줄바꿈이 꺼져 있으면 긴 NPC 대사가 대화창 오른쪽 끝에서 잘린다.
        [Test]
        public void LongNpcLines_WrapInsideThePanel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            var view = prefab.GetComponentInChildren<DialoguePanelView>(true);

            var lines = (TMP_Text)
                new SerializedObject(view).FindProperty("linesText").objectReferenceValue;

            Assert.That(lines.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
        }
    }
}
