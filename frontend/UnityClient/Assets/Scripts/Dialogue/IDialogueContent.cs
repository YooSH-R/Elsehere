using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 화면이 그리는 내용. 대본으로 진행하는 대화(<see cref="DialogueConversation"/>)와 서버 AI와 진행하는 대화
    /// (<see cref="ServerConversation"/>)가 같은 화면을 쓰도록 둔다.
    /// </summary>
    public interface IDialogueContent
    {
        string NpcName { get; }
        IReadOnlyList<DialogueLine> Lines { get; }

        /// <summary>가장 최근 NPC 대사. [뜻 보기]·[표현 저장]이 쓴다.</summary>
        DialogueLine LastNpcLine { get; }

        bool IsGuided { get; }
        bool IsGuidedCompleted { get; }
        string SuggestedEnglish { get; }
        string SuggestedKorean { get; }

        /// <summary>[예시 문장]으로 입력창에 넣어 줄 문장. 없으면 null이며 버튼을 숨긴다.</summary>
        string ExampleSentence { get; }

        /// <summary>서버 응답을 기다리는 중인지. 그동안 보내기·말하기를 막는다.</summary>
        bool IsBusy { get; }

        /// <summary>기다림·오류·안내 문구. 없으면 null이다.</summary>
        string StatusMessage { get; }

        /// <summary>오류나 대화 종료 뒤 사용자가 할 수 있는 일. 상태 문구 아래에 보이며, 없으면 null이다.</summary>
        string RecoveryGuide { get; }

        /// <summary>[다시 시도]를 보여 줄지. 일시적인 장애로 대화 시작이나 발화가 실패했을 때만 쓴다.</summary>
        bool CanRetry { get; }

        /// <summary>NPC 대사 아래에 보여 줄 추천 단어. 없으면 null이다.</summary>
        IReadOnlyList<string> SuggestWords { get; }

        bool CanSend { get; }

        /// <summary>[뜻 보기]를 쓸 수 있는지. 서버 대화는 처음 뜻을 볼 때 서버에서 받아 온다.</summary>
        bool HasMeaning { get; }

        /// <summary>가장 최근 NPC 대사의 단어 풀이. 뜻을 받기 전이거나 풀이가 없으면 null이다.</summary>
        IReadOnlyList<GlossaryData> LastNpcGlossary { get; }

        /// <summary>[힌트]를 쓸 수 있는지. 서버 대화만 쓴다.</summary>
        bool HasHint { get; }

        /// <summary>가장 최근 NPC 대사에 대답할 추천 문장. [힌트]를 누르기 전에는 null이다.</summary>
        string HintText { get; }

        /// <summary>[누르고 말하기]·[다시 듣기]를 보여 줄지. 서버 대화만 음성을 쓴다.</summary>
        bool UsesVoice { get; }

        /// <summary>NPC 호감도와 무례 경고 칸. 서버 대화만 쓰며, 대본 대화는 null이다.</summary>
        ConversationStanding Standing { get; }
    }
}
