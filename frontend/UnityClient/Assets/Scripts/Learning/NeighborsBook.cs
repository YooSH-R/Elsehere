using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using A305.Api;

namespace A305.Learning
{
    /// <summary>
    /// [이웃] 창 목록(WORLD-08). 마을 NPC를 서버가 준 순서(열린 이웃 먼저)대로 한 쪽에 <see cref="PageSize"/>명씩 보이고,
    /// 열린 이웃을 고르면 그 NPC의 정보 카드를 보인다. 다시 읽지 못하면 이전 목록을 그대로 두고 실패를 알린다.
    /// </summary>
    public sealed class NeighborsBook
    {
        /// <summary>[피드백 목록]과 같은 판에 같은 줄 수만큼 보인다.</summary>
        public const int PageSize = ConversationHistoryPages.PageSize;

        private readonly INeighborsApi api;
        private List<Neighbor> neighbors = new List<Neighbor>();
        private int neighborCount;
        private int totalCount;
        private int pageIndex;
        private bool isLoaded;
        private bool isLoading;
        private bool hasFailed;

        public NeighborsBook(INeighborsApi api)
        {
            this.api = api;
        }

        public IReadOnlyList<Neighbor> PageItems =>
            neighbors.Skip(pageIndex * PageSize).Take(PageSize).ToList();

        public string PageText => $"{pageIndex + 1} / {PageCount}";

        public bool CanGoPrevious => pageIndex > 0;

        public bool CanGoNext => pageIndex < PageCount - 1;

        public bool CanRetry => hasFailed && !isLoading;

        /// <summary>"3 / 36명". 목록을 받기 전에는 비어 있다.</summary>
        public string CountText => isLoaded ? NeighborsText.Count(neighborCount, totalCount) : "";

        /// <summary>정보 카드를 보이는 이웃. 목록을 보이는 동안은 null.</summary>
        public Neighbor Selected { get; private set; }

        /// <summary>목록 대신(또는 목록 아래에) 보여 줄 안내. 보여 줄 것이 없으면 null.</summary>
        public string StatusMessage
        {
            get
            {
                if (isLoading)
                {
                    return NeighborsText.Loading;
                }

                if (hasFailed)
                {
                    return NeighborsText.Failed;
                }

                if (!isLoaded)
                {
                    return null;
                }

                if (neighbors.Count == 0)
                {
                    return NeighborsText.Empty;
                }

                return neighborCount == 0 ? NeighborsText.NoneOpenYet : null;
            }
        }

        private int PageCount => Math.Max(1, (neighbors.Count + PageSize - 1) / PageSize);

        /// <summary>서버 목록을 다시 읽어 첫 쪽의 목록부터 보인다.</summary>
        public async Task LoadAsync()
        {
            isLoading = true;
            try
            {
                NeighborList list = await api.GetNeighborsAsync();
                neighbors = list.Npcs ?? new List<Neighbor>();
                neighborCount = list.NeighborCount;
                totalCount = list.TotalCount;
                pageIndex = 0;
                Selected = null;
                isLoaded = true;
                hasFailed = false;
            }
            catch (ApiException)
            {
                hasFailed = true;
            }
            finally
            {
                isLoading = false;
            }
        }

        /// <summary>열린 이웃이면 정보 카드를 보인다. 잠긴 NPC는 고를 수 없다.</summary>
        public void Select(Neighbor neighbor)
        {
            if (neighbor != null && neighbor.Unlocked)
            {
                Selected = neighbor;
            }
        }

        /// <summary>정보 카드를 닫고 목록으로 돌아간다.</summary>
        public void Back()
        {
            Selected = null;
        }

        public void NextPage()
        {
            if (CanGoNext)
            {
                pageIndex++;
            }
        }

        public void PreviousPage()
        {
            if (CanGoPrevious)
            {
                pageIndex--;
            }
        }
    }
}
