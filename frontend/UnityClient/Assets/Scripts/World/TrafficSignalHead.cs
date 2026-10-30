using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 신호등 하나의 렌즈를 켜고 끈다. 팔 끝 신호등은 본길을, 기둥에 붙은 작은 신호등은 가로길을 본다.
    /// </summary>
    public sealed class TrafficSignalHead : MonoBehaviour
    {
        [SerializeField]
        private Renderer mainRed;

        [SerializeField]
        private Renderer mainYellow;

        [SerializeField]
        private Renderer mainGreen;

        [SerializeField]
        private Renderer crossRed;

        [SerializeField]
        private Renderer crossYellow;

        [SerializeField]
        private Renderer crossGreen;

        [SerializeField]
        private Material redOn;

        [SerializeField]
        private Material yellowOn;

        [SerializeField]
        private Material greenOn;

        [SerializeField]
        private Material off;

        public void Show(SignalLight main, SignalLight cross)
        {
            Switch(mainRed, main == SignalLight.Red, redOn);
            Switch(mainYellow, main == SignalLight.Yellow, yellowOn);
            Switch(mainGreen, main == SignalLight.Green, greenOn);
            Switch(crossRed, cross == SignalLight.Red, redOn);
            Switch(crossYellow, cross == SignalLight.Yellow, yellowOn);
            Switch(crossGreen, cross == SignalLight.Green, greenOn);
        }

        private void Switch(Renderer lens, bool isOn, Material onMaterial)
        {
            if (lens != null)
            {
                lens.sharedMaterial = isOn ? onMaterial : off;
            }
        }
    }
}
