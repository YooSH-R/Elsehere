using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.ObjectHover
{
    /// <summary>
    /// 사물의 영어 표현·뜻·예문을 보여 주는 툴팁 화면. 화면 오버레이 캔버스 루트에 붙이고,
    /// 패널은 캔버스 왼쪽 아래를 기준으로 자리를 잡는다. 포인터 가까이에 두되 가리킨 사물·캐릭터와
    /// 다른 화면 UI를 덮지 않는 자리를 고른다. 호버로 보일 때는 클릭을 막지 않고,
    /// 클릭으로 고정했을 때만 카드 안 버튼을 누를 수 있게 클릭을 받는다.
    /// </summary>
    public class WordTooltipView : MonoBehaviour
    {
        private const string ExampleMeaningColor = "#B8C0CC";

        // 화면을 이 비율 이상 덮는 UI는 배경막으로 보고 피하지 않는다.
        private const float BackdropScreenShare = .9f;

        [SerializeField]
        private RectTransform canvasRect;

        [SerializeField]
        [Tooltip("기준점(Anchor)과 피벗을 왼쪽 아래(0, 0)로 둔 패널")]
        private RectTransform panel;

        [SerializeField]
        private CanvasGroup panelGroup;

        [SerializeField]
        private TMP_Text wordLabel;

        [SerializeField]
        private TMP_Text meaningLabel;

        [SerializeField]
        private TMP_Text exampleLabel;

        [SerializeField]
        [Tooltip("호버 중에만 보이는 '클릭하면 카드가 고정돼요' 안내")]
        private TMP_Text pinHintLabel;

        [SerializeField]
        [Tooltip("고정한 카드의 저장 실패 이유나 숨긴 카드를 다시 보는 방법")]
        private TMP_Text statusLabel;

        [SerializeField]
        [Tooltip(
            "고정한 카드에서 서버 단어 카드가 있을 때만 보이는 [단어장에 저장]·[발음 듣기] 줄"
        )]
        private GameObject wordActionRow;

        [SerializeField]
        private Button saveButton;

        [SerializeField]
        private TMP_Text saveLabel;

        [SerializeField]
        private Button pronunciationButton;

        [SerializeField]
        [Tooltip("고정한 카드에만 보이는 [알아요 · 다시 안 띄우기]·[닫기] 줄")]
        private GameObject actionRow;

        [SerializeField]
        private Button hideButton;

        [SerializeField]
        private TMP_Text hideLabel;

        [SerializeField]
        private Button closeButton;

        // 마지막으로 보인 카드의 자리 기준. 고정한 카드의 자리를 다시 정할 때 쓴다.
        private CardAnchor anchor;
        private ObjectHoverSettings settings;

        // 씬의 화면 캔버스. 카드가 덮지 않을 UI를 찾는다.
        private Canvas[] canvases;
        private readonly List<Rect> avoidAreas = new List<Rect>();
        private readonly Vector3[] corners = new Vector3[4];

        public bool IsVisible { get; private set; }

        public string WordText => wordLabel.text;

        public string MeaningText => meaningLabel.text;

        public bool IsExampleVisible => exampleLabel.gameObject.activeSelf;

        public string ExampleText => exampleLabel.text;

        /// <summary>클릭으로 고정한 카드인지. 고정한 카드만 [닫기] 같은 버튼을 보이고 누를 수 있다.</summary>
        public bool IsPinned { get; private set; }

        /// <summary>고정한 카드에서 [저장]을 누를 수 있는지. 서버 단어 카드가 없거나 이미 저장한 단어면 false.</summary>
        public bool CanSaveWord =>
            IsPinned && saveButton.gameObject.activeInHierarchy && saveButton.interactable;

        /// <summary>고정한 카드의 [저장]. 튜토리얼 코치 마크가 밝힌다.</summary>
        public RectTransform SaveButtonRect => (RectTransform)saveButton.transform;

        /// <summary>고정한 카드의 [닫기]를 눌렀다.</summary>
        public event Action CloseRequested;

        /// <summary>고정한 카드의 [단어장에 저장]을 눌렀다.</summary>
        public event Action SaveRequested;

        /// <summary>고정한 카드의 [발음 듣기]를 눌렀다.</summary>
        public event Action PronunciationRequested;

        /// <summary>고정한 카드의 [알아요 · 다시 안 띄우기]나 [다시 띄우기]를 눌렀다.</summary>
        public event Action HoverHiddenToggleRequested;

        /// <summary>호버로 잠깐 보이는 카드. 클릭을 막지 않고 "클릭하면 고정" 안내를 보인다.</summary>
        /// <param name="anchor">마우스 포인터와 가리킨 사물·캐릭터의 화면 영역</param>
        public void Show(ObjectWord word, CardAnchor anchor, ObjectHoverSettings settings)
        {
            Present(word, anchor, settings, isPinned: false, WordCardButtons.None);
        }

        /// <summary>클릭으로 고정한 카드. 카드 안 버튼을 누를 수 있게 클릭을 받고 버튼 줄을 보인다.</summary>
        /// <param name="anchor">클릭한 화면 좌표와 가리킨 사물·캐릭터의 화면 영역</param>
        /// <param name="buttons">서버 단어 카드가 없으면 <see cref="WordCardButtons.None"/>으로 [닫기]만 보인다.</param>
        public void ShowPinned(
            ObjectWord word,
            CardAnchor anchor,
            ObjectHoverSettings settings,
            WordCardButtons buttons
        )
        {
            Present(word, anchor, settings, isPinned: true, buttons);
        }

        /// <summary>보이는 카드의 자리를 다시 정한다. 고정한 카드가 나중에 열린 창에 가려지지 않도록 매 프레임 부른다.</summary>
        public void UpdatePlacement()
        {
            if (IsVisible)
            {
                Place();
            }
        }

        /// <summary>고정한 카드의 안내 문구만 바꾼다. 발음을 받지 못했을 때처럼 카드 상태와 상관없는 안내에 쓴다.</summary>
        public void ShowStatus(string message)
        {
            statusLabel.text = message;
            statusLabel.gameObject.SetActive(IsPinned && !string.IsNullOrEmpty(message));
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        public void Hide()
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
            IsVisible = false;
            IsPinned = false;
        }

        private void Present(
            ObjectWord word,
            CardAnchor anchor,
            ObjectHoverSettings settings,
            bool isPinned,
            WordCardButtons buttons
        )
        {
            pinHintLabel.gameObject.SetActive(!isPinned);
            wordLabel.text = word.Text;
            meaningLabel.text = word.MeaningKo;
            exampleLabel.text = string.IsNullOrEmpty(word.ExampleKo)
                ? word.Example
                : $"{word.Example}\n<color={ExampleMeaningColor}>{word.ExampleKo}</color>";
            exampleLabel.gameObject.SetActive(!string.IsNullOrEmpty(word.Example));
            ShowButtons(isPinned, buttons);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);

            this.anchor = anchor;
            this.settings = settings;
            Place();

            panelGroup.alpha = 1f;
            panelGroup.blocksRaycasts = isPinned;
            panelGroup.interactable = isPinned;
            IsVisible = true;
            IsPinned = isPinned;
        }

        // 포인터 가까이에서 가리킨 사물·캐릭터와 다른 화면을 덮지 않는 자리에 카드를 둔다.
        private void Place()
        {
            avoidAreas.Clear();
            if (anchor.PlayerArea is Rect playerArea)
            {
                avoidAreas.Add(ToCanvas(playerArea));
            }

            AddOtherScreenAreas();
            panel.anchoredPosition = TooltipPlacement.PlaceAround(
                ToCanvas(anchor.Pointer),
                ToCanvas(anchor.PointedArea),
                panel.rect.size,
                canvasRect.rect.size,
                settings.PointerGap,
                settings.ScreenMargin,
                avoidAreas
            );
        }

        // 카드 밖에서 지금 그려지는 화면 UI(HUD·퀘스트 안내·수첩·미니맵·튜토리얼 가이드 등)의 영역을 더한다.
        // 메뉴를 열 때의 어두운 막처럼 화면을 거의 다 덮는 것은 피할 수 없으므로 뺀다. 코치 마크처럼 덮개를 조각내 그린 막은
        // 조각마다 작아 이 비율에 걸리지 않으므로 캔버스째 뺀다.
        private void AddOtherScreenAreas()
        {
            Canvas ownRoot = canvasRect.GetComponent<Canvas>().rootCanvas;
            canvases ??= FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            float backdropArea =
                canvasRect.rect.width * canvasRect.rect.height * BackdropScreenShare;
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled)
                {
                    continue;
                }

                Canvas root = canvas.rootCanvas;
                if (
                    root == ownRoot
                    || root.renderMode == RenderMode.WorldSpace
                    || root.TryGetComponent(out IScreenBackdrop _)
                )
                {
                    continue;
                }

                Camera camera = CameraOf(root);
                IList<Graphic> graphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
                for (int i = 0; i < graphics.Count; i++)
                {
                    if (!IsDrawn(graphics[i]))
                    {
                        continue;
                    }

                    graphics[i].rectTransform.GetWorldCorners(corners);
                    Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(
                        camera,
                        corners[0]
                    );
                    Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                    Rect area = ToCanvas(
                        Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y)
                    );
                    if (area.width * area.height < backdropArea)
                    {
                        avoidAreas.Add(area);
                    }
                }
            }
        }

        private static bool IsDrawn(Graphic graphic)
        {
            return graphic.isActiveAndEnabled
                && !graphic.canvasRenderer.cull
                && graphic.color.a * graphic.canvasRenderer.GetInheritedAlpha() > .01f;
        }

        private static Camera CameraOf(Canvas root)
        {
            return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        }

        // 화면 좌표(픽셀)를 카드 캔버스의 왼쪽 아래 기준 좌표로 바꾼다.
        private Vector2 ToCanvas(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                CameraOf(canvasRect.GetComponent<Canvas>().rootCanvas),
                out Vector2 local
            );
            return local + canvasRect.rect.size * canvasRect.pivot;
        }

        private Rect ToCanvas(Rect screenArea)
        {
            Vector2 min = ToCanvas(screenArea.min);
            Vector2 max = ToCanvas(screenArea.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // 서버 단어 카드가 없으면 단어 버튼과 안내를 숨기고 [닫기]만 보인다.
        private void ShowButtons(bool isPinned, WordCardButtons buttons)
        {
            bool hasWordActions = isPinned && buttons.HasWordActions;
            actionRow.SetActive(hasWordActions);
            closeButton.gameObject.SetActive(isPinned);
            wordActionRow.SetActive(hasWordActions);
            hideButton.gameObject.SetActive(hasWordActions);
            statusLabel.text = buttons.Message;
            statusLabel.gameObject.SetActive(
                hasWordActions && !string.IsNullOrEmpty(buttons.Message)
            );
            if (!hasWordActions)
            {
                return;
            }

            saveLabel.text = buttons.SaveLabel switch
            {
                "단어장에 저장" => "저장",
                "저장했어요" => "저장됨",
                _ => buttons.SaveLabel,
            };
            saveButton.interactable = buttons.CanSave;
            hideLabel.text =
                buttons.HideLabel == "알아요 · 다시 안 띄우기"
                    ? "알아요 · 호버에서 숨기기"
                    : buttons.HideLabel;
            hideButton.interactable = buttons.CanToggleHide;
            pronunciationButton.gameObject.SetActive(buttons.HasPronunciation);
        }

        private void Awake()
        {
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            saveButton.onClick.AddListener(() => SaveRequested?.Invoke());
            pronunciationButton.onClick.AddListener(() => PronunciationRequested?.Invoke());
            hideButton.onClick.AddListener(() => HoverHiddenToggleRequested?.Invoke());
            Hide();
        }
    }
}
