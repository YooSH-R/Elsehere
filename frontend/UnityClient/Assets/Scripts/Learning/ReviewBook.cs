using System;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Learning
{
    /// <summary>
    /// 대화 노트의 [다시 연습]이 쓰는 복습 카드. 고른 대화의 카드를 서버에서 찾거나 만든다(같은 대화는 언제나 같은 카드).
    /// 실패하면 <see cref="Message"/>에 서버가 준 이유를 남긴다.
    /// </summary>
    public sealed class ReviewBook
    {
        private readonly IReviewApi api;

        public ReviewBook(IReviewApi api) => this.api = api;

        public bool IsBusy { get; private set; }

        public string Message { get; private set; }

        /// <returns>고른 대화의 복습 카드. 만들지 못하면 null.</returns>
        public async Task<ReviewCard> SaveAsync(long conversationId)
        {
            if (IsBusy)
                return null;
            IsBusy = true;
            Message = null;
            try
            {
                ReviewCard card = await api.SaveAsync(conversationId);
                if (card == null || card.Id <= 0)
                    throw new ApiException(
                        ApiErrorCodes.UnexpectedResponse,
                        0,
                        "연습을 준비하지 못했어요. 다시 시도해 주세요."
                    );
                return card;
            }
            catch (ApiException exception)
            {
                Message = exception.Message;
                return null;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
