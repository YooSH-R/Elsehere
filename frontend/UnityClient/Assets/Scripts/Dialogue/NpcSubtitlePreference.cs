using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 사용자가 NPC 대사 자막을 켜 두었는지. 듣고 이해하는 연습이 목적이라 처음에는 꺼져 있다.
    /// 화면 취향이라 서버가 아니라 이 기기(Web 빌드는 브라우저)에 저장한다.
    /// </summary>
    public static class NpcSubtitlePreference
    {
        public const string PreferenceKey = "A305.Dialogue.ShowsNpcSubtitles";

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
