// AI 이용 상태 AI-01(docs/api/mvp-api.md)의 응답. 칸 이름은 ApiJson이 camelCase로 바꾼다.
namespace A305.Dialogue
{
    public sealed class AiStatusData
    {
        /// <summary>AI_AVAILABLE, AI_EXHAUSTED 등</summary>
        public string Status { get; set; }

        public AiCapabilities Capabilities { get; set; }

        /// <summary>게스트가 더 시작할 수 있는 NPC 대화 수. 가입 사용자는 횟수 제한이 없어 null이다.</summary>
        public int? RemainingConversations { get; set; }
    }

    public sealed class AiCapabilities
    {
        public bool CanStartConversation { get; set; }
    }
}
