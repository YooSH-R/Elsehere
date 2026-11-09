// AI 이용 상태 AI-01(docs/api/mvp-api.md)의 응답. 칸 이름은 ApiJson이 camelCase로 바꾼다.
namespace A305.Dialogue
{
    public sealed class AiStatusData
    {
        /// <summary>AI_AVAILABLE, AI_EXHAUSTED 등</summary>
        public string Status { get; set; }

        public AiCapabilities Capabilities { get; set; }

        /// <summary>더 시작할 수 있는 NPC 대화 수. 게스트는 남은 전체 횟수, 가입 사용자는 오늘 남은 횟수다.</summary>
        public int? RemainingConversations { get; set; }

        /// <summary>게스트인지. 게스트가 대화를 다 쓰면 가입을 권하고, 회원에게는 내일 다시 대화하라고 안내한다.</summary>
        public bool Guest { get; set; }
    }

    public sealed class AiCapabilities
    {
        public bool CanStartConversation { get; set; }
    }
}
