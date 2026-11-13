using System.Reflection;
using A305.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Mission.Tests
{
    /// <summary>퀘스트 수첩은 화면에 맞춘 원래 크기를 다시 정해도 사용자가 바꾼 크기를 지킨다.</summary>
    public sealed class JournalWindowTests
    {
        private GameObject instance;

        [TearDown]
        public void Clean() => Object.DestroyImmediate(instance);

        [Test]
        public void Journal_KeepsTheUserSizeWhenTheHudLaysOutAgain()
        {
            instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Mission/FirstDayMission.prefab"
                )
            );
            var view = instance.GetComponent<FirstDayMissionView>();
            var journal = (RectTransform)
                (
                    (GameObject)
                        typeof(FirstDayMissionView)
                            .GetField("journal", BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(view)
                ).transform;
            // 화면 Awake처럼 붙인다(Awake는 공용 상태에 구독해 시험에서는 부르지 않는다).
            WindowFrame frame = WindowFrame.Attach(journal, .75f, .6f);
            view.UpdateLayout();
            Vector2 size = journal.rect.size;

            Assert.That(frame.BeginResize(new Vector2Int(1, -1)), Is.True);
            frame.DragResize(new Vector2(-80, 100) * journal.localScale.x);
            Vector2 resized = journal.rect.size;
            view.UpdateLayout();

            Assert.That(resized, Is.EqualTo(size - new Vector2(80, 100)));
            Assert.That(journal.rect.size, Is.EqualTo(resized));
        }
    }
}
