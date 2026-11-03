using UnityEngine;

namespace A305.Theme
{
    /// <summary>
    /// 유리 창(A305/UI/Glass)이 비추는 흐린 뒤 화면을 낮 밝기 쪽으로 얼마나 밝힐지. 씬을 밤처럼 어둡게 칠하는 쪽이
    /// 그만큼 올려 UI가 씬의 시각을 따라 어두워지지 않게 한다. 0이면 뒤 화면 그대로, 1이면 어두운 곳을 낮 밝기까지 밝힌다.
    /// </summary>
    public static class UiBackdropLift
    {
        private static readonly int AmountId = Shader.PropertyToID("_A305UiBackdropLift");

        public static float Amount => Shader.GetGlobalFloat(AmountId);

        public static void Show(float amount) =>
            Shader.SetGlobalFloat(AmountId, Mathf.Clamp01(amount));
    }
}
