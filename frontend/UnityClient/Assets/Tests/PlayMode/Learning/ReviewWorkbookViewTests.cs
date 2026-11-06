#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public IEnumerator PracticeButton_ReportsTheSelectedCardOnce()
        {
            instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Learning/ReviewWorkbook.prefab"
                )
            );
            var view = instance.GetComponent<ReviewWorkbookView>();
            var book = new ReviewBook(new SavedCardApi());
            book.LoadAsync().GetAwaiter().GetResult();
            int starts = 0;
            ReviewCard selected = null;
            view.PracticeRequested += card =>
            {
                starts++;
                selected = card;
            };
            view.Show(book);
            yield return null;
            Button button = instance
                .GetComponentsInChildren<Button>()
                .Single(item => item.name == "PracticeButton");
            button.onClick.Invoke();
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(selected, Is.SameAs(book.Items[0]));
            view.Hide();
            Assert.That(view.IsVisible, Is.False);
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

        private sealed class SavedCardApi : IReviewApi
        {
            public Task<ReviewPage> GetPageAsync(string scene, string cursor) =>
                Task.FromResult(
                    new ReviewPage
                    {
                        Items = new List<ReviewCard>
                        {
                            new ReviewCard
                            {
                                Id = 42,
                                SceneCode = "CAFE",
                                Npc = new NpcSummary { NpcCode = "CAFE_STAFF", Name = "Mia" },
                                SituationSummary = "포장을 부탁하기",
                                Targets = new List<ReviewTarget>
                                {
                                    new ReviewTarget { Expression = "Can I get that to go?" },
                                    new ReviewTarget { Expression = "A little less ice, please." },
                                },
                            },
                        },
                    }
                );

            public Task<ReviewCard> SaveAsync(long id) => throw new NotSupportedException();

            public Task DeleteAsync(long id) => throw new NotSupportedException();

            public Task<ConversationStart> StartAsync(long id, string requestId) =>
                throw new NotSupportedException();
        }
    }
}
#endif
