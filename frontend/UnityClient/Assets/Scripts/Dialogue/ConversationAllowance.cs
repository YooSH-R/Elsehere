using System.Threading.Tasks;
using A305.Api;

namespace A305.Dialogue
{
    /// <summary>
    /// 새 NPC 대화를 시작할 수 있는지와 게스트의 남은 대화 수(AI-01). 말 걸기 버튼과 행인 말걸기가 이 값을 보고 켜고 끈다.
    /// 가입 사용자는 대화 횟수 제한이 없다.
    /// </summary>
    /// <remarks>
    /// 서버 상태를 받기 전이나 받지 못했을 때는 막지 않는다. 최종 확인은 대화 시작(CONV-01)에서 서버가 한다.
    /// </remarks>
    public sealed class ConversationAllowance
    {
        public bool CanStartConversation { get; private set; } = true;

        /// <summary>게스트가 더 시작할 수 있는 대화 수. 가입 사용자이거나 아직 모르면 null이다.</summary>
        public int? RemainingConversations { get; private set; }

        /// <summary>게스트가 대화를 모두 썼는지. 가입 사용자이거나 아직 모르면 false다.</summary>
        public bool IsGuestOutOfConversations => RemainingConversations == 0;

        public void Apply(AiStatusData status)
        {
            CanStartConversation = status.Capabilities?.CanStartConversation ?? true;
            RemainingConversations = status.RemainingConversations;
        }

        /// <summary>서버에서 상태를 다시 받는다. 받지 못하면 지금 상태를 그대로 둔다.</summary>
        public async Task RefreshAsync(IAiStatusApi api)
        {
            try
            {
                Apply(await api.GetStatusAsync());
            }
            catch (ApiException)
            {
                // 다음 접속이나 대화 종료 때 다시 받는다.
            }
        }
    }
}
