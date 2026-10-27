using System.Threading.Tasks;
using A305.Api;
using A305.Voice;
using UnityEngine;
using UnityEngine.Networking;

namespace A305.Dialogue
{
    /// <summary>
    /// 서버 AI 대화에 필요한 연결. 대화 화면 프리팹에 붙어 있어 공간 씬마다 따로 배치하지 않는다. API 주소, NPC 음성 재생, 마이크 녹음을
    /// 맡는다.
    /// </summary>
    public class DialogueServerLink : MonoBehaviour
    {
        // NPC 대답은 대답 생성과 음성 합성까지 기다린다. 종료 피드백은 워커 상한(60초)보다 길게 기다린다.
        private const int TurnTimeoutSeconds = 40;
        private const int FeedbackTimeoutSeconds = 90;

        [SerializeField]
        [Tooltip("끄면 모든 대화를 확인용 대본으로 진행한다(서버 없이 화면만 확인할 때)")]
        private bool useServer = true;

        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        private AudioSource speechSource;

        [SerializeField]
        private VoiceRecorder voiceRecorder;

        private AudioClip lastSpeech;

        public bool IsAvailable => useServer && apiSettings != null;

        public VoiceRecorder VoiceRecorder => voiceRecorder;

        public bool HasSpeech => lastSpeech != null;

        public IConversationApi CreateApi()
        {
            return new ConversationApi(
                apiSettings.CreateClient(TurnTimeoutSeconds),
                apiSettings.CreateClient(FeedbackTimeoutSeconds)
            );
        }

        /// <summary>
        /// NPC 턴 음성을 받아 재생한다. 받지 못하면 재생하지 않고 false를 돌려준다(자막만 보여 준다).
        /// </summary>
        /// <param name="url">서버가 준 다시 듣기 주소(/api/v1/turns/{id}/speech)</param>
        public async Task<bool> PlaySpeechAsync(string url)
        {
            StopSpeech();
            lastSpeech = null;
            using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                apiSettings.BaseUrl.TrimEnd('/') + url,
                AudioType.WAV
            );
            await Awaitable.FromAsyncOperation(request.SendWebRequest());
            if (this == null || request.result != UnityWebRequest.Result.Success)
            {
                return false;
            }

            lastSpeech = DownloadHandlerAudioClip.GetContent(request);
            speechSource.clip = lastSpeech;
            speechSource.Play();
            return true;
        }

        /// <summary>마지막으로 받은 NPC 음성을 처음부터 다시 재생한다.</summary>
        public void ReplaySpeech()
        {
            if (lastSpeech == null)
            {
                return;
            }

            speechSource.clip = lastSpeech;
            speechSource.Play();
        }

        public void StopSpeech()
        {
            if (speechSource != null)
            {
                speechSource.Stop();
            }
        }
    }
}
