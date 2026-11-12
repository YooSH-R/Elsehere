using System;
using System.Collections.Generic;
using System.Linq;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace A305.Mission
{
    /// <summary>
    /// 퀘스트 창. 왼쪽 위 퀘스트 카드로 열면 먼저 같은 크기의 창 가운데에 이야기·장소 연습을 큰 그림으로 고르게 하고(시안 2-B ①),
    /// 고른 쪽을 열면 머리줄 왼쪽 ‹ 로 고르기 화면에 돌아간다(시안 2-B ②).
    /// 이야기는 퀘스트 목록(시안 c4)과 도장 지도(시안 q2)다. 목록의 카드를 누르면 그 퀘스트의 도장 지도로 간다.
    /// 목록은 서버가 준 퀘스트를 한 줄에 세 장씩 카드로 보이고(지금 하는 퀘스트는 코랄 테두리, 잠긴 퀘스트는 카드째 흐리게 하고 자물쇠),
    /// 쪽이 여럿이면 아래에서 넘긴다.
    /// 퀘스트를 끝내고 [다음]을 누르면 다음 퀘스트 카드의 잠금을 푸는 연출을 한다.
    /// 도장 지도는 가 본 곳의 잉크 도장,
    /// 지금 갈 곳의 번호 핀, 지나온 길을 마을 지도 위에 얹는다. 자리는 시안 px × 1.2이고 버튼 연결은 QuestBoardBuilder가 넣는다.
    /// 장소 연습 쪽(<see cref="PracticeBoardView"/>)에서 상황의 [연습하기]를 누르면 창을 닫고 그 가게 직원에게 연습하러 가자고 알린다(<see cref="PracticeVisit"/>).
    /// </summary>
    public sealed class QuestBoardView : MonoBehaviour
    {
        private const float Px = 1.2f;

        // 들르는 곳 알약(시안 c4 .rp): 글자 양옆 13, 알약 높이 34, 알약·화살표와 줄 사이 6. 화살표(›)는 폭 5.38을 칸 10 가운데에 둔다.
        // 알약 줄은 폭 342 안에서 넘치면 다음 줄로 넘기고, 카드 아래 71에서 끝나 줄 수만큼 위로 쌓인다.
        private const float RouteTextPadding = 13;
        private const float RouteArtPadding = 0;
        private const float RouteHeight = 34;
        private const float RouteGap = 6;
        private const float ArrowWidth = 5.38f;
        private const float ArrowBox = 10;
        private const float RouteWidth = 342;

        // 상태 칩(진행 중·새로 열림) 그림은 높이 44로 그렸고, 줄인 카드(시안 c4)에서는 높이 38로 줄여 쓴다.
        private const float ChipScale = 38f / 44f;

        // 지금 하는 퀘스트의 카드는 테두리가 3(다른 카드 1.5)이라 안의 내용을 1.5 들인다. 잠긴 퀘스트는 카드째 흐리게 한다.
        private const float CurrentBorderInset = 1.5f;
        private const float LockedAlpha = .55f;

        // 판 크기: [이야기]·[장소 연습] 탭(시안 #story·#place .panel)은 같은 1520×800, 도장 지도(시안 q2)는 1240×770이다.
        private const float TabWidth = 1520;
        private const float TabHeight = 800;
        private const float MapWidth = 1240;
        private const float MapHeight = 770;

        // 도장 지도: 지도 그림(2000×800)을 시안 1168×467에 담는다. 도장은 건물이 보이게 24 위로 찍는다.
        private const float MapScaleX = 1168f / 2000f;
        private const float MapScaleY = 467f / 800f;
        private const float StampLift = 24;
        private const float DotSpacing = 15;

        [SerializeField]
        [Tooltip("목록과 도장 지도를 담는 판. 보이는 쪽에 따라 높이를 바꾼다.")]
        private RectTransform sheet;

        [SerializeField]
        [Tooltip("이야기·장소 연습을 큰 그림으로 고르는 첫 쪽")]
        private GameObject choosePage;

        [SerializeField]
        private GameObject listPage;

        [SerializeField]
        private GameObject mapPage;

        [SerializeField]
        [Tooltip("[장소 연습] 탭의 쪽")]
        private PracticeBoardView practicePage;

        [SerializeField]
        private Button startButton;

        [SerializeField]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private TMP_Text pageText;

        [SerializeField]
        [Tooltip("연계 퀘스트 카드의 잠금을 푸는 연출")]
        private QuestUnlockEffect unlock;

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

        /// <summary>장소 연습 탭을 열 때마다 서버에 장소 목록을 묻는다.</summary>
        public event Action PracticeRequested;

        public bool IsOpen => gameObject.activeSelf;
        public string SelectedQuestId =>
            board != null && shownQuest >= 0 && shownQuest < board.Quests.Count
                ? board.Quests[shownQuest].Id
                : null;

        /// <summary>창을 열면 이야기·장소 연습 고르기부터 보인다. 이야기 목록은 미리 서버에 묻는다.</summary>
        public void Open()
        {
            gameObject.SetActive(true);
            ShowChooser();
            OpenChanged?.Invoke(true);
            Requested?.Invoke();
        }

        /// <summary>이야기·장소 연습을 큰 그림으로 고르는 쪽(시안 2-B ①). 두 쪽 머리줄의 ‹ 도 여기로 돌아온다.</summary>
        public void ShowChooser()
        {
            ShowOnly(choosePage);
            shownQuest = -1;
            FitSheet();
        }

        // 쪽 하나만 켠다.
        private void ShowOnly(GameObject page)
        {
            choosePage.SetActive(page == choosePage);
            listPage.SetActive(page == listPage);
            mapPage.SetActive(page == mapPage);
            practicePage.gameObject.SetActive(page == practicePage.gameObject);
        }

        public void Close()
        {
            unlock.Hide();
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
            ShowOnly(listPage);
            shownQuest = -1;
            FitSheet();
        }

        /// <summary>고르기 화면의 [이야기]. 장소 연습을 보는 사이 바뀌었을 수 있어 퀘스트 목록을 다시 묻는다.</summary>
        public void ShowStory()
        {
            ShowList();
            Requested?.Invoke();
        }

        /// <summary>고르기 화면의 [장소 연습]. 열 때마다 끝낸 기록을 새로 받는다.</summary>
        public void ShowPractice()
        {
            ShowOnly(practicePage.gameObject);
            shownQuest = -1;
            FitSheet();
            PracticeRequested?.Invoke();
        }

        /// <summary>서버에서 받은 장소 연습 목록을 보인다.</summary>
        public void ShowPracticePlaces(PracticeBoardState state) => practicePage.Show(state);

        /// <summary>
        /// 오른쪽 slot번째 상황의 [연습하기]·[다시 하기]. 창을 닫고 그 가게 직원에게 연습하러 가자고 알린다.
        /// </summary>
        public void StartPractice(int slot)
        {
            PracticeVisitPlan plan = practicePage.PlanFor(slot);
            if (plan == null)
                return;
            Close();
            PracticeVisit.Shared.Request(plan);
        }

        /// <summary>목록에서 index번째 퀘스트 카드의 잠금을 푼다(시안 f2). 그 카드가 있는 쪽을 보인다.</summary>
        public void PlayUnlock(int index)
        {
            if (board == null || index < 0 || index >= board.Quests.Count)
                return;
            ShowList();
            page = index / cards.Length;
            FillCards();
            unlock.Play(cards[index % cards.Length].Root.anchoredPosition);
        }

        /// <summary>목록·팡파레가 함께 쓰는 퀘스트 아이콘. 퀘스트 차례대로 쓰고 모자라면 처음부터 다시 쓴다.</summary>
        public Sprite IconOf(int index) =>
            icons[
                board != null
                && index >= 0
                && index < board.Quests.Count
                && board.Quests[index].Id == FirstDayMissionState.ShowcaseQuestId
                    ? 1 % icons.Length
                    : index % icons.Length
            ];

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
            ShowOnly(mapPage);
            FitSheet();
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

        // 판 크기를 보이는 쪽에 맞춘다(두 탭은 같은 크기). 목록은 쪽이 하나면 쪽 넘기기를 숨긴다.
        private void FitSheet()
        {
            bool paged = PageCount > 1;
            foreach (Selectable pager in new Selectable[] { previousButton, nextButton })
                pager.gameObject.SetActive(paged);
            pageText.gameObject.SetActive(paged);
            bool map = mapPage.activeSelf;
            sheet.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                (map ? MapWidth : TabWidth) * Px
            );
            sheet.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                (map ? MapHeight : TabHeight) * Px
            );
        }

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
            if (listPage.activeSelf)
                FitSheet();
        }

        private void FillCard(QuestCardSlot card, QuestEntry quest, int index)
        {
            bool current = quest.Status == QuestEntry.InProgress;
            card.Background.sprite = current ? currentCardSprite : cardSprite;
            float inset = current ? CurrentBorderInset * Px : 0;
            card.Content.offsetMin = new Vector2(inset, inset);
            card.Content.offsetMax = new Vector2(-inset, -inset);
            card.Icon.sprite = IconOf(index);
            card.Title.text = quest.Title;
            card.Description.text = quest.Description;
            card.Meta.text = QuestBoardText.Meta(quest);
            // 잠긴 퀘스트는 카드째 흐리게 하고 가운데에 자물쇠를 둔다.
            bool locked = quest.Status == QuestEntry.Locked;
            card.Button.interactable = !locked;
            card.Group.alpha = locked ? LockedAlpha : 1;
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
                card.Chip.rectTransform.sizeDelta = chip.rect.size / 2 * ChipScale;
            }
            card.DoneStamp.SetActive(quest.Status == QuestEntry.Done);
            FillRoute(card.Route, QuestBoardText.Route(quest));
        }

        // 들르는 곳 알약을 '›'로 이어 왼쪽부터 놓고, 줄 폭을 넘으면 다음 줄로 넘긴다(시안 #story). 줄 칸은 아래 끝에 붙어 있어
        // 줄 수만큼 높이를 늘리면 위로 쌓인다. 자리는 보이는 알약 기준(시안 px)으로 센다.
        private void FillRoute(RectTransform row, List<RoutePlace> route)
        {
            foreach (Transform old in row.Cast<Transform>().ToArray())
                DestroyNow(old.gameObject);
            float x = 0;
            int line = 0;
            for (int i = 0; i < route.Count; i++)
            {
                if (i > 0)
                {
                    PlaceInRoute(
                        Make(routeArrow.gameObject, row),
                        ref x,
                        ref line,
                        ArrowWidth,
                        ArrowBox
                    );
                }
                GameObject chip = Make(routeChip.gameObject, row);
                chip.GetComponent<Image>().sprite = route[i].Visited
                    ? visitedRouteSprite
                    : routeSprite;
                var label = chip.GetComponentInChildren<TMP_Text>();
                label.text = route[i].Place;
                label.color = route[i].Visited ? VisitedInk : RouteInk;
                float width =
                    label.GetPreferredValues(route[i].Place).x / Px + RouteTextPadding * 2;
                PlaceInRoute(chip, ref x, ref line, width, width + RouteArtPadding * 2);
            }
            float height = RouteHeight * (line + 1) + RouteGap * line;
            row.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                (height + RouteArtPadding * 2) * Px
            );
        }

        // 보이는 폭(width)의 부품을 지금 줄에 놓고 다음 자리로 간다. 줄에 들어가지 않으면 다음 줄 처음에 놓는다.
        // 그림 칸(box)은 보이는 폭 가운데에 맞춘다.
        private static void PlaceInRoute(
            GameObject item,
            ref float x,
            ref int line,
            float width,
            float box
        )
        {
            if (x > 0 && x + width > RouteWidth)
            {
                x = 0;
                line++;
            }
            var rect = (RectTransform)item.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            float boxHeight = box > ArrowBox ? RouteHeight + RouteArtPadding * 2 : RouteHeight;
            float boxTop = line * (RouteHeight + RouteGap) + (box > ArrowBox ? 0 : RouteArtPadding);
            rect.anchoredPosition =
                new Vector2(x + RouteArtPadding - (box - width) / 2, -boxTop) * Px;
            rect.sizeDelta = new Vector2(box, boxHeight) * Px;
            x += width + RouteGap;
        }

        // 알약 글자: 가 본 곳은 코랄(시안 .pill), 아직이면 잉크(시안 .card.mid .pill)
        private static readonly Color RouteInk = new Color32(47, 37, 27, 255);
        private static readonly Color VisitedInk = new Color32(214, 93, 64, 255);

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

        /// <summary>장소의 잉크 도장. 도장이 없거나 삭제됐으면 첫 도장을 쓴다.</summary>
        public Sprite StampOf(string place)
        {
            Sprite placeStamp = placeStamps.FirstOrDefault(stamp => stamp.Place == place).Sprite;
            return placeStamp != null ? placeStamp : placeStamps[0].Sprite;
        }

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

        /// <summary>카드 안의 내용. 지금 하는 퀘스트의 두꺼운 테두리만큼 들인다.</summary>
        public RectTransform Content;
        public Image Icon;
        public Image Chip;
        public GameObject DoneStamp;
        public TMP_Text Title;
        public TMP_Text Description;
        public RectTransform Route;
        public TMP_Text Meta;

        /// <summary>잠긴 퀘스트에만 켜는 자물쇠</summary>
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
