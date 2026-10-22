using System.Collections.Generic;
using UnityEngine;

namespace A305.Mission
{
    /// <summary>
    /// 오늘의 미션을 찾는 곳. 실제 미션은 서버 데이터이며, 서버 연동 전에는 <see cref="MockMissionSource"/>를 쓴다.
    /// </summary>
    public abstract class MissionSource : ScriptableObject
    {
        public abstract IReadOnlyList<MissionContent> GetMissions();
    }
}
