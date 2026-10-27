using UnityEngine;

namespace A305.Voice
{
    /// <summary>녹음 자동 종료 규칙. 시간 기준은 API 명세 CONV-02, 무음 기준 음량은 실제 기기에서 조정한다.</summary>
    [CreateAssetMenu(fileName = "VoiceRecorderSettings", menuName = "A305/Voice/Recorder Settings")]
    public class VoiceRecorderSettings : ScriptableObject
    {
        [SerializeField]
        [Min(1f)]
        [Tooltip("이 시간에 닿으면 녹음을 끝낸다")]
        private float maxRecordingSeconds = 15f;

        [SerializeField]
        [Min(0.5f)]
        [Tooltip("조용한 상태가 이만큼 이어지면 녹음을 끝낸다")]
        private float silenceSeconds = 3f;

        [SerializeField]
        [Range(0.001f, 0.2f)]
        [Tooltip(
            "이 음량(RMS)보다 작으면 조용한 것으로 본다. 주변이 시끄러우면 올리고, 작게 말해도 끊기면 내린다"
        )]
        private float silenceLevel = 0.02f;

        public float MaxRecordingSeconds => maxRecordingSeconds;
        public float SilenceSeconds => silenceSeconds;
        public float SilenceLevel => silenceLevel;
    }
}
