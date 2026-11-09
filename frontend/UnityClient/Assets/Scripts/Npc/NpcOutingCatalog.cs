using System;
using UnityEngine;

namespace A305.Npc
{
    /// <summary>기존 시설의 출입 지점과 출퇴근 외형을 연결한다. NpcOutingBuilder가 생성한다.</summary>
    public sealed class NpcOutingCatalog : ScriptableObject
    {
        public TextAsset LifeData;
        public RuntimeAnimatorController LifeAnimator;
        public NpcOutingVenue[] Venues = Array.Empty<NpcOutingVenue>();
        public GameObject[] Visitors = Array.Empty<GameObject>();
    }

    [Serializable]
    public sealed class NpcOutingVenue
    {
        public string Code;
        public string Scene;
        public Vector3 Entrance;
        public Vector3 Inside;
        public Vector3 WalkTo;
        public bool IsOutdoor;
        public int Capacity = 2;
    }
}
