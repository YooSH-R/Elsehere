using System;
using System.Threading.Tasks;
using A305.Api;
using A305.Characters;
using A305.Voice;
using UnityEngine;
using UnityEngine.Networking;

namespace A305.Dialogue
{
    /// <summary>대화 서버 연결과 NPC 음성의 수명·재생을 관리한다.</summary>
    public class DialogueServerLink : MonoBehaviour
    {
        private const int TurnTimeoutSeconds = 40;
        private const int FeedbackTimeoutSeconds = 90;

        [SerializeField]
        [Tooltip("끄면 모든 대화를 확인용 대본으로 진행한다")]
        private bool useServer = true;

        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        private AudioSource speechSource;

        [SerializeField]
        private VoiceRecorder voiceRecorder;

        private readonly NpcSpeechSlot lastSpeech = new NpcSpeechSlot();
        private SpeechVisemeTimeline lastTimeline;
        private CharacterSpeechPlayer speaker;
        private string speakerNotice;
        private int playbackRequest;
        private UnityWebRequest pendingRequest;

        public bool IsAvailable => useServer && apiSettings != null;
        public VoiceRecorder VoiceRecorder => voiceRecorder;

        /// <summary>지금 대사의 음성을 받아 두었는지. 지난 대사의 음성은 세지 않는다.</summary>
        public bool HasSpeech => lastSpeech.Clip != null;
        public string LipSyncNotice { get; private set; }

        public IConversationApi CreateApi() =>
            new ConversationApi(
                apiSettings.CreateClient(TurnTimeoutSeconds),
                apiSettings.CreateClient(FeedbackTimeoutSeconds)
            );

        public void SetSpeaker(CharacterSpeechPlayer player, string bindingNotice = null)
        {
            StopSpeech();
            ReleaseVoice();
            speaker = player;
            speakerNotice = bindingNotice;
            LipSyncNotice = bindingNotice;
        }

        /// <summary>
        /// 기존 GET 음성에서 PCM과 음소 시간을 함께 읽는다. 음성을 다시 합성하지 않는다.
        /// 받지 못하거나 받는 사이 새 대사로 바뀌었으면 재생하지 않고 false를 돌려준다(자막만 보여 준다).
        /// </summary>
        /// <param name="url">서버가 준 다시 듣기 주소(/api/v1/turns/{id}/speech)</param>
        public async Task<bool> PlaySpeechAsync(string url)
        {
            StopSpeech();
            ReleaseVoice();
            lastSpeech.Expect(url);
            int request = playbackRequest;
            byte[] bytes = await DownloadSpeechAsync(url);
            if (this == null || request != playbackRequest || !isActiveAndEnabled || bytes == null)
                return false;
            return PlayDownloadedVoice(url, bytes);
        }

        protected virtual async Task<byte[]> DownloadSpeechAsync(string url)
        {
            using var request = UnityWebRequest.Get(apiSettings.BaseUrl.TrimEnd('/') + url);
            request.timeout = TurnTimeoutSeconds;
            pendingRequest = request;
            try
            {
                await Awaitable.FromAsyncOperation(request.SendWebRequest());
                return request.result == UnityWebRequest.Result.Success
                    ? request.downloadHandler.data
                    : null;
            }
            finally
            {
                if (pendingRequest == request)
                    pendingRequest = null;
            }
        }

        /// <summary>오프라인 검수에서도 실제 대화와 같은 디코딩·재생 경로를 사용한다.</summary>
        public bool PlayRecordedSpeech(byte[] wav)
        {
            StopSpeech();
            ReleaseVoice();
            // 검수용 녹음에는 서버 주소가 없어 주소 없이 받는다.
            lastSpeech.Expect(null);
            return PlayDownloadedVoice(null, wav);
        }

        private bool PlayDownloadedVoice(string url, byte[] bytes)
        {
            DialogueSpeechWav decoded;
            try
            {
                decoded = DialogueSpeechWav.Read(bytes);
            }
            catch (ArgumentException error)
            {
                Debug.LogWarning("NPC voice rejected: " + error.Message, this);
                return false;
            }
            var clip = AudioClip.Create(
                "NPC dialogue voice",
                decoded.Samples.Length / decoded.Channels,
                decoded.Channels,
                decoded.Frequency,
                false
            );
            clip.SetData(decoded.Samples, 0);
            if (!lastSpeech.TryFill(url, clip))
            {
                Destroy(clip);
                return false;
            }
            lastTimeline = decoded.Timeline;
            LipSyncNotice = speakerNotice ?? decoded.TimingError;
            if (speaker != null && LipSyncNotice != null)
                Debug.LogWarning("NPC lip sync unavailable: " + LipSyncNotice, this);
            ReplaySpeech();
            return true;
        }

        /// <summary>지금 대사의 음성을 처음부터 다시 재생한다.</summary>
        public void ReplaySpeech()
        {
            AudioClip clip = lastSpeech.Clip;
            if (clip == null)
                return;
            if (speaker != null)
                speaker.Play(clip, lastTimeline);
            else if (speechSource != null)
            {
                speechSource.clip = clip;
                speechSource.Play();
            }
        }

        /// <summary>
        /// 새 대사에 음성이 없을 때(합성 실패 등) 지난 대사의 음성을 멈추고 버린다. [다시 듣기]가 지난 대사를 재생하지 않고 음성을 다시 만든다.
        /// </summary>
        public void ForgetSpeech()
        {
            StopSpeech();
            ReleaseVoice();
        }

        public void StopSpeech()
        {
            playbackRequest++;
            pendingRequest?.Abort();
            if (speaker != null)
                speaker.Stop();
            if (speechSource != null)
                speechSource.Stop();
        }

        private void ReleaseVoice()
        {
            if (lastSpeech.Clip != null)
                Destroy(lastSpeech.Clip);
            lastSpeech.Clear();
            lastTimeline = null;
            LipSyncNotice = null;
        }

        private void OnDisable() => StopSpeech();

        private void OnDestroy()
        {
            StopSpeech();
            ReleaseVoice();
        }
    }
}
