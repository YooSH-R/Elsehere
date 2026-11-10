using UnityEngine;

namespace A305.Ambience
{
    public static class BackgroundSoundPreference
    {
        public const string Key = "elsehere.audio.background.enabled";
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
