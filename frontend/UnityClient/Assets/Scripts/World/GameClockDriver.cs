using System.Threading.Tasks;
using A305.Api;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 월드 장면에 있는 동안 게임 시간 신호를 보내고, 신호 사이에도 게임 시각을 흘린다(<see cref="GameClock"/>). 장면이 바뀌거나 로그아웃해 입구
    /// 장면으로 가면 함께 사라져 신호도 멈춘다. 브라우저 탭이 숨거나 창이 포커스를 잃으면(Run In Background 꺼짐) Update가 멈춰 신호가
    /// 끊기고, 돌아온 첫 프레임의 긴 시간도 플레이한 시간으로 세지 않는다(<see cref="GameClock.FlowFrame"/>).
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
            // 신호 사이에도 게임 시각을 흘려 NPC 일과가 끊기지 않게 한다. 서버가 없어도 흐른다.
            GameClock.Shared.FlowFrame(Time.unscaledDeltaTime);
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
