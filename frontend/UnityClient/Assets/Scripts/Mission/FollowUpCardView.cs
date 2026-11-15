using System;
using System.Collections.Generic;
using A305.Dialogue;
using A305.World;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 왼쪽 퀘스트 카드 바로 아래의 기다리는 카드. 대화를 마친 NPC가 준비해 곧 건네기로 한 것(<see cref="FollowUpStore"/>)마다
    /// 먼저 되는 것부터 한 줄씩 받을 것·누가 어디서·남은 시간을 보이고, 다 되면 받으러 가라는 알림으로 바꿔 한 번 반짝인다.
    /// 기다리는 것이 없거나 미션 화면이 왼쪽을 쓸 수 없을 때(튜토리얼, 수첩·퀘스트 목록·큰 지도를 연 동안)는 숨긴다.
    /// 줄 끝의 X를 누르면 그 줄을 닫는다.
    /// 진행 중인 돌발 부탁(<see cref="FavorStore"/>)이 있으면 맨 위 줄에 부탁 제목·다음 할 일·남은 시간을 보이고, 그 줄의 X는 부탁을 그만둔다.
    /// </summary>
    public sealed class FollowUpCardView : MonoBehaviour
    {
        // 퀘스트 카드와 이 카드 사이(시안 px × 1.2)
        private const float Gap = 12 * 1.2f;

        [SerializeField]
        private RectTransform card;

        [SerializeField]
        [Tooltip("이 카드를 바로 아래에 붙일 왼쪽 위 퀘스트 카드")]
        private RectTransform questCard;

        [SerializeField]
        [Tooltip(
            "하는 퀘스트가 없어 퀘스트 카드 대신 보이는 퀘스트 아이콘. 이때는 이 아이콘 바로 아래에 붙인다."
        )]
        private RectTransform questIcon;

        [SerializeField]
        [Tooltip("하는 퀘스트를 접어 둔 알약. 카드를 접은 동안에는 이 알약 바로 아래에 붙인다.")]
        private RectTransform questPill;

        [SerializeField]
        [Tooltip("줄들. 첫 줄을 본떠 기다리는 것 수만큼 줄을 만든다.")]
        private RectTransform rows;

        [Header("퀘스트 카드 목표 체크와 같은 그림")]
        [SerializeField]
        private Sprite waitingMark;

        [SerializeField]
        private Sprite readyMark;

        private bool allowed;
        private long shownSecond = -1;
        private float bottomLimit = float.MaxValue;

        // 닫은 것을 서버에 알릴 API. 서버 없이 띄운 화면에서는 이 기기에서만 지운다.
        private IFollowUpApi api;

        // 그만둔 부탁을 서버에 알릴 API. 서버 없이 띄운 화면에서는 이 기기에서만 지운다.
        private IFavorApi favorApi;

        // 부탁 줄의 남은 시간을 셀 게임 시각(분). 테스트에서는 정한 값을 돌려준다.
        private Func<double> gameMinutes = () => GameClock.Shared.Minutes;

        // 줄 순서대로 보이고 있는 것. 다 된 것을 반짝일 줄을 찾는다.
        private readonly List<long> shownFollowUpIds = new List<long>();

        private static FollowUpStore Store => FollowUpStore.Shared;
        private static FavorStore Favors => FavorStore.Shared;

        // 부탁 줄은 기다리는 것이 아니라 반짝일 일이 없으므로 줄 순서를 맞추는 빈 자리로 둔다.
        private const long FavorRowMarker = -1;

        public GameObject Card => card.gameObject;

        /// <summary>
        /// 왼쪽 열이 내려갈 수 있는 끝(왼쪽 위에서 잰 캔버스 단위 거리). 데스크톱은 미니맵 위, 휴대폰은 조이스틱 위다.
        /// 퀘스트 카드 아래에 두면 이 끝을 넘을 때는 퀘스트 카드 오른쪽 옆에 둔다. 미션 화면이 매 프레임 정한다.
        /// </summary>
        public void SetBottomLimit(float limit) => bottomLimit = limit;

        /// <summary>X로 닫은 것을 알릴 서버 API를 정한다.</summary>
        public void Bind(IFollowUpApi api) => this.api = api;

        /// <summary>부탁 줄의 X로 그만둔 부탁을 알릴 서버 API를 정한다.</summary>
        public void Bind(IFavorApi favorApi) => this.favorApi = favorApi;

        /// <summary>부탁 줄의 남은 시간을 셀 게임 시각을 바꾼다. 테스트가 쓴다.</summary>
        public void UseGameClock(Func<double> gameMinutes) => this.gameMinutes = gameMinutes;

        /// <summary>부탁 줄 끝 X: 진행 중인 부탁을 그만둔다(FAV-02).</summary>
        public void AbandonFavor(long favorId)
        {
            if (favorApi != null)
                _ = Favors.AbandonAsync(favorApi, favorId);
            else
                Favors.Clear();
        }

        /// <summary>줄 끝 X: 그 줄을 닫는다.</summary>
        public void Dismiss(long followUpId)
        {
            if (api != null)
                _ = Store.DismissAsync(api, followUpId);
            else
                Store.Remove(followUpId);
        }

        /// <summary>미션 화면이 왼쪽 위를 쓸 수 있는지. 기다리는 것이 있어도 false면 숨긴다.</summary>
        public void SetAllowed(bool allowed)
        {
            this.allowed = allowed;
            Refresh();
        }

        /// <summary>지금 시각으로 줄과 남은 시간을 다시 그린다.</summary>
        public void Refresh()
        {
            IReadOnlyList<WaitingFollowUp> items = Store.Items;
            FavorUpdate favor = FavorCard.Shows(Favors.Current) ? Favors.Current : null;
            int favorRows = favor != null ? 1 : 0;
            int rowCount = favorRows + items.Count;
            card.gameObject.SetActive(allowed && rowCount > 0);
            shownFollowUpIds.Clear();
            if (rowCount == 0)
            {
                return;
            }

            double now = Store.Now;
            GameObject template = rows.GetChild(0).gameObject;
            while (rows.childCount < rowCount)
            {
                Instantiate(template, rows, false);
            }

            for (int i = 0; i < rows.childCount; i++)
            {
                var row = rows.GetChild(i).GetComponent<FollowUpRowView>();
                row.gameObject.SetActive(i < rowCount);
                if (i >= rowCount)
                {
                    continue;
                }

                if (i < favorRows)
                {
                    FollowUpCardRow favorText = FavorCard.Row(favor, gameMinutes());
                    row.FavorId = favor.FavorId;
                    row.Show(favorText, favorText.IsReady ? readyMark : waitingMark);
                    shownFollowUpIds.Add(FavorRowMarker);
                    continue;
                }

                WaitingFollowUp followUp = items[i - favorRows];
                row.FavorId = null;
                row.FollowUpId = followUp.FollowUpId;
                FollowUpCardRow text = FollowUpCardText.Row(followUp, now);
                row.Show(text, text.IsReady ? readyMark : waitingMark);
                shownFollowUpIds.Add(followUp.FollowUpId);
            }
        }

        private void OnEnable()
        {
            Store.Changed += Refresh;
            Favors.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Store.Changed -= Refresh;
            Favors.Changed -= Refresh;
        }

        private void Update()
        {
            // 보이는 동안에만 다 됐다는 알림을 꺼내, 수첩을 연 사이 다 된 것은 카드가 다시 보일 때 반짝인다.
            IReadOnlyList<WaitingFollowUp> newlyReady = card.gameObject.activeInHierarchy
                ? Store.TakeNewlyReady()
                : Array.Empty<WaitingFollowUp>();
            long second = (long)Math.Floor(Store.Now);
            if (second != shownSecond || newlyReady.Count > 0)
            {
                shownSecond = second;
                Refresh();
            }

            foreach (WaitingFollowUp followUp in newlyReady)
            {
                if (!FollowUpCardText.Row(followUp, Store.Now).IsReady)
                    continue;
                int index = shownFollowUpIds.IndexOf(followUp.FollowUpId);
                if (index >= 0)
                {
                    rows.GetChild(index).GetComponent<FollowUpRowView>().Flash();
                }
            }
        }

        // 퀘스트 카드 높이는 할 일 줄 수에 따라 바뀌므로 매 프레임 그 아래에 붙이고, 좁은 화면에서 줄어든 폭도 따른다.
        // 아래에 두면 왼쪽 아래(미니맵·조이스틱)까지 내려가면 퀘스트 카드 오른쪽 옆으로 옮긴다.
        // 카드를 알약으로 접었거나 하는 퀘스트가 없어 퀘스트 아이콘이 대신 보이면 그 아래에 붙인다.
        private void LateUpdate()
        {
            if (!card.gameObject.activeInHierarchy || questCard == null)
            {
                return;
            }

            RectTransform above = questCard;
            if (!questCard.gameObject.activeInHierarchy)
            {
                if (questPill != null && questPill.gameObject.activeInHierarchy)
                    above = questPill;
                else if (questIcon != null && questIcon.gameObject.activeInHierarchy)
                    above = questIcon;
            }
            // 좁은 화면에서 줄여 그린 퀘스트 카드와 같은 폭으로 보인다.
            card.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                questCard.rect.width * questCard.localScale.x
            );
            // 좁은 화면에서 줄여 그린 알약은 줄인 크기로 잰다.
            Vector2 size = Vector2.Scale(above.rect.size, above.localScale);
            Vector2 below = above.anchoredPosition + Vector2.down * (size.y + Gap);
            bool fitsBelow = -below.y + card.rect.height <= bottomLimit;
            card.anchoredPosition = fitsBelow
                ? below
                : above.anchoredPosition + Vector2.right * (size.x + Gap);
        }
    }
}
