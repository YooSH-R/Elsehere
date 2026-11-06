using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 경찰서 창구에서 받은 아직 출발하지 않은 신고 한 건. 경찰서는 마을과 다른 장면이라 창구가 순찰차를 직접 부를 수 없어,
    /// 신고를 여기 남겨 두면 마을이 열릴 때 순찰차(<see cref="PoliceCar"/>)가 가져가 출동한다. 순찰차가 한 대라 한 건만 받는다.
    /// 게임을 새로 시작하면 비운다.
    /// </summary>
    public static class PoliceDispatchOrder
    {
        private static string place;

        /// <summary>순찰차가 아직 가져가지 않은 신고가 있는지</summary>
        public static bool IsPending { get; private set; }

        /// <param name="placeCode">신고한 장소 코드. 장소를 말하지 않았으면 null이고 본길을 순찰한다.</param>
        /// <returns>새로 받았으면 true, 이미 기다리는 신고가 있어 받지 않았으면 false</returns>
        public static bool TryRequest(string placeCode)
        {
            if (IsPending)
            {
                return false;
            }

            place = placeCode;
            IsPending = true;
            return true;
        }

        /// <summary>기다리는 신고를 가져간다. 가져가면 없어진다.</summary>
        public static bool TryTake(out string placeCode)
        {
            placeCode = place;
            if (!IsPending)
            {
                return false;
            }

            Clear();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            place = null;
            IsPending = false;
        }
    }
}
