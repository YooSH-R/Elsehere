using System.Collections.Generic;
using A305.Player;

namespace A305.Dialogue
{
    /// <summary>
    /// 대화 한 번의 진행. 사용자가 문장을 보내면 대본에 따라 NPC 대사를 덧붙인다.
    /// 서버·AI 연동 전에는 대본의 정해진 턴과 키워드 규칙으로 응답한다.
    /// 정해진 턴(튜토리얼)에서 글자로 보낸 문장은 그 턴의 대사와 같아야 넘어가고, 다른 문장은 대화에 남기지 않고 다시 입력하게 한다.
    /// 정해진 턴은 대사마다 쓸 만한 추천 단어를 보인다.
    /// 말로 한 대사는 무엇을 말했든 정해진 대사로 이어 가고, 다르게 들렸으면 그렇게 했다고 알린다.
    /// </summary>
    public sealed class DialogueConversation : IDialogueContent
    {
        /// <summary>정해진 대사와 다른 문장을 보냈을 때 입력칸 위에 보이는 안내</summary>
        public const string GuidedLineMissNotice =
            "튜토리얼은 정해진 흐름대로 진행돼요. 안내한 대사와 같게 입력하거나 [힌트]로 펼친 문장을 눌러 넣어 보세요.";

        /// <summary>말로 한 대사가 정해진 대사와 다르거나 알아듣지 못했어도 정해진 대사로 이어 갔다는 안내</summary>
        public const string GuidedVoiceNotice =
            "튜토리얼은 정해진 흐름대로 진행돼요. 말한 내용과 달라도 안내한 대사로 이어 갔어요.";

        private readonly DialogueScript script;
        private readonly bool hasRecorder;
        private readonly List<DialogueLine> lines = new List<DialogueLine>();
        private readonly List<string> userUtterances = new List<string>();
        private int guidedTurnIndex;
        private string statusMessage;
        private string hintText;
        private readonly string playerName;
        private readonly ConversationStanding tutorialStanding;

        /// <param name="hasRecorder">녹음기가 연결되어 있는지. 정해진 대사를 따라가는 대화면 [말하기]를 둔다.</param>
        public DialogueConversation(
            string npcCode,
            DialogueScript script,
            bool hasRecorder = false,
            DialogueLine recordedOpening = null
        )
        {
            NpcCode = npcCode;
            this.script = script;
            this.hasRecorder = hasRecorder;
            playerName = PlayerNickname.Name;
            if (IsGuided && script.NpcName == "Ben")
            {
                tutorialStanding = new ConversationStanding();
                UpdateTutorialStanding();
            }

            DialogueLine opening = IsGuided
                ? script.OpeningLine
                : recordedOpening ?? script.OpeningLine;
            if (opening != null)
            {
                AddNpcLine(opening);
            }
        }

        /// <summary>ERD npcs.code</summary>
        public string NpcCode { get; }

        public string UserTranslation => null;
        public string NpcName => script.NpcName;
        public IReadOnlyList<DialogueLine> Lines => lines;

        /// <summary>튜토리얼처럼 읽을 대사를 따라가는 대화인지</summary>
        public bool IsGuided => script.GuidedTurns.Count > 0;

        /// <summary>정해진 흐름에서 지금 몇 번째 대사인지(0부터). 튜토리얼이 대사마다 다른 도움 카드를 알린다.</summary>
        public int GuidedTurnIndex => guidedTurnIndex;

        /// <summary>
        /// 정해진 흐름에서 글자로 보낸 문장이 대사와 달라 다시 입력해야 하는지. 말로 한 대사는 달라도 넘어가므로 여기에 들지 않는다.
        /// </summary>
        public bool IsGuidedLineMissed => statusMessage == GuidedLineMissNotice;

        public bool IsGuidedCompleted => IsGuided && guidedTurnIndex >= script.GuidedTurns.Count;

        /// <summary>정해진 흐름에서 이번에 읽을 대사. 자유 대화이거나 흐름을 마쳤으면 null이다.</summary>
        public string SuggestedEnglish =>
            CurrentGuidedTurn?.SuggestedEnglish?.Replace("{name}", playerName ?? "a visitor");

