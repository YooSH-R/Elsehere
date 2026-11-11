#if UNITY_EDITOR
using System.Collections;
using A305.Api;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace A305.Learning.PlayModeTests
{
    public sealed class SurveyEntryTests
    {
        private GameObject root;
        private bool hadInvitation;
        private int savedInvitation;

        [SetUp]
        public void SetUp()
        {
            hadInvitation = PlayerPrefs.HasKey(SurveyPrompt.InvitationKey);
            savedInvitation = PlayerPrefs.GetInt(SurveyPrompt.InvitationKey);
            PlayerPrefs.DeleteKey(SurveyPrompt.InvitationKey);
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Learning/LearningHud.prefab"
            );
            root = Object.Instantiate(hud.transform.Find("SurveyPrompt").gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            if (hadInvitation)
                PlayerPrefs.SetInt(SurveyPrompt.InvitationKey, savedInvitation);
            else
                PlayerPrefs.DeleteKey(SurveyPrompt.InvitationKey);
            SurveyPrompt.RestoreEntry();
        }

        [UnityTest]
        public IEnumerator CloseRevealsAnEntryThatReopensTheCard()
        {
            yield return null;
            var view = root.GetComponent<SurveyPromptView>();
            Assert.That(view.IsEntryVisible, Is.False);
            SurveyPrompt.RequestAfterFeedback(true);
            root.transform.Find("Card/CloseButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.IsShowing, Is.False);
            Assert.That(view.IsEntryVisible, Is.True);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(root.transform.Find("Card").gameObject.activeSelf, Is.False);
            root.transform.Find("EntryButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.IsShowing, Is.True);
            Assert.That(view.IsEntryVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator OpeningThePhone_DrawsItOverTheSurveyEntry()
        {
            Object.DestroyImmediate(root);
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Learning/LearningHud.prefab"
            );
            root = Object.Instantiate(hud);
            SurveyPrompt.RevealEntry();
            yield return null;

            var menu = root.GetComponent<HudMenuView>();
            menu.MenuButtonRect.GetComponent<Button>().onClick.Invoke();

            Transform entry = root.transform.Find("SurveyPrompt/EntryButton");
            Canvas entryCanvas = entry.GetComponentInParent<Canvas>();
            Assert.That(entry.gameObject.activeInHierarchy, Is.True);
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(entryCanvas.overrideSorting, Is.True);
            Assert.That(
                entryCanvas.sortingOrder,
                Is.LessThan(root.GetComponent<Canvas>().sortingOrder)
            );
        }

        [UnityTest]
        public IEnumerator AutomaticCollapsePreservesTheEntryAcrossSceneHudReplacement()
        {
            yield return null;
            SurveyPrompt.RequestAfterFeedback(true);
            yield return new WaitForSecondsRealtime(6.4f);
            Assert.That(root.GetComponent<SurveyPromptView>().IsShowing, Is.False);
            Assert.That(root.GetComponent<SurveyPromptView>().IsEntryVisible, Is.True);
            Object.DestroyImmediate(root);
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Learning/LearningHud.prefab"
            );
            root = Object.Instantiate(hud.transform.Find("SurveyPrompt").gameObject);
            yield return null;
            Assert.That(root.GetComponent<SurveyPromptView>().IsShowing, Is.False);
            Assert.That(root.GetComponent<SurveyPromptView>().IsEntryVisible, Is.True);
        }
    }
}
#endif
