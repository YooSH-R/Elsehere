using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;
using A305.Npc;
using A305.Player;
using A305.World;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.Learning
{
    /// <summary>
    /// 휴대폰 [이웃] 앱으로 여는 마을 NPC 목록(WORLD-08). 열 때마다 서버 목록을 다시 읽어 [대화 노트]처럼 한 쪽에 4명씩 보인다.
    /// 끝까지 대화한 NPC(열린 이웃)는 이름·관계를, 잠긴 NPC는 역할·장소만 보이고, 열린 이웃을 누르면 정보 카드(지금 위치 포함)를 보인다.
    /// 열려 있는 동안 이동을 잠그고, 대화처럼 다른 화면이 이동을 잠근 동안에는 열지 않는다. 단어장 HUD 프리팹에 붙어 있다.
    /// </summary>
    public class NeighborsView : MonoBehaviour
    {
        [SerializeField]
        private ApiSettings apiSettings;

        [SerializeField]
        [Tooltip("휴대폰의 [이웃] 버튼")]
        private Button openButton;

        [SerializeField]
        [Tooltip("NPC 일과(Data/Npc/NpcLife.json). 정보 카드의 지금 위치를 정한다.")]
        private TextAsset lifeData;

        [SerializeField]
        [Tooltip("목록 뒤 장면을 어둡게 덮는 그림자. 목록과 함께 켜고 끈다.")]
        private GameObject shade;

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        [Tooltip("머리줄의 \"열린 이웃 / 전체\" 수")]
        private TMP_Text countText;

        [SerializeField]
        [Tooltip("NPC 줄을 담은 칸. 정보 카드를 보이는 동안 숨긴다.")]
        private GameObject rowsGroup;

        [SerializeField]
        private NeighborRow[] rows;

        [SerializeField]
        [Tooltip("NPC 색 아이콘 그림. [대화 노트]와 같은 그림을 같은 순서로 둔다.")]
        private Sprite[] avatarSprites;

        [SerializeField]
        [Tooltip("쪽 넘김 줄. 정보 카드를 보이는 동안 숨긴다.")]
        private GameObject pager;

        [SerializeField]
        private TMP_Text pageText;

        [SerializeField]
        [Tooltip("보일 NPC가 없을 때 줄 자리에 보이는 안내(불러오는 중·없음·실패)")]
        private TMP_Text emptyText;

        [SerializeField]
        [Tooltip("NPC를 보이는 동안 목록 아래 안내(다시 불러오는 중·실패·아직 열린 이웃 없음)")]
        private TMP_Text statusText;

        [SerializeField]
        private Button previousButton;

        [SerializeField]
        private Button nextButton;

        [SerializeField]
        private Button retryButton;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        [Tooltip("열린 이웃을 누르면 목록 자리에 보이는 정보 카드")]
        private GameObject card;

        [SerializeField]
        private TMP_Text cardNameText;

        [SerializeField]
        private TMP_Text cardFactsText;

        [SerializeField]
        private Button backButton;

        [SerializeField]
        [Tooltip("정보 카드의 [지도에서 보기]. 지도에 찍을 자리가 있을 때만 보인다.")]
        private Button mapButton;

        [SerializeField]
        [Tooltip("[지도에서 보기] 대신 보이는 안내(숙소에서 쉼·마을에 나가면 보임)")]
        private TMP_Text mapHintText;

        private NeighborsBook book;
        private PlayerMover player;
        private Dictionary<string, NpcLifeEntry> lives;

        /// <summary>목록을 보인다. 서버 요청 없이 <paramref name="shownBook"/>의 지금 상태를 그린다.</summary>
        public void Show(NeighborsBook shownBook)
        {
            book = shownBook;
            shade.SetActive(true);
            panel.SetActive(true);
            Refresh();
        }

        private void Awake()
        {
            WindowFrame.Attach((RectTransform)panel.transform);
            previousButton.onClick.AddListener(() => Change(() => book.PreviousPage()));
            nextButton.onClick.AddListener(() => Change(() => book.NextPage()));
            backButton.onClick.AddListener(() => Change(() => book.Back()));
            for (int slot = 0; slot < rows.Length; slot++)
            {
                int shownSlot = slot;
                rows[slot].Button.onClick.AddListener(() => Change(() => SelectSlot(shownSlot)));
            }
            closeButton.onClick.AddListener(Close);
            retryButton.onClick.AddListener(() => _ = LoadAsync());
            mapButton.onClick.AddListener(ShowOnMap);
            lives =
                lifeData == null
                    ? new Dictionary<string, NpcLifeEntry>()
                    : NpcLifeData.Parse(lifeData.text).Npcs.ToDictionary(life => life.NpcId);
            Hide();
            if (apiSettings == null)
            {
                Debug.LogError("NeighborsView의 apiSettings가 연결되지 않았습니다.", this);
                openButton.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            openButton.onClick.AddListener(Open);
        }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerMover>();
        }

        // 다른 화면이 이동을 잠근 동안에는 열지 않는다. 닫을 때 그 잠금을 풀어 버리지 않기 위함이다.
        private void Update()
        {
            openButton.interactable =
                panel.activeSelf || player == null || !player.IsMovementLocked;
        }

        private void Open()
        {
            if (panel.activeSelf)
            {
                return;
            }

            Show(new NeighborsBook(new NeighborsApi(apiSettings.CreateClient())));
            SetMovementLocked(true);
            _ = LoadAsync();
        }

        private void Close()
        {
            if (!panel.activeSelf)
            {
                return;
            }

            Hide();
            SetMovementLocked(false);
        }

        private void Hide()
        {
            shade.SetActive(false);
            panel.SetActive(false);
        }

        // 버튼 이벤트에서 부르므로 서버 오류가 아닌 예외는 로그로 남긴다.
        private async Task LoadAsync()
        {
            NeighborsBook loading = book;
            Task load = loading.LoadAsync();
            Refresh();
            try
            {
                await load;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            // 기다리는 동안 창을 닫거나 다시 열었으면 지금 화면을 건드리지 않는다.
            if (this != null && panel.activeSelf && book == loading)
            {
                Refresh();
            }
        }

        private void SelectSlot(int slot)
        {
            IReadOnlyList<Neighbor> shown = book.PageItems;
            if (slot < shown.Count)
            {
                book.Select(shown[slot]);
            }
        }

        private void Change(Action change)
        {
            change();
            Refresh();
        }

        private void Refresh()
        {
            countText.text = book.CountText;
            Neighbor selected = book.Selected;
            card.SetActive(selected != null);
            rowsGroup.SetActive(selected == null);
            pager.SetActive(selected == null);
            if (selected != null)
            {
                cardNameText.text = NeighborsText.Name(selected);
                cardFactsText.text = NeighborsText.Facts(selected, Whereabouts(selected));
                string mapHint = NeighborsText.MapHint(Locate(selected).Spot, IsPlayerInTown());
                mapButton.gameObject.SetActive(mapHint == null);
                mapHintText.gameObject.SetActive(mapHint != null);
                mapHintText.text = mapHint ?? "";
                emptyText.text = "";
                statusText.text = "";
                retryButton.gameObject.SetActive(false);
                return;
            }

            IReadOnlyList<Neighbor> shown = book.PageItems;
            for (int slot = 0; slot < rows.Length; slot++)
            {
                if (slot >= shown.Count)
                {
                    rows[slot].Hide();
                    continue;
                }

                Neighbor neighbor = shown[slot];
                rows[slot]
                    .Show(
                        neighbor,
                        avatarSprites[NeighborsText.AvatarColor(neighbor, avatarSprites.Length)]
                    );
            }

            string status = book.StatusMessage ?? "";
            emptyText.text = shown.Count == 0 ? status : "";
            statusText.text = shown.Count > 0 ? status : "";
            retryButton.gameObject.SetActive(book.CanRetry);
            pageText.text = book.PageText;
            previousButton.interactable = book.CanGoPrevious;
            nextButton.interactable = book.CanGoNext;
        }

        // 지금 게임 시각의 일과로 정한다. 다른 장면에 있는 NPC도 같은 시각이면 같은 상태다.
        private string Whereabouts(Neighbor neighbor)
        {
            return NpcWhereabouts.Describe(
                LifeOf(neighbor),
                GameClock.Shared.Minutes,
                NpcDuty.RequiresPresence(neighbor.NpcCode)
            );
        }

        // 지도에 찍을 자리. 마을에 켜진 몸이 있으면 그 몸을, 없으면 일과로 일터나 숙소를 고른다.
        private NpcLocation Locate(Neighbor neighbor)
        {
            NpcIdentity body = NpcTracking.FindBody(neighbor.NpcCode);
            return NpcLocator.Locate(
                LifeOf(neighbor),
                GameClock.Shared.Minutes,
                NpcDuty.RequiresPresence(neighbor.NpcCode),
                IsPlayerInTown(),
                body != null ? body.transform.position : (Vector3?)null
            );
        }

        // 고른 이웃을 찾기 시작하고, 지도를 가리지 않게 창을 닫은 뒤 큰 지도를 연다.
        // 지도 핀과 머리 위 표시는 그 NPC 3m 안에 다가가거나 다른 이웃을 고를 때까지 남는다.
        private void ShowOnMap()
        {
            Neighbor selected = book?.Selected;
            if (selected == null)
            {
                return;
            }

            NpcTracking.Shared.Track(selected.NpcCode, selected.Name, LifeOf(selected));
            Close();
            JourneyStatus.Shared.RequestMap();
        }

        private NpcLifeEntry LifeOf(Neighbor neighbor)
        {
            lives.TryGetValue(neighbor.NpcCode ?? "", out NpcLifeEntry life);
            return life;
        }

        private static bool IsPlayerInTown() =>
            PlaceScenes.CodeOf(SceneManager.GetActiveScene().name) == "TOWN";

        private void SetMovementLocked(bool isLocked)
        {
            if (player != null)
            {
                player.SetMovementLocked(isLocked);
            }
        }
    }
}
