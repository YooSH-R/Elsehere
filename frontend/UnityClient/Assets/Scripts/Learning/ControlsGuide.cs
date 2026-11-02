using UnityEngine;

namespace A305.Learning
{
    /// <summary>
    /// 오른쪽 아래 조작법 카드를 보일지 정한다. X로 닫으면 기억해 다음에 들어와도 띄우지 않고, 메뉴(≡)의 [조작법]으로 다시 켠다.
    /// PC와 휴대폰은 보이는 조작이 달라 서버가 아니라 이 기기(Web 빌드는 브라우저)에 저장한다.
    /// </summary>
    public static class ControlsGuide
    {
        public const string PreferenceKey = "A305.Learning.ControlsGuideClosed";

        private static bool? closed;

        public static bool IsClosed
        {
            get
            {
                closed ??= PlayerPrefs.GetInt(PreferenceKey, 0) == 1;
                return closed.Value;
            }
        }

        public static void Close() => SetClosed(true);

        public static void Reopen() => SetClosed(false);

        /// <param name="isBlocked">대화 중이거나 화면을 덮는 창이 열려 있으면 true. 닫은 기록은 그대로 두고 잠시 숨긴다.</param>
        public static bool ShouldShow(bool isBlocked) => !IsClosed && !isBlocked;

        /// <summary>저장된 값을 다시 읽는다. 테스트에서 저장을 확인할 때 쓴다.</summary>
        public static void Reload()
        {
            closed = null;
        }

        private static void SetClosed(bool value)
        {
            closed = value;
            PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
