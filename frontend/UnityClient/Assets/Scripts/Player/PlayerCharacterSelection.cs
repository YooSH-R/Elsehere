using UnityEngine;

namespace A305.Player
{
    /// <summary>서버가 확인한 캐릭터 선택을 공간 씬 사이에 전달한다. 영구 저장은 계정 API가 맡는다.</summary>
    public static class PlayerCharacterSelection
    {
        public static string SelectedId { get; private set; }

        public static void Select(string characterId)
        {
            SelectedId = characterId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            SelectedId = null;
        }
    }
}
