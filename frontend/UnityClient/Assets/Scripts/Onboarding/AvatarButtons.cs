using A305.Theme;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 가입·게스트 화면의 [여성]·[남성] 버튼에 고른 표시를 한다. 고른 버튼은 테마 강조색으로 칠하고 자식 Check(체크 아이콘)를 보인다.
    /// </summary>
    public static class AvatarButtons
    {
        public static void ShowSelected(Button female, Button male, AvatarType? selected)
        {
            Show(female, selected == AvatarType.F);
            Show(male, selected == AvatarType.M);
        }

        private static void Show(Button button, bool selected)
        {
            button.GetComponent<ThemedElement>().SetSelected(selected);
            button.transform.Find("Check").gameObject.SetActive(selected);
        }
    }
}
