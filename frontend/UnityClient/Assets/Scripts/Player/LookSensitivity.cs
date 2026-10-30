using System;
using UnityEngine;

namespace A305.Player
{
    /// <summary>
    /// 사용자가 고른 시점 회전 감도 배율. 기본 감도(PlayerCameraSettings)에 곱한다.
    /// 마우스와 화면 배율은 기기마다 달라 서버가 아니라 이 기기(Web 빌드는 브라우저)에 저장한다.
    /// </summary>
    public static class LookSensitivity
    {
        public const float Min = 0.5f;
        public const float Max = 2f;
        public const float Default = 1f;

        private const string PreferenceKey = "A305.Player.LookSensitivity";

        private static float? current;

        public static event Action<float> Changed;

        public static float Current
        {
            get
            {
                current ??= Clamp(PlayerPrefs.GetFloat(PreferenceKey, Default));
                return current.Value;
            }
            set
            {
                current = Clamp(value);
                PlayerPrefs.SetFloat(PreferenceKey, current.Value);
                PlayerPrefs.Save();
                Changed?.Invoke(current.Value);
            }
        }

        public static float Clamp(float value)
        {
            return float.IsNaN(value) ? Default : Mathf.Clamp(value, Min, Max);
        }

        /// <summary>저장된 값을 다시 읽는다. 다른 창에서 바꾼 값을 반영하거나 테스트에서 저장을 확인할 때 쓴다.</summary>
        public static void Reload()
        {
            current = null;
        }
    }
}
