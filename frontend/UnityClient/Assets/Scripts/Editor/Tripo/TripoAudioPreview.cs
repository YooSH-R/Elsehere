using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.Tripo
{
    // Unity 6000.3의 내부 오디오 미리보기 API를 이 어댑터에 한정한다.
    // 버전 변경 시 API를 확인할 수 없으면 재생을 막으며, 타이머를 음성 시계로 가장하지 않는다.
    public sealed class TripoAudioPreview : IDisposable
    {
        private static readonly Type AudioType = typeof(AudioImporter).Assembly.GetType(
            "UnityEditor.AudioUtil"
        );
        private static readonly MethodInfo PlayMethod = Find(
            "PlayPreviewClip",
            typeof(AudioClip),
            typeof(int),
            typeof(bool)
        );
        private static readonly MethodInfo StopMethod = Find("StopAllPreviewClips");
        private static readonly MethodInfo PositionMethod = Find("GetPreviewClipPosition");
        private static readonly MethodInfo PlayingMethod = Find("IsPreviewClipPlaying");
        private bool ownsPlayback;

        public static bool IsSupported =>
            PlayMethod != null
            && StopMethod != null
            && PositionMethod != null
            && PlayingMethod != null;
        public bool IsPlaying => ownsPlayback && (bool)PlayingMethod.Invoke(null, null);
        public float Position => ownsPlayback ? (float)PositionMethod.Invoke(null, null) : 0f;

        public void Play(AudioClip clip)
        {
            if (!IsSupported)
                throw new InvalidOperationException(
                    "현재 Unity 버전에서 음성 미리보기를 사용할 수 없습니다."
                );
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));
            PlayMethod.Invoke(null, new object[] { clip, 0, false });
            ownsPlayback = true;
        }

        public void Dispose()
        {
            if (ownsPlayback)
            {
                StopMethod.Invoke(null, null);
                ownsPlayback = false;
            }
        }

        private static MethodInfo Find(string name, params Type[] parameters)
        {
            return AudioType?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                parameters,
                null
            );
        }
    }
}
