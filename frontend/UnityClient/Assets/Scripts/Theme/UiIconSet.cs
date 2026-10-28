using UnityEngine;

namespace A305.Theme
{
    /// <summary>글자 대신 버튼에 쓰는 그림 아이콘. 순서는 <see cref="UiIconSet"/>의 스프라이트 순서와 같다.</summary>
    public enum UiIcon
    {
        None,
        Map,
        Book,
        Star,
        Talk,
        Mic,
        Send,
        Meaning,
        Hint,
        Example,
        Replay,
        Save,
        Exit,
        Close,
        Check,
        Play,
        Skip,
        Palette,
        Previous,
        Next,
        Delete,
        Detail,
        Retry,
        Rest,
    }

    /// <summary>한 테마가 쓰는 아이콘 그림 묶음. 같은 그림을 테두리 유무만 달리해 테마끼리 나눠 쓴다.</summary>
    [CreateAssetMenu(fileName = "UiIconSet", menuName = "A305/Theme/Icon Set")]
    public sealed class UiIconSet : ScriptableObject
    {
        [SerializeField]
        [Tooltip("UiIcon 순서대로. None 자리는 비워 둔다.")]
        private Sprite[] sprites = new Sprite[0];

        public Sprite Get(UiIcon icon)
        {
            int index = (int)icon;
            return index > 0 && index < sprites.Length ? sprites[index] : null;
        }
    }
}
