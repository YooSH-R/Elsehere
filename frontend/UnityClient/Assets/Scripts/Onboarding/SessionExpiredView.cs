using A305.Api;
using A305.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.Onboarding
{
    /// <summary>
    /// 장소 화면에서 서버가 로그인이 풀렸다고 알리면(<see cref="ApiSession.Expired"/>) 다른 창 위에 안내를 띄우고 이동을 막는다.
    /// 화면마다 따로 실패 문구를 보이는 대신 한 번에 알리고, [처음 화면으로]로 입구 씬의 로그인 화면에 돌아간다.
    /// 단어장 HUD 프리팹에 붙어 있어 장소 씬마다 배치하지 않는다.
    /// </summary>
    public sealed class SessionExpiredView : MonoBehaviour
    {
        public const string Title = "접속이 끊겼어요";

        public const string Body =
            "오래 쉬었거나 서버 점검으로 로그인이 풀렸어요. 처음 화면에서 다시 들어와 주세요. 게스트는 새 게스트로 다시 시작해요.";

        public const string ButtonLabel = "처음 화면으로";

        [SerializeField]
        [Tooltip("다른 화면보다 위에 그리는 캔버스가 붙은 덮개")]
        private GameObject overlay;

        [SerializeField]
        private Button entryButton;

        [SerializeField]
        private string entrySceneName = "EntryScene";

        private void Awake()
        {
            entryButton.onClick.AddListener(() => SceneManager.LoadScene(entrySceneName));
            overlay.SetActive(false);
        }

        private void OnEnable()
        {
            ApiSession.Expired += Show;
        }

        private void OnDisable()
        {
            ApiSession.Expired -= Show;
        }

        // 여러 화면이 거의 동시에 같은 응답을 받아도 한 번만 띄운다.
        private void Show()
        {
            if (overlay.activeSelf)
            {
                return;
            }

            overlay.SetActive(true);
            var player = FindFirstObjectByType<PlayerMover>();
            if (player != null)
            {
                player.SetMovementLocked(true);
            }
        }
    }
}
