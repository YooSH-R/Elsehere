using UnityEngine;

namespace A305.World
{
    /// <summary>메인스트리트 Sandbox를 다시 만들 때 사용하는 크기와 확장 구간의 시작값.</summary>
    [CreateAssetMenu(fileName = "TownBlockoutSettings", menuName = "A305/World/Blockout Settings")]
    public class TownBlockoutSettings : ScriptableObject
    {
        [SerializeField, Min(0.5f)]
        private float characterHeightMeters = 1.6f;

        [SerializeField, Min(2.4f)]
        private float groundFloorHeightMeters = 4f;

        [SerializeField, Min(2f)]
        private float upperFloorHeightMeters = 3f;

        [SerializeField, Min(6f)]
        private float shopFootprintMeters = 8f;

        [SerializeField, Min(8f)]
        private float hotelFootprintMeters = 10f;

        [SerializeField, Min(6f)]
        private float homeFootprintMeters = 8f;

        [SerializeField, Min(0.1f)]
        private float parapetHeightMeters = 0.4f;

        [SerializeField, Min(1.6f)]
        private float doorwayWidthMeters = 2f;

        [SerializeField, Min(1.8f)]
        private float doorwayHeightMeters = 2.2f;

        [SerializeField, Range(0f, 80f)]
        [Tooltip("현재 블록의 북쪽 끝 Z=30 뒤에 붙이는 보행 시험 구간. 0이면 만들지 않습니다.")]
        private float northExtensionMeters = 40f;

        public float CharacterHeightMeters => characterHeightMeters;
        public float GroundFloorHeightMeters => groundFloorHeightMeters;
        public float UpperFloorHeightMeters => upperFloorHeightMeters;
        public float ShopFootprintMeters => shopFootprintMeters;
        public float HotelFootprintMeters => hotelFootprintMeters;
        public float HomeFootprintMeters => homeFootprintMeters;
        public float ParapetHeightMeters => parapetHeightMeters;
        public float DoorwayWidthMeters => doorwayWidthMeters;
        public float DoorwayHeightMeters => doorwayHeightMeters;
        public float NorthExtensionMeters => northExtensionMeters;
        public float ShopHeightMeters =>
            groundFloorHeightMeters + upperFloorHeightMeters + parapetHeightMeters;
    }
}
