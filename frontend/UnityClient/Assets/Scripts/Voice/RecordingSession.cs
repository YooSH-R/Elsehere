namespace A305.Voice
{
    /// <summary>
    /// 녹음 한 번의 진행과 자동 종료 규칙(API 명세 CONV-02). 최대 시간에 닿거나, 음량이 기준보다 작은 상태가 정한 시간만큼 이어지면 끝낸다.
    /// </summary>
    /// <remarks>
    /// 시각과 음량은 호출하는 쪽이 매 프레임 넘긴다. 녹음을 시작한 순간부터 조용한 시간을 세고, 기준보다 큰 소리가 들리면 멈췄다가
    /// 다시 조용해진 첫 프레임부터 새로 센다.
    /// </remarks>
    public sealed class RecordingSession
    {
        private readonly float maxSeconds;
        private readonly float silenceSeconds;
        private readonly float silenceLevel;

        private float startedAt;

        // 조용해지기 시작한 시각. 소리가 들리는 동안은 null이다.
        private float? quietSince;

        /// <param name="silenceLevel">이 음량(RMS)보다 작으면 조용한 것으로 본다.</param>
        public RecordingSession(float maxSeconds, float silenceSeconds, float silenceLevel)
        {
            this.maxSeconds = maxSeconds;
            this.silenceSeconds = silenceSeconds;
            this.silenceLevel = silenceLevel;
        }

        public bool IsRecording { get; private set; }

        /// <summary>끝난 이유. 녹음 중이거나 시작하기 전이면 null이다.</summary>
        public RecordingEndReason? EndReason { get; private set; }

        /// <summary>녹음한 시간. 끝나면 그 시점에서 멈춘다.</summary>
        public float ElapsedSeconds { get; private set; }

        public void Start(float now)
        {
            startedAt = now;
            quietSince = now;
            ElapsedSeconds = 0f;
            EndReason = null;
            IsRecording = true;
        }

        /// <returns>이번 호출로 자동 종료했으면 true.</returns>
        public bool Update(float now, float level)
        {
            if (!IsRecording)
            {
                return false;
            }

            ElapsedSeconds = now - startedAt;
            if (level >= silenceLevel)
            {
                quietSince = null;
            }
            else if (quietSince == null)
            {
                quietSince = now;
            }

            if (ElapsedSeconds >= maxSeconds)
            {
                End(now, RecordingEndReason.MaxDuration);
                return true;
            }

            if (quietSince != null && now - quietSince.Value >= silenceSeconds)
            {
                End(now, RecordingEndReason.Silence);
                return true;
            }

            return false;
        }

        /// <summary>[누르고 말하기] 버튼을 뗐다.</summary>
        public void Stop(float now)
        {
            End(now, RecordingEndReason.UserStopped);
        }

        /// <summary>녹음 중 다른 탭·창으로 갔다.</summary>
        public void Interrupt(float now)
        {
            End(now, RecordingEndReason.Interrupted);
        }

        private void End(float now, RecordingEndReason reason)
        {
            if (!IsRecording)
            {
                return;
            }

            ElapsedSeconds = now - startedAt;
            EndReason = reason;
            IsRecording = false;
        }
    }
}
