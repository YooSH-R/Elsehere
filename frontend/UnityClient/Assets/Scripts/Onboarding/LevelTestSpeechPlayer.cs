using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using UnityEngine;
using UnityEngine.Networking;

namespace A305.Onboarding
{
    /// <summary>레벨 테스트 듣기 문항 음성(ONB-10, WAV)을 받아 들려준다. 다시 들을 때는 받아 둔 음성을 쓴다.</summary>
    public class LevelTestSpeechPlayer : MonoBehaviour
    {
        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        private AudioSource speechSource;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        /// <summary>음성을 받는 중인지. 받는 동안 [다시 듣기]를 막는다.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>문항 음성을 처음부터 들려준다. 받지 못하면 들려주지 않고 false를 돌려준다.</summary>
        public async Task<bool> PlayAsync(string itemCode)
        {
            if (!clips.TryGetValue(itemCode, out AudioClip clip))
            {
                IsLoading = true;
                try
                {
                    clip = await DownloadAsync(itemCode);
                }
                finally
                {
                    IsLoading = false;
                }

                if (clip == null || this == null)
                {
                    return false;
                }

                clips[itemCode] = clip;
            }

            speechSource.Stop();
            speechSource.clip = clip;
            speechSource.Play();
            return true;
        }

        public void Stop()
        {
            speechSource.Stop();
        }

        private async Task<AudioClip> DownloadAsync(string itemCode)
        {
            using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                apiSettings.BaseUrl.TrimEnd('/') + OnboardingApi.LevelTestSpeechPath(itemCode),
                AudioType.WAV
            );
            await Awaitable.FromAsyncOperation(request.SendWebRequest());
            return request.result == UnityWebRequest.Result.Success
                ? DownloadHandlerAudioClip.GetContent(request)
                : null;
        }
    }
}
