using System.Collections.Generic;
using A305.Player;
using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>
    /// 누른 자리에 있는 UI가 사물 클릭을 막는지 본다. 휴대폰·터치 노트북에서 화면을 덮는 터치 조작판(이동·시점 끌기 영역)은
    /// 끌기만 받으므로 막지 않는다. 그 밖의 UI(카드 버튼, HUD, 코치 마크 덮개 등)는 막는다.
    /// </summary>
    public static class WorldClickBlocker
    {
        public static bool IsBlockedBy(IEnumerable<GameObject> uiHits)
        {
            foreach (GameObject hit in uiHits)
            {
                if (hit.GetComponentInParent<TouchDragArea>() == null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
