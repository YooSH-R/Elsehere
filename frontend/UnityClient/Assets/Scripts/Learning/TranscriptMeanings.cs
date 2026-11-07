using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A305.Api;
using A305.Dialogue;

namespace A305.Learning
{
    /// <summary>턴별 뜻 요청을 합치고 이미 읽은 결과를 다시 쓴다.</summary>
    public sealed class TranscriptMeanings
    {
        private readonly Func<long, Task<MeaningData>> request;
        private readonly Dictionary<long, Task<MeaningData>> requests =
            new Dictionary<long, Task<MeaningData>>();

        public TranscriptMeanings(Func<long, Task<MeaningData>> request)
        {
            this.request = request;
        }

        public async Task<DialogueLine> ResolveAsync(DialogueLine line, bool retry = false)
        {
            if (line == null || !line.IsNpc || line.TurnId <= 0 || line.Korean != null)
                return line;

            if (
                requests.TryGetValue(line.TurnId, out Task<MeaningData> saved)
                && retry
                && (saved.IsFaulted || saved.IsCanceled)
            )
                requests.Remove(line.TurnId);

            if (!requests.TryGetValue(line.TurnId, out Task<MeaningData> loading))
            {
                loading = RequestAsync(line.TurnId);
                requests.Add(line.TurnId, loading);
            }
            MeaningData meaning = await loading;
            return DialogueLine.FromNpc(
                line.English,
                meaning.MeaningKo,
                GlossaryWord.FromMeaning(line.TurnId, meaning.Glossary),
                line.Speech,
                line.TurnId
            );
        }

        private async Task<MeaningData> RequestAsync(long turnId)
        {
            MeaningData meaning = await request(turnId);
            if (
                meaning == null
                || meaning.TurnId != turnId
                || string.IsNullOrWhiteSpace(meaning.MeaningKo)
            )
                throw new ApiException("INVALID_MEANING", 0, "대사의 뜻을 읽지 못했어요.");
            return meaning;
        }
    }
}
