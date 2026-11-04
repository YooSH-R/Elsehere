using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>
    /// 퀘스트 목록(시안 q1)과 도장 지도(시안 q2). 왼쪽 위 퀘스트 카드로 열고, 목록의 카드를 누르면 그 퀘스트의 도장 지도로 간다.
    /// 목록은 서버가 준 퀘스트를 한 쪽에 여섯 장씩 카드로 보이고(지금 하는 퀘스트는 코랄 테두리, 잠긴 퀘스트는 흐리게), 아래 가운데에서 쪽을 넘긴다.
    /// 도장 지도는 가 본 곳의 잉크 도장,
    /// 지금 갈 곳의 번호 핀, 지나온 길을 마을 지도 위에 얹는다. 자리는 시안 px × 1.2이고 버튼 연결은 QuestBoardBuilder가 넣는다.
    /// </summary>
    public sealed class QuestBoardView : MonoBehaviour
    {
        private const float Px = 1.2f;

        // 들르는 곳 알약: 글자 양옆 11, 알약 그림의 여백 4
        private const float RoutePadding = (11 + 4) * 2;

        // 카드 안: 설명 시작(시안 119.44), 들르는 곳 줄이 놓일 가장 위 자리(154), 그 아래 보상 줄까지(42)
        private const float DescriptionTop = 119.44f;
        private const float RouteTop = 154;
        private const float MetaGap = 42;

        // 도장 지도: 지도 그림(2000×800)을 시안 1168×467에 담는다. 도장은 건물이 보이게 24 위로 찍는다.
        private const float MapScaleX = 1168f / 2000f;
        private const float MapScaleY = 467f / 800f;
        private const float StampLift = 24;
        private const float DotSpacing = 15;

        [SerializeField]
        private GameObject listPage;

        [SerializeField]
        private GameObject mapPage;

        [SerializeField]
        private Button startButton;

        [SerializeField]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private TMP_Text pageText;

        [SerializeField]
        private QuestCardSlot[] cards;

        [SerializeField]
        private Sprite cardSprite;

        [SerializeField]
        private Sprite currentCardSprite;

        [SerializeField]
        [Tooltip("퀘스트 아이콘. 퀘스트 차례대로 쓰고 모자라면 처음부터 다시 쓴다.")]
        private Sprite[] icons;

        [SerializeField]
        private Sprite nowChip;

        [SerializeField]
        private Sprite newChip;

        [SerializeField]
        [Tooltip("들르는 곳 알약. 꺼 둔 견본을 복사해 쓴다.")]
        private Image routeChip;

        [SerializeField]
        private TMP_Text routeArrow;

        [SerializeField]
        private Sprite routeSprite;

        [SerializeField]
        private Sprite visitedRouteSprite;

        [SerializeField]
        private TMP_Text mapTitle;

        [SerializeField]
        private RectTransform countChip;

        [SerializeField]
        private TMP_Text countText;

        [SerializeField]
        [Tooltip("지도 그림. 도장·핀·길 점은 이 위에 그림 왼쪽 위 기준으로 얹는다.")]
        private RectTransform map;

        [SerializeField]
        private Image stampTemplate;

        [SerializeField]
        private RectTransform dotTemplate;

        [SerializeField]
        private RectTransform pin;

        [SerializeField]
        private TMP_Text pinNumber;

        [SerializeField]
        private RectTransform nowLabel;

        [SerializeField]
        private PlaceStamp[] placeStamps;

        [SerializeField]
        private RectTransform stopsContent;

        [SerializeField]
        private QuestStopSlot stopTemplate;

        [SerializeField]
        private Sprite stopSprite;

        [SerializeField]
        private Sprite currentStopSprite;

        [SerializeField]
        private Sprite slotSprite;

        [SerializeField]
        private Sprite currentSlotSprite;

        private QuestBoardState board;
        private int page;
        private int shownQuest = -1;
        private readonly List<GameObject> made = new List<GameObject>();

        /// <summary>목록을 열 때마다 서버에 퀘스트 목록을 묻는다.</summary>
        public event Action Requested;

        /// <summary>도장 지도의 [퀘스트 시작]</summary>
        public event Action StartRequested;

        public event Action<bool> OpenChanged;

        public bool IsOpen => gameObject.activeSelf;

        public void Open()
        {
            gameObject.SetActive(true);
            ShowList();
            OpenChanged?.Invoke(true);
            Requested?.Invoke();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            OpenChanged?.Invoke(false);
        }

        public void Show(QuestBoardState state)
        {
            board = state;
            page = Mathf.Clamp(page, 0, PageCount - 1);
            FillCards();
            if (mapPage.activeSelf && shownQuest >= 0 && shownQuest < board.Quests.Count)
                ShowMap(shownQuest);
        }

        public void ShowList()
        {
            listPage.SetActive(true);
            mapPage.SetActive(false);
            shownQuest = -1;
        }

        /// <summary>지금 쪽의 slot번째 카드를 누르면 그 퀘스트의 도장 지도로 간다.</summary>
        public void ShowCard(int slot) => ShowMap(page * cards.Length + slot);

        public void NextPage() => TurnPage(1);

        public void PreviousPage() => TurnPage(-1);

        private int PageCount =>
            board == null
                ? 1
                : Mathf.Max(1, Mathf.CeilToInt(board.Quests.Count / (float)cards.Length));

        private void TurnPage(int step)
        {
            page = Mathf.Clamp(page + step, 0, PageCount - 1);
            FillCards();
        }

        public void ShowMap(int index)
        {
            if (board == null || index < 0 || index >= board.Quests.Count)
                return;
            QuestEntry quest = board.Quests[index];
            if (quest.Status == QuestEntry.Locked)
                return;
            shownQuest = index;
            listPage.SetActive(false);
            mapPage.SetActive(true);
            ClearMade();

            mapTitle.text = quest.Title;
            countText.text = QuestBoardText.StampCount(quest);
            // 제목 뒤 16에 도장 수 칩을 둔다. 칩 폭은 글자 + 양옆 14
            float titleWidth = mapTitle.GetPreferredValues(quest.Title).x;
            countChip.anchoredPosition = new Vector2(
                mapTitle.rectTransform.anchoredPosition.x + titleWidth + 16 * Px,
                countChip.anchoredPosition.y
            );
            countChip.sizeDelta = new Vector2(
                countText.GetPreferredValues(countText.text).x + 28 * Px,
                countChip.sizeDelta.y
            );
            startButton.gameObject.SetActive(quest.Status == QuestEntry.Available);

            DrawPath(QuestStampMap.Path(quest));
            foreach (QuestStamp stamp in QuestStampMap.Stamps(quest))
            {
                GameObject made = Track(Make(stampTemplate.gameObject, map));
                Place(made, stamp.Centre, StampLift);
                made.GetComponent<Image>().sprite = StampOf(stamp.Place);
            }
            QuestPin? next = QuestStampMap.Pin(quest);
            pin.gameObject.SetActive(next.HasValue);
            nowLabel.gameObject.SetActive(next.HasValue);
            if (next.HasValue)
            {
                pinNumber.text = next.Value.Number.ToString();
                Place(pin, next.Value.Centre, 70);
                // 칩은 핀 오른쪽 위(시안: 핀 가운데 + 34, 건물 가운데 위 86)
                Vector2 at = OnMap(next.Value.Centre);
                nowLabel.anchoredPosition = new Vector2(at.x + 34 * Px, at.y + 86 * Px);
            }
            // 새로 찍은 도장이 핀과 칩을 가리지 않게 맨 위로 올린다.
            pin.SetAsLastSibling();
            nowLabel.SetAsLastSibling();
            FillStops(quest);
        }

        public void RequestStart() => StartRequested?.Invoke();

        private void FillCards()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                QuestCardSlot card = cards[i];
                int index = page * cards.Length + i;
                bool shown = board != null && index < board.Quests.Count;
                card.Root.gameObject.SetActive(shown);
                if (shown)
                    FillCard(card, board.Quests[index], index);
            }
            pageText.text = $"{page + 1} / {PageCount}";
            previousButton.interactable = page > 0;
            nextButton.interactable = page < PageCount - 1;
        }

        private void FillCard(QuestCardSlot card, QuestEntry quest, int index)
        {
            card.Background.sprite =
                quest.Status == QuestEntry.InProgress ? currentCardSprite : cardSprite;
            card.Icon.sprite = icons[index % icons.Length];
            card.Title.text = quest.Title;
            card.Description.text = quest.Description;
            card.Meta.text = QuestBoardText.Meta(quest);
            // 잠긴 퀘스트는 카드를 옅은 흰 막으로 살짝 덮고 가운데에 자물쇠를 둔다.
            bool locked = quest.Status == QuestEntry.Locked;
            card.Button.interactable = !locked;
            card.Locked.SetActive(locked);

            Sprite chip = quest.Status switch
            {
                QuestEntry.InProgress => nowChip,
                QuestEntry.Available => newChip,
                _ => null,
            };
            card.Chip.gameObject.SetActive(chip != null);
            if (chip != null)
            {
                card.Chip.sprite = chip;
                card.Chip.rectTransform.sizeDelta = chip.rect.size / 2;
            }
            card.DoneStamp.SetActive(quest.Status == QuestEntry.Done);

            // 설명이 두 줄이면 들르는 곳 줄을 그만큼 내린다.
            float descriptionHeight =
                card.Description.GetPreferredValues(
                    quest.Description,
                    card.Description.rectTransform.rect.width,
                    0
                ).y / Px;
            float routeTop = Mathf.Max(RouteTop, DescriptionTop + descriptionHeight);
            card.Route.anchoredPosition = new Vector2(
                card.Route.anchoredPosition.x,
                -routeTop * Px
            );
            card.MetaRow.anchoredPosition = new Vector2(
                card.MetaRow.anchoredPosition.x,
                -(routeTop + MetaGap) * Px
            );

            foreach (Transform old in card.Route.Cast<Transform>().ToArray())
                DestroyNow(old.gameObject);
            List<RoutePlace> route = QuestBoardText.Route(quest);
            for (int i = 0; i < route.Count; i++)
            {
                if (i > 0)
                    Make(routeArrow.gameObject, card.Route);
                GameObject chipObject = Make(routeChip.gameObject, card.Route);
                chipObject.GetComponent<Image>().sprite = route[i].Visited
                    ? visitedRouteSprite
                    : routeSprite;
                var label = chipObject.GetComponentInChildren<TMP_Text>();
                label.text = route[i].Place;
                label.color = route[i].Visited ? VisitedInk : RouteInk;
                // 알약 폭은 글자 폭 + 양옆 여백이고, 글자는 알약 가운데에 둔다.
                chipObject.GetComponent<LayoutElement>().preferredWidth =
                    label.GetPreferredValues(route[i].Place).x + RoutePadding * Px;
            }
        }

        private static readonly Color RouteInk = new Color32(95, 82, 66, 255);
        private static readonly Color VisitedInk = new Color32(210, 79, 47, 255);

        private void FillStops(QuestEntry quest)
        {
            for (int i = 0; i < quest.Stops.Count; i++)
            {
                QuestStop stop = quest.Stops[i];
                GameObject made = Track(Make(stopTemplate.Root.gameObject, stopsContent));
                QuestStopSlot slot = stopTemplate.CopyIn(made.transform);
                slot.Background.sprite = stop.Current ? currentStopSprite : stopSprite;
                slot.Title.text = QuestBoardText.StopTitle(stop);
                slot.Todo.text = stop.Todo;
                slot.Todo.gameObject.SetActive(!string.IsNullOrEmpty(stop.Todo));
                slot.Stamp.gameObject.SetActive(stop.Done);
                slot.Slot.gameObject.SetActive(!stop.Done);
                if (stop.Done)
                    slot.Stamp.sprite = StampOf(stop.Place);
                else
                {
                    slot.Slot.sprite = stop.Current ? currentSlotSprite : slotSprite;
                    slot.Number.text = (i + 1).ToString();
                    slot.Number.color = stop.Current ? VisitedInk : SlotInk;
                }
            }
        }

        private static readonly Color SlotInk = new Color32(163, 149, 128, 255);

        private void DrawPath(List<Vector2> points)
        {
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 from = OnMap(points[i - 1]);
                Vector2 to = OnMap(points[i]);
                int dots = Mathf.Max(
                    1,
                    Mathf.RoundToInt(Vector2.Distance(from, to) / (DotSpacing * Px))
                );
                for (int dot = 0; dot <= dots; dot++)
                {
                    GameObject made = Track(Make(dotTemplate.gameObject, map));
                    ((RectTransform)made.transform).anchoredPosition = Vector2.Lerp(
                        from,
                        to,
                        dot / (float)dots
                    );
                }
            }
        }

        // 지도 그림 좌표(왼쪽 위 0, 아래로 +)를 지도 위 자리(왼쪽 위 기준, 위로 +)로 바꾼다.
        private static Vector2 OnMap(Vector2 art) =>
            new Vector2(art.x * MapScaleX, -art.y * MapScaleY) * Px;

        // 그림 좌표의 자리에서 시안 px lift만큼 위에 둔다.
        private static void Place(RectTransform item, Vector2 art, float lift) =>
            item.anchoredPosition = OnMap(art) + new Vector2(0, lift * Px);

        private static void Place(GameObject item, Vector2 art, float lift) =>
            Place((RectTransform)item.transform, art, lift);

        private Sprite StampOf(string place) =>
            placeStamps.FirstOrDefault(stamp => stamp.Place == place).Sprite
            ?? placeStamps[0].Sprite;

        // 꺼 둔 견본을 복사해 켠다. 이름은 견본과 같게 둔다.
        private static GameObject Make(GameObject template, Transform parent)
        {
            GameObject item = Instantiate(template, parent);
            item.name = template.name;
            item.SetActive(true);
            return item;
        }

        // 도장 지도에 얹은 것(도장·길 점·들르는 곳 칸)은 다른 퀘스트를 고르면 지운다.
        private GameObject Track(GameObject item)
        {
            made.Add(item);
            return item;
        }

        private void ClearMade()
        {
            foreach (GameObject item in made.Where(item => item != null))
                DestroyNow(item);
            made.Clear();
        }

        private static void DestroyNow(GameObject item)
        {
            if (Application.isPlaying)
                Destroy(item);
            else
                DestroyImmediate(item);
        }
    }

    [Serializable]
    public sealed class QuestCardSlot
    {
        public RectTransform Root;
        public Button Button;
        public CanvasGroup Group;
        public Image Background;
        public Image Icon;
        public Image Chip;
        public GameObject DoneStamp;
        public TMP_Text Title;
        public TMP_Text Description;
        public RectTransform Route;
        public RectTransform MetaRow;
        public TMP_Text Meta;

        /// <summary>잠긴 퀘스트에만 켜는 흰 막과 자물쇠</summary>
        public GameObject Locked;
    }

    [Serializable]
    public sealed class QuestStopSlot
    {
        public RectTransform Root;
        public Image Background;
        public Image Slot;
        public TMP_Text Number;
        public Image Stamp;
        public TMP_Text Title;
        public TMP_Text Todo;

        /// <summary>견본을 복사한 칸에서 같은 이름의 부품을 찾는다.</summary>
        public QuestStopSlot CopyIn(Transform copy) =>
            new QuestStopSlot
            {
                Root = (RectTransform)copy,
                Background = Find<Image>(copy, Background),
                Slot = Find<Image>(copy, Slot),
                Number = Find<TMP_Text>(copy, Number),
                Stamp = Find<Image>(copy, Stamp),
                Title = Find<TMP_Text>(copy, Title),
                Todo = Find<TMP_Text>(copy, Todo),
            };

        private static T Find<T>(Transform copy, Component original)
            where T : Component =>
            copy.GetComponentsInChildren<T>(true).First(item => item.name == original.name);
    }

    [Serializable]
    public struct PlaceStamp
    {
        public string Place;
        public Sprite Sprite;
    }
}
