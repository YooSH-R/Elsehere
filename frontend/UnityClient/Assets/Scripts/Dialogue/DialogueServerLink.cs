using System;
using System.Collections.Generic;
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

        // 스트리밍으로 받은 대답의 조각 음성. 앞 조각이 끝나면 다음 조각을 재생한다.
        private readonly Queue<SpeechChunk> chunkQueue = new Queue<SpeechChunk>();
        private readonly List<AudioClip> chunkClips = new List<AudioClip>();
        private int chunkReply = -1;

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

        public IAiStatusApi CreateAiStatusApi()
        {
            return new AiStatusApi(apiSettings.CreateClient());
        }

        public IVoiceConsentApi CreateVoiceConsentApi()
        {
            return new VoiceConsentApi(apiSettings.CreateClient());
        }

        public IFollowUpApi CreateFollowUpApi()
        {
            return new FollowUpApi(apiSettings.CreateClient());
        }

        /// <summary>
        /// 기존 GET 음성에서 PCM과 음소 시간을 함께 읽는다. 음성을 다시 합성하지 않는다.
        /// 404는 ApiException으로 알린다. 그 밖의 실패나 이전 대사의 늦은 응답은 false를 반환한다.
        /// </summary>
        /// <param name="url">서버가 준 다시 듣기 주소(/api/v1/turns/{id}/speech)</param>
        public async Task<bool> PlaySpeechAsync(string url)
        {
            StopSpeech();
            ReleaseVoice();
            lastSpeech.Expect(url);
            int request = playbackRequest;
            byte[] bytes;
            try
            {
                bytes = await DownloadSpeechAsync(url);
            }
            catch (ApiException)
                when (this == null || request != playbackRequest || !isActiveAndEnabled)
            {
                return false;
            }
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
                if (request.responseCode == 404)
                    throw new ApiException("NOT_FOUND", 404, "음성이 없습니다.");
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
            if (!TryCreateVoice(bytes, out AudioClip clip, out SpeechVisemeTimeline timeline))
                return false;
            if (!lastSpeech.TryFill(url, clip))
            {
                Destroy(clip);
                return false;
            }
            lastTimeline = timeline;
            ReplaySpeech();
            return true;
        }

        // 음소 시간이 든 WAV를 재생할 음성으로 만든다. 읽을 수 없는 음성은 경고만 남기고 재생하지 않는다.
        private bool TryCreateVoice(
            byte[] bytes,
            out AudioClip clip,
            out SpeechVisemeTimeline timeline
        )
        {
            clip = null;
            timeline = null;
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
            clip = AudioClip.Create(
                "NPC dialogue voice",
                decoded.Samples.Length / decoded.Channels,
                decoded.Channels,
                decoded.Frequency,
                false
            );
            clip.SetData(decoded.Samples, 0);
            timeline = decoded.Timeline;
            LipSyncNotice = speakerNotice ?? decoded.TimingError;
            if (speaker != null && LipSyncNotice != null)
                Debug.LogWarning("NPC lip sync unavailable: " + LipSyncNotice, this);
            return true;
        }

        /// <summary>
        /// 스트리밍으로 받은 NPC 대답의 문장 조각 음성을 받은 순서대로 이어 재생한다. 새 대답의 조각이 오면 지난 대사의 음성과 지난 대답의
        /// 남은 조각을 버린다. 조각은 [다시 듣기]에 쓰지 않는다([다시 듣기]는 턴 전체 음성을 받아 재생한다).
        /// </summary>
        /// <param name="reply">어느 대답의 조각인지(<see cref="NpcSpeechChunkData.Reply"/>)</param>
        public void QueueSpeechChunk(int reply, byte[] wav)
        {
            if (reply != chunkReply)
            {
                StopSpeech();
                ReleaseVoice();
                chunkReply = reply;
            }

            if (!TryCreateVoice(wav, out AudioClip clip, out SpeechVisemeTimeline timeline))
                return;
            chunkClips.Add(clip);
            chunkQueue.Enqueue(new SpeechChunk(clip, timeline));
            PlayNextChunk();
        }

        private void Update() => PlayNextChunk();

        private void PlayNextChunk()
        {
            if (chunkQueue.Count == 0 || IsSpeaking)
                return;
            SpeechChunk next = chunkQueue.Dequeue();
            Play(next.Clip, next.Timeline);
        }

        private bool IsSpeaking =>
            speaker != null ? speaker.IsPlaying : speechSource != null && speechSource.isPlaying;

        /// <summary>지금 대사의 음성을 처음부터 다시 재생한다.</summary>
        public void ReplaySpeech()
        {
            AudioClip clip = lastSpeech.Clip;
            if (clip == null)
                return;
            Play(clip, lastTimeline);
        }

        private void Play(AudioClip clip, SpeechVisemeTimeline timeline)
        {
            if (speaker != null)
                speaker.Play(clip, timeline);
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
            chunkQueue.Clear();
            if (speaker != null)
                speaker.Stop();
            if (speechSource != null)
                speechSource.Stop();
        }

        private void ReleaseVoice()
        {
            if (speechSource != null)
                speechSource.clip = null;
            if (lastSpeech.Clip != null)
                Destroy(lastSpeech.Clip);
            lastSpeech.Clear();
            lastTimeline = null;
            LipSyncNotice = null;
            foreach (AudioClip chunk in chunkClips)
                Destroy(chunk);
            chunkClips.Clear();
        }

        private void OnDisable() => StopSpeech();

        private readonly struct SpeechChunk
        {
            public SpeechChunk(AudioClip clip, SpeechVisemeTimeline timeline)
            {
                Clip = clip;
                Timeline = timeline;
            }

            public AudioClip Clip { get; }
            public SpeechVisemeTimeline Timeline { get; }
        }

        private void OnDestroy()
        {
            StopSpeech();
            ReleaseVoice();
        }
    }
}
