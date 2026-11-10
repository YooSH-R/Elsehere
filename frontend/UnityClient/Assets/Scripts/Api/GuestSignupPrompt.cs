using System;

namespace A305.Api
{
    /// <summary>
    /// 게스트에게 가입을 권할 때. 게스트가 첫 호텔 퀘스트를 끝낸 팡파레의 [다음]을 누르면(<see cref="RequestAfterQuest"/>)
    /// <see cref="Request"/>로 알리고, 장소마다 있는 가입 안내 창이 받아 연다.
    /// 서버에서 게스트 대화 횟수 제한을 켠 경우(GUEST_CONVERSATION_LIMIT)에는 대화를 다 쓴 마지막 대화의 피드백을 닫을 때도 알리고,
    /// 막힌 [말 걸기]는 가입 안내 창을 직접 연다.
    /// </summary>
    public static class GuestSignupPrompt
    {
        /// <summary>첫 호텔 퀘스트(서버 Quest.CHECK_IN).</summary>
        public const string FirstQuestId = "CHECK_IN";

        /// <summary>가입을 권할 때가 됐다. 가입 안내 창이 받는다.</summary>
        public static event Action Requested;

        public static void Request() => Requested?.Invoke();

        /// <summary>끝낸 퀘스트가 첫 호텔 퀘스트이고 게스트(<see cref="PlayerAccount.IsGuest"/>)이면 가입을 권한다.</summary>
        public static void RequestAfterQuest(string questId)
        {
            if (PlayerAccount.IsGuest && questId == FirstQuestId)
            {
                Request();
            }
        }
    }
}
