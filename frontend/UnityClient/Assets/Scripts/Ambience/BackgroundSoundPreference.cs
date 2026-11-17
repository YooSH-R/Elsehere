using UnityEngine;

namespace A305.Ambience
{
    /// <summary>
    /// 설정 창의 효과음 [켜기]·[끄기]와 크기 막대로 고른 배경 소리(생활소음·환경 효과음·발자국·UI 효과음·접속 음악) 설정. 기기의 PlayerPrefs에 남는다.
    /// 켜고 끄기(<see cref="Enabled"/>)와 크기(<see cref="Volume"/>)를 따로 기억해, 껐다 켜면 고른 크기로 돌아온다. NPC 음성에는 쓰지 않는다.
    /// </summary>
    public static class BackgroundSoundPreference
    {
        public const string Key = "elsehere.audio.background.enabled";
        public const string VolumeKey = "elsehere.audio.background.volume";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>마지막으로 고른 크기(0~1, 처음에는 1). 꺼 둔 동안에도 남아 있다.</summary>
        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1));
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>배경 소리에 곱할 지금 크기. 꺼 두면 0이다.</summary>
        public static float Level => Enabled ? Volume : 0;

        /// <summary>막대로 고른 크기. 0이면 끄고(전 크기는 남김), 0보다 크면 그 크기로 켠다.</summary>
        public static void SetLevel(float level)
        {
            if (level <= 0)
            {
                Enabled = false;
                return;
            }
            Volume = level;
            Enabled = true;
        }

        /// <summary>막대 옆 스피커: 켜져 있으면 끄고, 꺼져 있으면 마지막으로 고른 크기로 켠다.</summary>
        public static void ToggleMute() => Enabled = !Enabled;
    }
}
