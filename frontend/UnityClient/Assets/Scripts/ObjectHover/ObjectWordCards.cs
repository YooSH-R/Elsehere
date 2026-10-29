using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;

namespace A305.ObjectHover
{
    /// <summary>
    /// 장소의 단어 카드와 사용자별 저장·숨김 상태. 장소에 들어올 때 WORLD-02로 한 번 받고, 카드 버튼으로 바꾼 상태를
    /// 서버에 보낸 뒤 반영한다. 받지 못한 단어는 카드에 Unity 단어만 보이고 버튼은 쓰지 않는다.
    /// </summary>
    public sealed class ObjectWordCards
    {
        private const string HiddenNotice =
            "이제 마우스를 올려도 카드가 뜨지 않아요. 사물을 클릭하면 다시 볼 수 있어요.";

        private readonly IObjectWordApi api;
        private readonly Dictionary<string, WordCard> cards = new Dictionary<string, WordCard>();

        public ObjectWordCards(IObjectWordApi api)
        {
            this.api = api;
        }

        /// <summary>장소의 단어 카드를 받았는지</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>장소의 단어 카드를 받는다. 받지 못하면 카드 없이 두어 Unity 단어로만 보이게 한다.</summary>
        public async Task LoadAsync(string sceneCode)
        {
            SceneWords scene;
            try
            {
                scene = await api.GetSceneWordsAsync(sceneCode);
            }
            catch (ApiException)
            {
                return;
            }

            foreach (WordCardData data in scene.Words)
            {
                cards[data.WordKey] = new WordCard(data);
            }

            IsLoaded = true;
        }

        public bool TryGet(string wordKey, out WordCard card)
        {
            if (wordKey == null)
            {
                card = null;
                return false;
            }

            return cards.TryGetValue(wordKey, out card);
        }

        /// <summary>호버로 카드를 띄워도 되는지. 숨긴 단어만 false이며, 숨긴 단어도 클릭하면 카드를 보인다.</summary>
        public bool CanHover(string wordKey)
        {
            return !TryGet(wordKey, out WordCard card) || !card.HoverHidden;
        }

        /// <summary>[단어장에 저장](VOCAB-02 OBJECT). 실패하면 카드 안내에 이유를 남기고 다시 누를 수 있다.</summary>
        public async Task SaveAsync(string wordKey)
        {
            if (!TryGet(wordKey, out WordCard card) || card.Saved || card.IsBusy)
            {
                return;
            }

            card.SaveRequestId ??= Guid.NewGuid().ToString();
            await RunAsync(
                card,
                async () =>
                {
                    await api.SaveAsync(card.WordKey, card.SaveRequestId);
                    card.Saved = true;
                    card.SaveRequestId = null;
                }
            );
        }

        /// <summary>[알아요 · 다시 안 띄우기]와 [다시 띄우기](WORLD-03).</summary>
        public async Task SetHoverHiddenAsync(string wordKey, bool hoverHidden)
        {
            if (!TryGet(wordKey, out WordCard card) || card.IsBusy)
            {
                return;
            }

            await RunAsync(
                card,
                async () =>
                {
                    await api.SetHoverHiddenAsync(card.WordKey, hoverHidden);
                    card.HoverHidden = hoverHidden;
                    card.Message = hoverHidden ? HiddenNotice : null;
                }
            );
        }

        // 요청 중에는 카드 버튼을 막고, 실패하면 상태는 그대로 두고 서버가 준 이유를 카드에 남긴다.
        private static async Task RunAsync(WordCard card, Func<Task> request)
        {
            card.IsBusy = true;
            card.Message = null;
            try
            {
                await request();
            }
            catch (ApiException exception)
            {
                card.Message = exception.Message;
            }
            finally
            {
                card.IsBusy = false;
            }
        }
    }
}
