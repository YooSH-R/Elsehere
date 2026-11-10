using UnityEngine;

namespace A305.Onboarding
{
    /// <summary>
    /// 입구 화면 뒤 배경. 접속 확인 중에는 로그인 화면과 같은 마을·왼쪽 크림색 그라데이션을 보여 주고,
    /// 가운데에 창이 뜨면 시안(e2~e5)처럼 그라데이션 없이 흐린 마을만 보여 준다.
    /// </summary>
    public class EntryBackdropView : MonoBehaviour
    {
        [SerializeField]
        private GameObject loginTown;

        [SerializeField]
        private GameObject creamShade;

        [SerializeField]
        private GameObject windowTown;

        public void Show(bool behindWindow)
        {
            loginTown.SetActive(!behindWindow);
            creamShade.SetActive(!behindWindow);
            windowTown.SetActive(behindWindow);
        }

        /// <summary>
        /// 그 화면이 가운데 창인지. 로그인 창은 자기 배경을 깔고, 튜토리얼·장소는 장면을 바꾼다. 그 밖의 입구 창은 모두 화면 가운데에 뜬다.
        /// </summary>
        public static bool IsBehindWindow(EntryScreen screen) =>
            screen is not (EntryScreen.Login or EntryScreen.Tutorial or EntryScreen.World);
    }
}
