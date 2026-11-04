using A305.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 오른쪽 위 휴대폰 메뉴. 휴대폰 버튼은 폰을 펼치고 접으며, 폰 안의 버튼(앱·내비게이션 바)을 누르거나 바깥을 누르면 접는다.
    /// 펼친 동안에는 같은 자리에 강조한 휴대폰 버튼을 보이고, 퀘스트 카드·시계 칩보다 위에 그린다. 앱의 기능은 각 화면 코드가 맡는다.
    /// </summary>
    public sealed class HudMenuView : MonoBehaviour
    {
        [SerializeField]
        private Button menuButton;

        [SerializeField]
        [Tooltip("펼친 동안 메뉴 버튼 자리에 보이는 강조 버튼. 누르면 접는다.")]
        private Button openMenuButton;

        [SerializeField]
        [Tooltip("펼친 동안 화면 전체를 덮는 투명 버튼. 누르면 접는다.")]
        private Button backdrop;

        [SerializeField]
        private GameObject items;

        private readonly HudMenu menu = new HudMenu();
        private Canvas canvas;

        public bool IsOpen => menu.IsOpen;

        /// <summary>접힌 메뉴 버튼(≡). 튜토리얼 코치 마크가 밝힌다.</summary>
        public RectTransform MenuButtonRect => (RectTransform)menuButton.transform;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            menu.Changed += Show;
            menuButton.onClick.AddListener(menu.Toggle);
            openMenuButton.onClick.AddListener(menu.Toggle);
            backdrop.onClick.AddListener(menu.Close);
            // 장소마다 켜지는 항목(집의 테마 등)도 고르면 접히도록 꺼진 버튼까지 연결한다.
            foreach (Button item in items.GetComponentsInChildren<Button>(true))
                item.onClick.AddListener(menu.Close);
            Show(menu.IsOpen);
        }

        private void OnDestroy()
        {
            menu.Changed -= Show;
        }

        private void Show(bool isOpen)
        {
            items.SetActive(isOpen);
            backdrop.gameObject.SetActive(isOpen);
            menuButton.gameObject.SetActive(!isOpen);
            openMenuButton.gameObject.SetActive(isOpen);
            // 펼친 동안에는 퀘스트 카드·미니맵 등 다른 캔버스와 먼저 연 창보다 위에 그린다.
            if (isOpen)
                UiLayer.Open(canvas, this);
            else
                UiLayer.Close(canvas, this);
        }
    }
}
