using System.Threading.Tasks;
using A305.Api;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 월드 장면에 있는 동안 게임 시간 신호를 보낸다(<see cref="GameClock"/>). 장면이 바뀌거나 로그아웃해 입구 장면으로 가면 함께 사라져 신호도
    /// 멈춘다. 브라우저 탭이 숨으면 Update가 멈춰 신호가 끊기고, 서버는 그 시간을 세지 않는다.
    /// </summary>
    public class GameClockDriver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip(
            "게임 시간 신호를 보낼 API 서버 설정. 비우면 보내지 않는다(서버 없이 씬만 확인할 때)."
        )]
        private ApiSettings apiSettings;

        private IGameClockApi api;

        private void Awake()
        {
            if (apiSettings != null)
            {
                api = new GameClockApi(apiSettings.CreateClient());
            }
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (api != null && GameClock.Shared.IsDue(now))
            {
                _ = BeatAsync(now);
            }
        }

        private async Task BeatAsync(double now)
        {
            GameClockBeatStatus status = await GameClock.Shared.TickAsync(api, now);
            if (status == GameClockBeatStatus.Failed)
            {
                ApiException error = GameClock.Shared.LastError;
                Debug.LogWarning(
                    $"게임 시간 신호를 보내지 못했습니다({error.Code}): {error.Message}",
                    this
                );
            }
        }
    }
}
