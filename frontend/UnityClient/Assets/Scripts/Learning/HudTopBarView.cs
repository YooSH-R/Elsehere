using A305.World;
using UnityEngine;

namespace A305.Learning
{
    /// <summary>휴대폰 메뉴의 열림 여부와 관계없이 메인 화면 휴대폰 자리를 스피커 왼쪽에 유지한다.</summary>
    public sealed class HudTopBarView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform phoneBar;

        public void Configure(RectTransform bar) => phoneBar = bar;

        private void LateUpdate()
        {
            if (phoneBar == null)
                return;
            var screen = (RectTransform)transform;
            Vector3 corner = screen.TransformPoint(
                new Vector3(
                    screen.rect.xMax - HudTopBarLayout.PhoneRight,
                    screen.rect.yMax - HudTopBarLayout.Margin,
                    0f
                )
            );
            phoneBar.position = corner;
        }
    }
}
