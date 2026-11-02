using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>
    /// Editor에서 서버에 연결하지 못했을 때 쓰는 확인용 단어 카드. 장소의 Unity 단어로 카드를 만들고,
    /// [저장]·[알아요]로 바꾼 상태는 같은 Play 동안만 기억한다. 발음 음성은 없고 빌드에서는 쓰지 않는다.
    /// </summary>
    public sealed class LocalObjectWordApi : IObjectWordApi
    {
        // 장소를 옮겨도 같은 Play 동안은 저장·숨김을 기억한다.
        private static readonly HashSet<string> SavedKeys = new HashSet<string>();
        private static readonly HashSet<string> HiddenKeys = new HashSet<string>();

        private readonly List<ObjectWordDetails> sceneWords = new List<ObjectWordDetails>();

        public LocalObjectWordApi(IEnumerable<ObjectWordDetails> sceneWords)
        {
            var keys = new HashSet<string>();
            foreach (ObjectWordDetails word in sceneWords)
            {
                if (word != null && !string.IsNullOrEmpty(word.Key) && keys.Add(word.Key))
                {
                    this.sceneWords.Add(word);
                }
            }
        }

        /// <summary>기억한 저장·숨김을 지운다. Play를 시작할 때마다 부른다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Forget()
        {
            SavedKeys.Clear();
            HiddenKeys.Clear();
        }

        public Task<SceneWords> GetSceneWordsAsync(string sceneCode)
        {
            var scene = new SceneWords();
            for (int index = 0; index < sceneWords.Count; index++)
            {
                ObjectWordDetails word = sceneWords[index];
                scene.Words.Add(
                    new WordCardData
                    {
                        WordKey = word.Key,
                        Vocab = new WordCardVocab
                        {
                            VocabId = index + 1,
                            Text = word.Text,
                            MeaningKo = word.MeaningKo,
                            Example = word.Example,
                            ExampleKo = word.ExampleKo,
                        },
                        HasPronunciation = false,
                        Saved = SavedKeys.Contains(word.Key),
                        HoverHidden = HiddenKeys.Contains(word.Key),
                    }
                );
            }

            return Task.FromResult(scene);
        }

        public Task SaveAsync(string wordKey, string requestId)
        {
            SavedKeys.Add(wordKey);
            return Task.CompletedTask;
        }

        public Task SetHoverHiddenAsync(string wordKey, bool hoverHidden)
        {
            if (hoverHidden)
            {
                HiddenKeys.Add(wordKey);
            }
            else
            {
                HiddenKeys.Remove(wordKey);
            }

            return Task.CompletedTask;
        }
    }
}
