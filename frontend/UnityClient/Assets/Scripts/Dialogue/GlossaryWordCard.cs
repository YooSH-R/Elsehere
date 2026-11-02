using System.Collections.Generic;

namespace A305.Dialogue
{
    /// <summary>
    /// 대사에서 누른 풀이 단어의 호버 카드 상태. 단어·뜻과 [단어장에 저장], 저장 안내(저장하는 중·결과·실패 이유)를 정한다.
    /// 이번 대화에서 저장한 단어는 다시 눌러도 저장했다고 보인다.
    /// </summary>
    public sealed class GlossaryWordCard
    {
        private const string SavingNote = "저장하는 중이에요…";

        // 이번 대화에서 단어장에 넣은 단어와 그때 받은 결과 문구
        private readonly Dictionary<GlossaryWord, string> savedNotes =
            new Dictionary<GlossaryWord, string>();

        private bool isSaving;

        // 저장하지 못한 이유. 다시 누를 수 있게 버튼과 함께 보인다.
        private string failureNote;

        /// <summary>지금 고른 단어. 카드를 닫았으면 null이다.</summary>
        public GlossaryWord Word { get; private set; }

        public bool IsVisible => Word != null;

        /// <summary>단어장 서버가 연결되어 있는지. 연결되지 않았으면 [단어장에 저장]을 숨긴다.</summary>
        public bool IsSaveAvailable { get; set; } = true;

        /// <summary>뜻 아래에 보일 저장 안내. 안내할 것이 없으면 null이다.</summary>
        public string Note
        {
            get
            {
                if (Word == null)
                {
                    return null;
                }

                if (savedNotes.TryGetValue(Word, out string savedNote))
                {
                    return savedNote;
                }

                if (!IsSaveAvailable)
                {
                    return null;
                }

                return isSaving ? SavingNote : failureNote;
            }
        }

        public bool IsSaveVisible =>
            Word != null && IsSaveAvailable && !savedNotes.ContainsKey(Word);

        public bool IsSaveInteractable => IsSaveVisible && !isSaving;

        /// <summary>단어를 고른다. 이미 고른 단어를 다시 누르면 카드를 닫는다.</summary>
        public void Select(GlossaryWord word)
        {
            if (word == Word)
            {
                Close();
                return;
            }

            Word = word;
            isSaving = false;
            failureNote = null;
        }

        public void Close()
        {
            Word = null;
            isSaving = false;
            failureNote = null;
        }

        /// <summary>[단어장에 저장]을 눌렀다. 결과를 받을 때까지 버튼을 막는다.</summary>
        public void StartSaving()
        {
            if (!IsSaveInteractable)
            {
                return;
            }

            isSaving = true;
            failureNote = null;
        }

        /// <param name="saved">단어장에 들어갔는지. 이미 있던 단어도 true다.</param>
        /// <param name="message">저장 결과나 실패 이유</param>
        public void FinishSaving(GlossaryWord word, bool saved, string message)
        {
            if (saved)
            {
                savedNotes[word] = message;
            }

            // 기다리는 사이 다른 단어를 골랐으면 지금 보이는 카드는 그대로 둔다.
            if (word != Word)
            {
                return;
            }

            isSaving = false;
            failureNote = saved ? null : message;
        }
    }
}
