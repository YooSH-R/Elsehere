using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;

namespace A305.ObjectHover.Tests
{
    /// <summary>서버 없이 사물 카드 API의 성공·실패와 응답 대기를 흉내 낸다.</summary>
    internal sealed class FakeObjectWordApi : IObjectWordApi
    {
        public SceneWords Scene { get; } = new SceneWords();

        public ApiException SceneError { get; set; }

        public string RequestedScene { get; private set; }

        public ApiException SaveError { get; set; }

        /// <summary>정하면 저장 응답이 이 작업이 끝날 때까지 오지 않는다.</summary>
        public TaskCompletionSource<bool> SaveGate { get; set; }

        public List<(string WordKey, string RequestId)> Saves { get; } =
            new List<(string WordKey, string RequestId)>();

        public ApiException PreferenceError { get; set; }

        public List<(string WordKey, bool HoverHidden)> Preferences { get; } =
            new List<(string WordKey, bool HoverHidden)>();

        public static ApiException Failure(string message)
        {
            return new ApiException(ApiErrorCodes.NetworkError, 0, message);
        }

        public FakeObjectWordApi AddWord(
            string wordKey,
            bool saved = false,
            bool hoverHidden = false,
            bool hasPronunciation = true
        )
        {
            Scene.Words.Add(
                new WordCardData
                {
                    WordKey = wordKey,
                    Vocab = new WordCardVocab
                    {
                        VocabId = 31,
                        Text = wordKey,
                        MeaningKo = "소파",
                        Example = "He is sitting on the sofa.",
                        ExampleKo = "그는 소파에 앉아 있어요.",
                    },
                    HasPronunciation = hasPronunciation,
                    Saved = saved,
                    HoverHidden = hoverHidden,
                }
            );
            return this;
        }

        public async Task<ObjectWordCards> LoadCardsAsync(string sceneCode = "HOME")
        {
            var cards = new ObjectWordCards(this);
            await cards.LoadAsync(sceneCode);
            return cards;
        }

        public Task<SceneWords> GetSceneWordsAsync(string sceneCode)
        {
            RequestedScene = sceneCode;
            if (SceneError != null)
            {
                throw SceneError;
            }

            return Task.FromResult(Scene);
        }

        public async Task SaveAsync(string wordKey, string requestId)
        {
            Saves.Add((wordKey, requestId));
            if (SaveGate != null)
            {
                await SaveGate.Task;
            }

            if (SaveError != null)
            {
                throw SaveError;
            }
        }

        public Task SetHoverHiddenAsync(string wordKey, bool hoverHidden)
        {
            Preferences.Add((wordKey, hoverHidden));
            if (PreferenceError != null)
            {
                throw PreferenceError;
            }

            return Task.CompletedTask;
        }
    }
}
