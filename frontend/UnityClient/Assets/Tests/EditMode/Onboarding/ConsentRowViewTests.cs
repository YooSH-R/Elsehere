using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace A305.Onboarding.Tests
{
    public class ConsentRowViewTests
    {
        private GameObject rowObject;
        private GameObject eventSystemObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(rowObject);
            Object.DestroyImmediate(eventSystemObject);
        }

        [Test]
        public void GuestVoiceConsent_ClickingLabel_ChangesConsent()
        {
            ConsentRowView row = CreateGuestVoiceConsentRow();
            var label = rowObject.transform.Find("Label").GetComponent<TMP_Text>();
            row.Show(new ConsentTerm { Title = "음성 처리 동의" });
            row.Toggle.SetIsOnWithoutNotify(false);
            PointerEventData click = CreateClick();

            Assert.That(label.raycastTarget, Is.True, "동의 문구가 클릭 대상이어야 합니다.");
            ExecuteEvents.ExecuteHierarchy(
                label.gameObject,
                click,
                ExecuteEvents.pointerClickHandler
            );

            Assert.That(row.Toggle.isOn, Is.True);

            ExecuteEvents.ExecuteHierarchy(
                label.gameObject,
                click,
                ExecuteEvents.pointerClickHandler
            );

            Assert.That(row.Toggle.isOn, Is.False);
        }

        [Test]
        public void GuestVoiceConsent_ClickingViewButton_DoesNotChangeConsent()
        {
            ConsentRowView row = CreateGuestVoiceConsentRow();
            row.Show(new ConsentTerm { Title = "음성 처리 동의", Url = "https://example.com" });
            row.Toggle.SetIsOnWithoutNotify(false);
            bool documentRequested = false;
            row.ViewButton.onClick.AddListener(() => documentRequested = true);

            ExecuteEvents.ExecuteHierarchy(
                row.ViewButton.gameObject,
                CreateClick(),
                ExecuteEvents.pointerClickHandler
            );

            Assert.That(documentRequested, Is.True);
            Assert.That(row.Toggle.isOn, Is.False);
        }

        private ConsentRowView CreateGuestVoiceConsentRow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Onboarding/EntryScreens.prefab"
            );
            Transform template = prefab.transform.Find("GuestPanel/VoiceConsentRow");
            rowObject = Object.Instantiate(template.gameObject);
            rowObject.SetActive(true);
            return rowObject.GetComponent<ConsentRowView>();
        }

        private PointerEventData CreateClick()
        {
            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            return new PointerEventData(eventSystemObject.GetComponent<EventSystem>());
        }
    }
}
