using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// NPC 대사 자막은 기본으로 꺼서 듣고 이해하게 하고, 사용자가 선택한 상태를 이 기기(웹은 브라우저)에 저장한다.
    /// </summary>
    public static class NpcSubtitlePreference
    {
        // 이전 버전이 대화를 열 때마다 저장한 꺼짐 값은 사용자 선택과 구분할 수 없어 이전 키는 읽지 않는다.
        public const string PreferenceKey = "A305.Dialogue.NpcSubtitles";

        private static bool? showsSubtitles;

        public static bool ShowsSubtitles
        {
            get
            {
                showsSubtitles ??= PlayerPrefs.GetInt(PreferenceKey, 0) == 1;
                return showsSubtitles.Value;
            }
            set
            {
                showsSubtitles = value;
                PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>저장된 값을 다시 읽는다. 테스트에서 저장을 확인할 때 쓴다.</summary>
        public static void Reload()
        {
            showsSubtitles = null;
        }
    }
}
