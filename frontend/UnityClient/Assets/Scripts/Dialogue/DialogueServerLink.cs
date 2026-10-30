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

        private AudioClip lastSpeech;
        private SpeechVisemeTimeline lastTimeline;
        private CharacterSpeechPlayer speaker;
        private string speakerNotice;
        private int playbackRequest;
        private UnityWebRequest pendingRequest;

        public bool IsAvailable => useServer && apiSettings != null;
        public VoiceRecorder VoiceRecorder => voiceRecorder;
        public bool HasSpeech => lastSpeech != null;
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

        /// <summary>기존 GET 음성에서 PCM과 음소 시간을 함께 읽는다. 음성을 다시 합성하지 않는다.</summary>
        public async Task<bool> PlaySpeechAsync(string url)
        {
            StopSpeech();
            ReleaseVoice();
            int request = playbackRequest;
            byte[] bytes = await DownloadSpeechAsync(url);
            if (this == null || request != playbackRequest || !isActiveAndEnabled || bytes == null)
                return false;
            return PlayDownloadedVoice(bytes);
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
            return PlayDownloadedVoice(wav);
        }

        private bool PlayDownloadedVoice(byte[] bytes)
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
            lastSpeech = AudioClip.Create(
                "NPC dialogue voice",
                decoded.Samples.Length / decoded.Channels,
                decoded.Channels,
                decoded.Frequency,
                false
            );
            lastSpeech.SetData(decoded.Samples, 0);
            lastTimeline = decoded.Timeline;
            LipSyncNotice = speakerNotice ?? decoded.TimingError;
            if (speaker != null && LipSyncNotice != null)
                Debug.LogWarning("NPC lip sync unavailable: " + LipSyncNotice, this);
            ReplaySpeech();
            return true;
        }

        public void ReplaySpeech()
        {
            if (lastSpeech == null)
                return;
            if (speaker != null)
                speaker.Play(lastSpeech, lastTimeline);
            else if (speechSource != null)
            {
                speechSource.clip = lastSpeech;
                speechSource.Play();
            }
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
            if (lastSpeech != null)
                Destroy(lastSpeech);
            lastSpeech = null;
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
