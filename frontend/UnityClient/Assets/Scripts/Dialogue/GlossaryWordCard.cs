using System;
using System.Collections.Generic;
using System.Linq;

namespace A305.Dialogue
{
    /// <summary>
    /// 대사에서 누른 자리의 호버 카드 상태. 그 자리를 덮는 풀이(누른 단어와 그 단어가 든 숙어)를 짧은 말부터 보이고,
    /// 풀이마다 [단어장에 저장]과 저장 안내(저장하는 중·결과·실패 이유)를 정한다.
    /// 이번 대화에서 저장한 단어는 다시 눌러도 저장했다고 보인다.
    /// </summary>
    public sealed class GlossaryWordCard
    {
        private const string SavingNote = "저장하는 중이에요…";

        // 이번 대화에서 단어장에 넣은 단어와 그때 받은 결과 문구
        private readonly Dictionary<GlossaryWord, string> savedNotes =
            new Dictionary<GlossaryWord, string>();

        // 결과를 기다리는 단어. 카드를 닫았다 열어도 같은 단어를 또 보내지 않게 막는다.
        private readonly HashSet<GlossaryWord> savingWords = new HashSet<GlossaryWord>();

        // 저장하지 못한 이유. 다시 누를 수 있게 버튼과 함께 보인다.
        private readonly Dictionary<GlossaryWord, string> failureNotes =
            new Dictionary<GlossaryWord, string>();

        /// <summary>카드에 보이는 풀이. 누른 단어가 먼저이고 그 단어가 든 숙어가 뒤에 온다. 카드를 닫았으면 비어 있다.</summary>
        public IReadOnlyList<GlossaryWord> Words { get; private set; } =
            Array.Empty<GlossaryWord>();

        /// <summary>누른 자리의 첫 풀이. 대사에서 이 말을 칠하고 카드를 그 옆에 둔다. 카드를 닫았으면 null이다.</summary>
        public GlossaryWord Word => Words.Count > 0 ? Words[0] : null;

        public bool IsVisible => Words.Count > 0;

        /// <summary>단어장 서버가 연결되어 있는지. 연결되지 않았으면 [단어장에 저장]을 숨긴다.</summary>
        public bool IsSaveAvailable { get; set; } = true;

        /// <summary>그 풀이의 뜻 아래에 보일 저장 안내. 안내할 것이 없으면 null이다.</summary>
        public string NoteFor(GlossaryWord word)
        {
            if (!Words.Contains(word))
            {
                return null;
            }

            if (savedNotes.TryGetValue(word, out string savedNote))
            {
                return savedNote;
            }

            if (!IsSaveAvailable)
            {
                return null;
            }

            if (savingWords.Contains(word))
            {
                return SavingNote;
            }

            return failureNotes.TryGetValue(word, out string failure) ? failure : null;
        }

        public bool IsSaveVisible(GlossaryWord word) =>
            Words.Contains(word) && IsSaveAvailable && !savedNotes.ContainsKey(word);

        public bool IsSaveInteractable(GlossaryWord word) =>
            IsSaveVisible(word) && !savingWords.Contains(word);

        /// <summary>누른 자리의 풀이를 보인다. 첫 풀이가 지금과 같은 자리를 다시 누르면 카드를 닫는다.</summary>
        public void Select(IReadOnlyList<GlossaryWord> words)
        {
            if (words.Count == 0)
            {
                return;
            }

            if (words[0] == Word)
            {
                Close();
                return;
            }

            Words = words;
            failureNotes.Clear();
        }

        public void Close()
        {
            Words = Array.Empty<GlossaryWord>();
            failureNotes.Clear();
        }

        /// <summary>그 풀이의 [단어장에 저장]을 눌렀다. 결과를 받을 때까지 그 버튼을 막는다.</summary>
        public void StartSaving(GlossaryWord word)
        {
            if (!IsSaveInteractable(word))
            {
                return;
            }

            savingWords.Add(word);
            failureNotes.Remove(word);
        }

        /// <param name="saved">단어장에 들어갔는지. 이미 있던 단어도 true다.</param>
        /// <param name="message">저장 결과나 실패 이유</param>
        public void FinishSaving(GlossaryWord word, bool saved, string message)
        {
            savingWords.Remove(word);
            if (saved)
            {
                savedNotes[word] = message;
            }
            // 기다리는 사이 다른 자리를 골랐으면 실패 이유는 지금 카드에 남기지 않는다.
            else if (Words.Contains(word))
            {
                failureNotes[word] = message;
            }
        }
    }
}
