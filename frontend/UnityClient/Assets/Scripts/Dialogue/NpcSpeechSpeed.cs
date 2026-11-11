using System;
using UnityEngine;

namespace A305.Dialogue
{
    /// <summary>
    /// 사용자가 마이페이지에서 고른 NPC 대화 음성 빠르기 배율(S15P31A305-778). 서버가 NPC 대사 음성을 만들 때 쓰므로
    /// 대화 시작·발화 전송·음성 다시 만들기 요청마다 실어 보낸다. 범위는 서버 검증(0.8~1.5배)과 같다.
    /// 시점 감도처럼 서버가 아니라 이 기기(Web 빌드는 브라우저)에 저장한다.
    /// </summary>
    public static class NpcSpeechSpeed
    {
        public const float Min = 0.8f;
        public const float Max = 1.5f;
        public const float Default = 1.1f;

        private const string PreferenceKey = "A305.Dialogue.NpcSpeechSpeed";

        private static float? current;

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
            }
        }

        /// <summary>요청 본문에 싣는 값. float 그대로 보내면 1.3이 1.2999999로 나가므로 슬라이더 단위로 맞춘다.</summary>
        public static double ForRequest => Math.Round(Current, 1);

        /// <summary>범위 안으로 자르고 슬라이더 단위(0.1)로 맞춘다.</summary>
        public static float Clamp(float value)
        {
            if (float.IsNaN(value))
            {
                return Default;
            }

            return Mathf.Round(Mathf.Clamp(value, Min, Max) * 10f) / 10f;
        }

        /// <summary>저장된 값을 다시 읽는다. 테스트에서 저장을 확인할 때 쓴다.</summary>
        public static void Reload()
        {
            current = null;
        }
    }
}
