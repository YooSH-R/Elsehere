using UnityEngine;

namespace A305.ObjectHover
{
    /// <summary>
    /// 단어 카드를 둘 자리의 기준. 좌표는 화면 픽셀이다.
    /// 카드는 포인터 가까이에 두되 가리킨 사물 영역과 캐릭터 영역을 덮지 않는다.
    /// </summary>
    public readonly struct CardAnchor
    {
        public CardAnchor(Vector2 pointer, Rect pointedArea, Rect? playerArea)
        {
            Pointer = pointer;
            PointedArea = pointedArea;
            PlayerArea = playerArea;
        }

        /// <summary>사물·캐릭터 영역 없이 포인터 한 점만 기준으로 삼는다.</summary>
        public static CardAnchor AtPointer(Vector2 pointer) =>
            new CardAnchor(pointer, new Rect(pointer, Vector2.zero), null);

        public Vector2 Pointer { get; }

        /// <summary>가리킨 사물이 화면에서 차지하는 영역. 바닥·벽처럼 화면을 넓게 차지하는 사물은 포인터 한 점이다.</summary>
        public Rect PointedArea { get; }

        /// <summary>캐릭터가 화면에서 차지하는 영역. 화면에 다 들어오지 않으면 없다.</summary>
        public Rect? PlayerArea { get; }
    }
}
