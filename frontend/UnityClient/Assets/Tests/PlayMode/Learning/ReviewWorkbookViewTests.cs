#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using A305.Dialogue;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace A305.Learning.PlayModeTests
{
    public sealed class ReviewWorkbookViewTests
    {
        private GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                var view = instance.GetComponent<ReviewWorkbookView>();
                if (view != null)
                    view.Hide();
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator AProgressUpdate_DoesNotUnfoldAReferenceTheLearnerClosed()
        {
            instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Dialogue/DialoguePanel.prefab"
                )
            );
            var view = instance.GetComponent<DialoguePanelView>();
            var source = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(
                "Assets/Data/Dialogue/MockDialogueScripts.asset"
            );
            source.TryGetScript("CAFE_STAFF", out DialogueScript script);
            view.Show(new DialogueConversation("CAFE_STAFF", script));
            view.SetReviewReference("Can I get that to go?");
            yield return null;
            instance
                .GetComponentsInChildren<Button>(true)
                .Single(item => item.name == "ReviewFoldButton")
                .onClick.Invoke();
            view.SetReviewReference("1 / 2  말해봤어요\nCan I get that to go?");
            yield return null;
            var words = (TMP_Text)
                new SerializedObject(view).FindProperty("suggestText").objectReferenceValue;
            Assert.That(
                words.gameObject.activeSelf,
                Is.False,
                "사용 표시만 갱신할 때 접은 카드를 다시 펼치지 않습니다."
            );
        }

        [UnityTest]
        public IEnumerator ReferenceCard_CanFoldAndReturnToOrdinarySuggestions()
        {
            instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Dialogue/DialoguePanel.prefab"
                )
            );
            var view = instance.GetComponent<DialoguePanelView>();
            var source = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(
                "Assets/Data/Dialogue/MockDialogueScripts.asset"
            );
            Assert.That(source.TryGetScript("CAFE_STAFF", out DialogueScript script), Is.True);
            view.Show(new DialogueConversation("CAFE_STAFF", script));
            yield return null;
            var fold = instance
                .GetComponentsInChildren<Button>(true)
                .Single(item => item.name == "ReviewFoldButton");
            var words = (TMP_Text)
                typeof(DialoguePanelView)
                    .GetField(
                        "suggestText",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    .GetValue(view);
            float normalSize = words.fontSize;
            view.SetReviewReference("Can I get that to go?\n포장해 주실 수 있나요?");
            yield return null;
            Assert.That(fold.gameObject.activeInHierarchy, Is.True);
            Assert.That(words.text, Does.Contain("Can I get"));
            fold.onClick.Invoke();
            Assert.That(words.gameObject.activeSelf, Is.False);
            fold.onClick.Invoke();
            Assert.That(words.gameObject.activeSelf, Is.True);
            view.SetReviewReference(null);
            Assert.That(fold.gameObject.activeSelf, Is.False);
            Assert.That(words.fontSize, Is.EqualTo(normalSize));
            Assert.That(words.text, Does.Not.Contain("Can I get that to go?"));
        }
    }
}
#endif