        public string SuggestedKorean =>
            CurrentGuidedTurn?.SuggestedKorean?.Replace("{name}", playerName ?? "여행자");

        /// <summary>확인용 자유 대화의 [예시 문장]. 튜토리얼은 서비스와 같은 [힌트]를 쓴다.</summary>
        public string ExampleSentence => IsGuided ? null : script.ExampleSentence;

        /// <summary>가장 최근 NPC 대사. [뜻 보기]·[표현 저장]이 이 대사를 쓴다.</summary>
        public DialogueLine LastNpcLine { get; private set; }

        // 대본 대화는 바로 답한다. 서버를 부르지 않으므로 실패도 없다.
        public bool IsBusy => false;

        /// <summary>녹음 중 안내, 정해진 대사와 다른 문장을 보냈을 때의 안내, 말한 대사를 정해진 대사로 이어 갔다는 안내</summary>
        public string StatusMessage => statusMessage;
        public DialogueWait Waiting => DialogueWait.None;
        public string RecoveryGuide => null;
        public bool CanRetry => false;
        public IReadOnlyList<string> SuggestWords => CurrentGuidedTurn?.SuggestWords;
        public bool IsSuggestingWords => false;
        public bool CanSend => !IsGuidedCompleted;
        public bool HasMeaning => true;
        public IReadOnlyList<GlossaryData> LastNpcGlossary => null;
        public bool HasHint => IsGuided && !IsGuidedCompleted;
        public string HintText => hintText;

        public void RequestHint()
        {
            if (HasHint)
                hintText = SuggestedEnglish;
        }

        // 튜토리얼이라고 말하기를 빼지 않는다. 말한 녹음은 정해진 대사로 이어 가므로 음성 처리 동의 없이도 말해 볼 수 있다.
        public bool UsesVoice => IsGuided && hasRecorder;
        public ConversationStanding Standing => tutorialStanding;
        public bool CanTalk => UsesVoice;
        public bool CanReplaySpeech => LastNpcLine?.Speech != null;
        public bool SpeaksNpcLines => LastNpcLine?.Speech != null;
        public bool IsNpcSpeechReady => LastNpcLine?.Speech != null;

        public bool ShowRecordedReply(string text, DialogueLine reply)
        {
            if (IsGuided || !CanSend || reply?.Speech == null)
                return false;
            statusMessage = null;
            if (!string.IsNullOrWhiteSpace(text))
                lines.Add(DialogueLine.FromUser(text));
            AddNpcLine(reply);
            return true;
        }

        public void ShowMessage(string message)
        {
            statusMessage = message;
        }

        /// <summary>
        /// 말로 한 대사로 이번 턴을 넘긴다. 무엇을 말했든 정해진 대사를 보낸 것으로 하며, 알아들은 문장이 대사와 다르거나
        /// 알아듣지 못했으면(<paramref name="transcript"/>가 비어 있음) 그렇게 했다고 알린다.
        /// </summary>
        /// <returns>이번 턴을 넘겼으면 true. 정해진 흐름이 아니거나 이미 마쳤으면 false.</returns>
        public bool SendSpoken(string transcript)
        {
            string line = SuggestedEnglish;
            if (line == null)
            {
                return false;
            }

            bool isSameLine =
                !string.IsNullOrWhiteSpace(transcript)
                && GuidedLineMatcher.IsSameLine(transcript.Trim(), line);
            Send(line);
            statusMessage = isSameLine ? null : GuidedVoiceNotice;
            return true;
        }

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
            hintText = null;
            lines.Add(DialogueLine.FromUser(utterance));
            userUtterances.Add(utterance);
            AddNpcLine(FindReply(utterance));
            UpdateTutorialStanding();
            return true;
        }

        private void UpdateTutorialStanding()
        {
            // 길 안내 완료 API가 저장하는 기본 호감도 +4를 두 번의 긍정적인 응답(인사, 길 안내)으로 보여 준다.
            // 실제 저장은 TutorialFinishRecorder가 완료 API를 한 번씩 재시도하는 경로를 쓴다.
            tutorialStanding?.Apply(
                new ConversationState
                {
                    Affinity = guidedTurnIndex * 2,
                    RudeCount = 0,
                    RudeLimit = 3,
                }
            );
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
