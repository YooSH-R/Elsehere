using System;
using System.Collections.Generic;
using A305.Dialogue;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 왼쪽 퀘스트 카드 바로 아래의 기다리는 카드. 대화를 마친 NPC가 준비해 곧 건네기로 한 것(<see cref="FollowUpStore"/>)마다
    /// 먼저 되는 것부터 한 줄씩 받을 것·누가 어디서·남은 시간을 보이고, 다 되면 받으러 가라는 알림으로 바꿔 한 번 반짝인다.
    /// 기다리는 것이 없거나 미션 화면이 왼쪽을 쓸 수 없을 때(튜토리얼, 수첩·퀘스트 목록·큰 지도를 연 동안)는 숨긴다.
    /// 줄 끝의 X를 누르면 그 줄을 닫는다.
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
        [Tooltip("줄들. 첫 줄을 본떠 기다리는 것 수만큼 줄을 만든다.")]
        private RectTransform rows;

        [Header("퀘스트 카드 목표 체크와 같은 그림")]
        [SerializeField]
        private Sprite waitingMark;

        [SerializeField]
        private Sprite readyMark;

        private bool allowed;
        private long shownSecond = -1;

        // 닫은 것을 서버에 알릴 API. 서버 없이 띄운 화면에서는 이 기기에서만 지운다.
        private IFollowUpApi api;

        // 줄 순서대로 보이고 있는 것. 다 된 것을 반짝일 줄을 찾는다.
        private readonly List<long> shownFollowUpIds = new List<long>();

        private static FollowUpStore Store => FollowUpStore.Shared;

        public GameObject Card => card.gameObject;

        /// <summary>X로 닫은 것을 알릴 서버 API를 정한다.</summary>
        public void Bind(IFollowUpApi api) => this.api = api;

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
            card.gameObject.SetActive(allowed && items.Count > 0);
            shownFollowUpIds.Clear();
            if (items.Count == 0)
            {
                return;
            }

            double now = Store.Now;
            GameObject template = rows.GetChild(0).gameObject;
            while (rows.childCount < items.Count)
            {
                Instantiate(template, rows, false);
            }

            for (int i = 0; i < rows.childCount; i++)
            {
                var row = rows.GetChild(i).GetComponent<FollowUpRowView>();
                row.gameObject.SetActive(i < items.Count);
                if (i >= items.Count)
                {
                    continue;
                }

                WaitingFollowUp followUp = items[i];
                row.FollowUpId = followUp.FollowUpId;
                row.Show(
                    FollowUpCardText.Row(followUp, now),
                    followUp.IsReady(now) ? readyMark : waitingMark
                );
                shownFollowUpIds.Add(followUp.FollowUpId);
            }
        }

        private void OnEnable()
        {
            Store.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Store.Changed -= Refresh;
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
                int index = shownFollowUpIds.IndexOf(followUp.FollowUpId);
                if (index >= 0)
                {
                    rows.GetChild(index).GetComponent<FollowUpRowView>().Flash();
                }
            }
        }

        // 퀘스트 카드 높이는 할 일 줄 수에 따라 바뀌므로 매 프레임 그 아래에 붙이고, 좁은 화면에서 줄어든 폭도 따른다.
        // 하는 퀘스트가 없어 퀘스트 아이콘이 대신 보이면 아이콘 아래에 붙인다.
        private void LateUpdate()
        {
            if (!card.gameObject.activeInHierarchy || questCard == null)
            {
                return;
            }

            RectTransform above =
                questIcon != null
                && questIcon.gameObject.activeInHierarchy
                && !questCard.gameObject.activeInHierarchy
                    ? questIcon
                    : questCard;
            card.anchoredPosition =
                above.anchoredPosition + Vector2.down * (above.rect.height + Gap);
            card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, questCard.rect.width);
        }
    }
}
