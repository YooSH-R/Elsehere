using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using A305.Api;
using UnityEngine;
using UnityEngine.Networking;

namespace A305.ObjectHover
{
    /// <summary>
    /// 사물 카드의 서버 연결. 카드 프리팹에 붙어 있어 장소 씬마다 따로 배치하지 않는다. API 주소와 발음 재생을 맡는다.
    /// </summary>
    public class ObjectWordServerLink : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("끄면 서버 없이 Unity 단어로만 카드를 보인다(화면만 확인할 때)")]
        private bool useServer = true;

        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        private AudioSource pronunciationSource;

        // 같은 단어를 다시 들을 때 내려받지 않도록 장소에 있는 동안 받은 음성을 둔다.
        private readonly Dictionary<long, AudioClip> pronunciations =
            new Dictionary<long, AudioClip>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int A305Word_CanPronounce();

        [DllImport("__Internal")]
        private static extern int A305Word_Pronounce(string text);
#endif

        public bool BrowserPronunciationAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return A305Word_CanPronounce() != 0;
#else
                return false;
#endif
            }
        }

        public bool PronounceInBrowser(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return A305Word_Pronounce(text) != 0;
#else
            return false;
#endif
        }

        public bool IsAvailable => useServer && apiSettings != null;

        public IObjectWordApi CreateApi()
        {
            return new ObjectWordApi(apiSettings.CreateClient());
        }

        /// <summary>발음 음성(WORLD-04)을 받아 재생한다. 받지 못하면 재생하지 않고 false를 돌려준다.</summary>
        public async Task<bool> PlayPronunciationAsync(long vocabId)
        {
            if (!pronunciations.TryGetValue(vocabId, out AudioClip clip))
            {
                using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                    apiSettings.BaseUrl.TrimEnd('/') + ObjectWordApi.PronunciationPath(vocabId),
                    AudioType.WAV
                );
                await Awaitable.FromAsyncOperation(request.SendWebRequest());
                if (this == null || request.result != UnityWebRequest.Result.Success)
                {
                    return false;
                }

                clip = DownloadHandlerAudioClip.GetContent(request);
                pronunciations[vocabId] = clip;
            }

            pronunciationSource.Stop();
            pronunciationSource.clip = clip;
            pronunciationSource.Play();
            return true;
        }
    }
}
