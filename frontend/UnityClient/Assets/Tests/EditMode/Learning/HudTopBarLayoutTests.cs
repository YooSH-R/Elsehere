using A305.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Learning.Tests
{
    public sealed class HudTopBarLayoutTests
    {
        [Test]
        public void SurveyInsertsOneSlotWithoutMovingThePhone()
        {
            float before = HudTopBarLayout.StatusRight(false);
            float after = HudTopBarLayout.StatusRight(true);
            Assert.That(
                before,
                Is.EqualTo(
                    HudTopBarLayout.PhoneRight + HudTopBarLayout.PhoneWidth + HudTopBarLayout.Gap
                )
            );
            Assert.That(
                after - before,
                Is.EqualTo(HudTopBarLayout.SurveyWidth + HudTopBarLayout.Gap).Within(.001f)
            );
            // 스피커는 설정 창으로 옮겨 휴대폰이 화면 오른쪽 여백에 바로 붙는다.
            Assert.That(HudTopBarLayout.PhoneRight, Is.EqualTo(HudTopBarLayout.Margin));
        }

        // 배치 상수를 바꾸고 프리팹을 다시 만들지 않으면 설문 아이콘이 옛 자리에 남아 코인 칩을 덮는다.
        [Test]
        public void SavedSurveyEntrySitsBetweenThePhoneAndTheStatusChips()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Learning/LearningHud.prefab"
            );
            var entry = (RectTransform)hud.transform.Find("SurveyPrompt/EntryButton");

            Assert.That(entry.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(entry.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(entry.pivot, Is.EqualTo(Vector2.one));
            float right = -entry.anchoredPosition.x;
            float left = right + entry.sizeDelta.x;
            Assert.That(right, Is.EqualTo(HudTopBarLayout.SurveyRight).Within(.01f));
            Assert.That(
                left + HudTopBarLayout.Gap,
                Is.EqualTo(HudTopBarLayout.StatusRight(true)).Within(.01f)
            );
        }
    }
}
