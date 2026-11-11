using A305.Api;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning.Tests
{
    /// <summary>오른쪽 위 휴대폰 표시와 학습 앱 배지를 확인한다.</summary>
    public sealed class HudTalkingTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/Learning/LearningHud.prefab";
        private GameObject instance;

        [TearDown]
        public void DestroyHud() => Object.DestroyImmediate(instance);

        [Test]
        public void UnreadFeedback_UsesMatchingBadgesAndHidesThemAtZero()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
            var hud = instance.GetComponent<LearningHudView>();
            Transform phone = instance.transform.Find("Panel/MenuBar/MenuButton/CountBadge");
            Transform feedback = instance.transform.Find("Panel/Menu/HistoryButton/CountBadge");
            Transform vocabulary = instance.transform.Find(
                "Panel/Menu/VocabularyButton/CountBadge"
            );
            Assert.That(phone, Is.Not.Null);
            Assert.That(feedback, Is.Not.Null);
            hud.SetFeedbackCount(3);
            foreach (Transform badge in new[] { phone, feedback })
            {
                Assert.That(badge.gameObject.activeSelf, Is.True);
                Assert.That(badge.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("3"));
                Assert.That(
                    badge.GetComponent<Image>().sprite,
                    Is.EqualTo(vocabulary.GetComponent<Image>().sprite)
                );
                Assert.That(
                    badge.Find("Label").GetComponent<TMP_Text>().color,
                    Is.EqualTo(vocabulary.Find("Label").GetComponent<TMP_Text>().color)
                );
            }
            hud.SetFeedbackCount(0);
            Assert.That(phone.gameObject.activeSelf, Is.False);
            Assert.That(feedback.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void UpdatingCounts_ShowsBadgesInFrontOfAppIcons()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
            var hud = instance.GetComponent<LearningHudView>();
            Transform phone = instance.transform.Find("Panel/MenuBar/MenuButton/CountBadge");
            Transform feedback = instance.transform.Find("Panel/Menu/HistoryButton/CountBadge");
            Transform vocabulary = instance.transform.Find(
                "Panel/Menu/VocabularyButton/CountBadge"
            );
            foreach (Transform badge in new[] { phone, feedback, vocabulary })
                badge.SetAsFirstSibling();

            hud.SetFeedbackCount(3);
            hud.SetVocabularyCount(2);

            foreach (Transform badge in new[] { phone, feedback, vocabulary })
            {
                Assert.That(badge.gameObject.activeSelf, Is.True);
                Assert.That(badge.GetSiblingIndex(), Is.EqualTo(badge.parent.childCount - 1));
            }
        }

        [Test]
        public void WhileTalking_ShowsTheSpeakerAndRestoresTheOtherIconsWhenFinished()
        {
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
            var hud = instance.GetComponent<LearningHudView>();
            GameObject bar = instance.transform.Find("Panel/MenuBar").gameObject;
            GameObject speaker = instance.transform.Find("BackgroundSoundButton").gameObject;
            var survey = instance.GetComponentInChildren<SurveyPromptView>(true);

            SurveyPrompt.RevealEntry();
            try
            {
                hud.SetTalking(true);
                Assert.That(bar.activeSelf, Is.False);
                Assert.That(survey.IsEntryVisible, Is.False);
                Assert.That(speaker.activeSelf, Is.True);

                hud.SetTalking(false);
                Assert.That(bar.activeSelf, Is.True);
                Assert.That(survey.IsEntryVisible, Is.True);
                Assert.That(speaker.activeSelf, Is.True);
            }
            finally
            {
                SurveyPrompt.RestoreEntry();
            }
        }
    }
}
