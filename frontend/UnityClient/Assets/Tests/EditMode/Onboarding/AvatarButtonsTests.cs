using A305.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Onboarding.Tests
{
    /// <summary>가입·게스트 화면의 캐릭터 버튼이 고른 쪽에만 체크를 보이는지 확인한다.</summary>
    public class AvatarButtonsTests
    {
        private Button female;
        private Button male;

        [SetUp]
        public void CreateButtons()
        {
            female = CreateButton("FemaleButton");
            male = CreateButton("MaleButton");
        }

        [TearDown]
        public void DestroyButtons()
        {
            Object.DestroyImmediate(female.gameObject);
            Object.DestroyImmediate(male.gameObject);
        }

        [Test]
        public void ShowSelected_ShowsCheckOnlyOnChosenCharacter()
        {
            AvatarButtons.ShowSelected(female, male, AvatarType.F);

            Assert.That(Check(female).activeSelf, Is.True);
            Assert.That(Check(male).activeSelf, Is.False);
        }

        [Test]
        public void ShowSelected_ChangingChoice_MovesTheCheck()
        {
            AvatarButtons.ShowSelected(female, male, AvatarType.F);

            AvatarButtons.ShowSelected(female, male, AvatarType.M);

            Assert.That(Check(female).activeSelf, Is.False);
            Assert.That(Check(male).activeSelf, Is.True);
        }

        [Test]
        public void ShowSelected_NothingChosen_HidesBothChecks()
        {
            AvatarButtons.ShowSelected(female, male, null);

            Assert.That(Check(female).activeSelf, Is.False);
            Assert.That(Check(male).activeSelf, Is.False);
        }

        private static Button CreateButton(string name)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            button.AddComponent<ThemedElement>();
            var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(button.transform, false);
            return button.GetComponent<Button>();
        }

        private static GameObject Check(Button button) => button.transform.Find("Check").gameObject;
    }
}
