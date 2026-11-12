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

        // 누른 사람이 왜 시작하지 못했는지 알 수 있게 서버 오류 코드를 풀어 쓴다.
        private static string Reason(ApiException exception)
        {
            switch (exception.Code)
            {
                case "REVIEW_NOT_READY":
                    return "이 대화에는 다시 연습할 문장이 없어요. 고친 문장이나 추천 표현이 2개 이상 있는 대화만 연습할 수 있어요.";
                case "NOT_FOUND":
                    return "이 대화를 찾지 못했어요. 대화 노트를 다시 열어 주세요.";
                case ApiErrorCodes.NetworkError:
                    return "서버에 연결하지 못해 연습을 준비하지 못했어요. 잠시 뒤 다시 눌러 주세요.";
                default:
                    return string.IsNullOrWhiteSpace(exception.Message)
                        ? "연습을 준비하지 못했어요. 다시 시도해 주세요."
                        : exception.Message;
            }
        }

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
                Message = Reason(exception);
                return null;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
