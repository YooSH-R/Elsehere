using System.Collections.Generic;
using UnityEngine;

namespace A305.ObjectHover
{
    public static class WorldClickBlocker
    {
        public static bool IsBlockedBy(IEnumerable<GameObject> uiHits) => false;
    }
}
