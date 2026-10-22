using UnityEngine;

namespace A305.Learning
{
    /// <summary>
    /// 공간 씬이 바뀌어도 단어장이 남도록 씬 밖에 둔다. 서버 연동 전에는 Play 동안만 남는다.
    /// </summary>
    public static class LearningRecord
    {
        public static VocabularyBook Vocabulary { get; private set; } = new VocabularyBook();

        // Domain Reload를 끈 Editor에서도 Play를 시작할 때마다 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Vocabulary = new VocabularyBook();
        }
    }
}
