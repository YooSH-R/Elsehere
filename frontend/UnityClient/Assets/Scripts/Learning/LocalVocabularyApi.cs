using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using A305.ObjectHover;
using UnityEngine;

namespace A305.Learning
{
    /// <summary>
    /// Editor에서 서버 단어장을 읽지 못했을 때 쓰는 확인용 단어장. 같은 Play 동안 확인용 단어 카드(<see cref="LocalObjectWordApi"/>)에서
    /// [저장]한 단어를 최근 저장순으로 보이고, 외웠어요·메모·삭제도 같은 Play 동안만 기억한다. 빌드에서는 쓰지 않는다.
    /// </summary>
    public sealed class LocalVocabularyApi : IVocabularyApi
    {
        // 항목 ID는 단어를 처음 보인 순서로 정해, 다른 단어를 지워도 이미 보인 항목의 ID가 바뀌지 않게 한다.
        private static readonly List<string> KeysById = new List<string>();
        private static readonly Dictionary<string, string> Memos = new Dictionary<string, string>();
        private static readonly HashSet<string> LearnedKeys = new HashSet<string>();

        /// <summary>기억한 메모·외웠어요를 지운다. Play를 시작할 때마다 부른다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Forget()
        {
            KeysById.Clear();
            Memos.Clear();
            LearnedKeys.Clear();
        }

        public Task<VocabPage> GetPageAsync(string cursor)
        {
            var page = new VocabPage();
            IReadOnlyList<ObjectWordDetails> saved = LocalObjectWordApi.SavedWords;
            for (int index = saved.Count - 1; index >= 0; index--)
            {
                page.Items.Add(ToItem(saved[index]));
            }

            page.Counts = new VocabCounts { All = page.Items.Count };
            return Task.FromResult(page);
        }

        // 대사 단어 풀이(CONV-04)는 서버 대화에서만 받으므로, 서버 단어장을 읽지 못한 동안에는 저장하지 못했다고 알린다.
        public Task<VocabItem> SaveNpcLineWordAsync(long turnId, string text)
        {
            throw new ApiException(
                "VOCAB_UNAVAILABLE",
                503,
                "확인용 단어장에는 대사 단어를 저장하지 않습니다."
            );
        }

        public Task<VocabItem> SaveFeedbackWordAsync(long conversationId, string text)
        {
            throw new ApiException(
                "VOCAB_UNAVAILABLE",
                503,
                "확인용 단어장에는 피드백 표현을 저장하지 않습니다."
            );
        }

        public Task<VocabItem> UpdateMemoAsync(long userVocabId, string memo)
        {
            ObjectWordDetails word = Find(userVocabId);
            Memos[word.Key] = memo;
            return Task.FromResult(ToItem(word));
        }

        public Task<VocabItem> UpdateMemorizedAsync(long userVocabId, bool memorized)
        {
            ObjectWordDetails word = Find(userVocabId);
            if (memorized)
            {
                LearnedKeys.Add(word.Key);
            }
            else
            {
                LearnedKeys.Remove(word.Key);
            }

            return Task.FromResult(ToItem(word));
        }

        public Task DeleteAsync(long userVocabId)
        {
            ObjectWordDetails word = Find(userVocabId);
            Memos.Remove(word.Key);
            LearnedKeys.Remove(word.Key);
            LocalObjectWordApi.Unsave(word.Key);
            return Task.CompletedTask;
        }

        // 이미 지운 단어면 서버처럼 찾을 수 없다고 알린다.
        private static ObjectWordDetails Find(long userVocabId)
        {
            int index = (int)userVocabId - 1;
            string key = index >= 0 && index < KeysById.Count ? KeysById[index] : null;
            foreach (ObjectWordDetails word in LocalObjectWordApi.SavedWords)
            {
                if (word.Key == key)
                {
                    return word;
                }
            }

            throw new ApiException("NOT_FOUND", 404, "단어장에 없는 단어입니다.");
        }

        // 사물에서 저장한 단어는 서버처럼 사물 예문을 저장한 문장으로 보인다.
        private static VocabItem ToItem(ObjectWordDetails word)
        {
            int index = KeysById.IndexOf(word.Key);
            if (index < 0)
            {
                KeysById.Add(word.Key);
                index = KeysById.Count - 1;
            }

            return new VocabItem
            {
                UserVocabId = index + 1,
                Vocab = new VocabWord
                {
                    VocabId = index + 1,
                    Text = word.Text,
                    MeaningKo = word.MeaningKo,
                },
                Context = string.IsNullOrEmpty(word.Example)
                    ? null
                    : new VocabContext { Text = word.Example, MeaningKo = word.ExampleKo },
                Memo = Memos.TryGetValue(word.Key, out string memo) ? memo : null,
                Memorized = LearnedKeys.Contains(word.Key),
                Source = new VocabSource { SourceType = "OBJECT" },
            };
        }
    }
}
