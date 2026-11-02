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

        // 마을 지도의 장소와 가로수. 카페는 쉬어 가기 아이콘(커피잔)을 함께 쓴다.
        Home,
        Hotel,
        Store,
        Tree,

        // 오른쪽 위 메뉴(≡). 아이콘 묶음은 이 순서대로 저장되므로 새 아이콘은 끝에 더한다.
        Menu,

        // 오른쪽 위 휴대폰 버튼
        Phone,
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
