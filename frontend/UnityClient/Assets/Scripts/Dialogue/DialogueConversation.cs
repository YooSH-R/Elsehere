using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 한 번의 진행. 사용자가 문장을 보내면 대본에 따라 NPC 대사를 덧붙인다.
    /// 서버·AI 연동 전에는 대본의 정해진 턴과 키워드 규칙으로 응답한다.
    /// 정해진 턴(튜토리얼)은 그 턴의 대사와 같게 말해야 넘어가고, 다른 문장은 대화에 남기지 않고 다시 말하게 한다.
    /// </summary>
    public sealed class DialogueConversation : IDialogueContent
    {
        /// <summary>정해진 대사와 다른 문장을 보냈을 때 입력칸 위에 보이는 안내</summary>
        public const string GuidedLineMissNotice =
            "튜토리얼 대화는 정해진 대사로 연습해요. 이 문장과 같게 입력해 보세요. [예시 문장]을 누르면 입력칸에 넣어 줘요.";

        private readonly DialogueScript script;
        private readonly List<DialogueLine> lines = new List<DialogueLine>();
        private readonly List<string> userUtterances = new List<string>();
        private int guidedTurnIndex;
        private string statusMessage;

        public DialogueConversation(string npcCode, DialogueScript script)
        {
            NpcCode = npcCode;
            this.script = script;

            if (script.OpeningLine != null)
            {
                AddNpcLine(script.OpeningLine);
            }
        }

        /// <summary>ERD npcs.code</summary>
        public string NpcCode { get; }

        public string NpcName => script.NpcName;
        public IReadOnlyList<DialogueLine> Lines => lines;

        /// <summary>튜토리얼처럼 읽을 대사를 따라가는 대화인지</summary>
        public bool IsGuided => script.GuidedTurns.Count > 0;

        public bool IsGuidedCompleted => IsGuided && guidedTurnIndex >= script.GuidedTurns.Count;

        /// <summary>정해진 흐름에서 이번에 읽을 대사. 자유 대화이거나 흐름을 마쳤으면 null이다.</summary>
        public string SuggestedEnglish => CurrentGuidedTurn?.SuggestedEnglish;

        public string SuggestedKorean => CurrentGuidedTurn?.SuggestedKorean;

        /// <summary>[예시 문장]으로 입력창에 넣어 줄 문장. 정해진 흐름에서는 이번에 읽을 대사다.</summary>
        public string ExampleSentence => IsGuided ? SuggestedEnglish : script.ExampleSentence;

        /// <summary>가장 최근 NPC 대사. [뜻 보기]·[표현 저장]이 이 대사를 쓴다.</summary>
        public DialogueLine LastNpcLine { get; private set; }

        // 대본 대화는 바로 답하고 음성을 쓰지 않는다. 서버를 부르지 않으므로 실패도 없다.
        public bool IsBusy => false;

        /// <summary>정해진 대사와 다른 문장을 보냈을 때의 안내. 대사를 맞게 보내면 지운다.</summary>
        public string StatusMessage => statusMessage;
        public string RecoveryGuide => null;
        public bool CanRetry => false;
        public IReadOnlyList<string> SuggestWords => null;
        public bool CanSend => !IsGuidedCompleted;
        public bool HasMeaning => true;
        public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
        public bool HasHint => false;
        public string HintText => null;
        public bool UsesVoice => false;
        public ConversationStanding Standing => null;
        public bool CanTalk => false;
        public bool CanReplaySpeech => false;

        private GuidedTurn? CurrentGuidedTurn =>
            IsGuided && !IsGuidedCompleted
                ? script.GuidedTurns[guidedTurnIndex]
                : (GuidedTurn?)null;

        /// <returns>
        /// 문장을 보내 NPC가 답했으면 true. 빈 문장이거나, 정해진 흐름을 마쳤거나, 정해진 대사와 다르면 false.
        /// </returns>
        public bool Send(string text)
        {
            string utterance = text?.Trim();
            if (string.IsNullOrEmpty(utterance) || IsGuidedCompleted)
            {
                return false;
            }

            if (IsGuided && !GuidedLineMatcher.IsSameLine(utterance, SuggestedEnglish))
            {
                statusMessage = GuidedLineMissNotice;
                return false;
            }

            statusMessage = null;
            lines.Add(DialogueLine.FromUser(utterance));
            userUtterances.Add(utterance);
            AddNpcLine(FindReply(utterance));
            return true;
        }

        public DialogueRecord ToRecord()
        {
            return new DialogueRecord(
                NpcCode,
                NpcName,
                new List<DialogueLine>(lines),
                new List<string>(userUtterances),
                IsGuidedCompleted
            );
        }

        private DialogueLine FindReply(string utterance)
        {
            if (IsGuided)
            {
                // 정해진 흐름은 그 턴의 대사와 같게 말했을 때만 여기까지 온다. 발화 평가는 피드백에서 한다.
                GuidedTurn turn = script.GuidedTurns[guidedTurnIndex];
                guidedTurnIndex++;
                return turn.Reply;
            }

            foreach (KeywordReply rule in script.KeywordReplies)
            {
                if (rule.Matches(utterance))
                {
                    return rule.Reply;
                }
            }

            return script.DefaultReply;
        }

        private void AddNpcLine(DialogueLine line)
        {
            lines.Add(line);
            LastNpcLine = line;
        }
    }
}
