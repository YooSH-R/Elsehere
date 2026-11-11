using System.Collections.Generic;
using System.Linq;
using A305.Player;
using A305.Weather;
using UnityEngine;

namespace A305.World
{
    /// <summary>
    /// 장소 씬마다 하나 있어 영업시간을 게임에 알리고(<see cref="OpeningHours"/>), 닫힌 장소의 문 앞 거절 안내를 띄운다.
    /// 플레이어가 머무는 장소의 영업이 끝나면 게임 시간으로 남은 시간을 세어 보이고(<see cref="ClosingCountdown"/>), 다 지나면 마을로 내보낸다.
    /// 장소 씬(마을 매장 포함)은 그 씬의 마을 출입구로 내보낸다. 튜토리얼 잠금과 상관없이 내보내지만,
    /// 대화 중처럼 플레이어가 움직일 수 없는 동안에는 기다린다.
    /// </summary>
    public sealed class OpeningHoursController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip(
            "장소별 영업시간(Data/World/PlaceHours.json). ai/tools/npc_life_export.py가 만든다."
        )]
        private TextAsset placeHours;

        [SerializeField]
        private HudNoticeView notice;

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        [Tooltip(
            "영업이 끝나면 나갈 마을 출입구. 마을처럼 이 씬에서 마을로 나가는 문이 없으면 비운다."
        )]
        private SpaceDoorway exitToTown;

        private readonly ClosingCountdown countdown = new ClosingCountdown();
        private string watchedPlaceCode;
        private bool isShowingCountdown;

        private void Awake()
        {
            if (placeHours == null || notice == null || player == null)
            {
                Debug.LogError(
                    "OpeningHoursController의 placeHours·notice·player가 연결되지 않았습니다.",
                    this
                );
                enabled = false;
                return;
            }

            OpeningHours.Use(PlaceHours.FromJson(placeHours.text));
        }

        private WeatherPresenter weather;
        private readonly Dictionary<Transform, string> buildings =
            new Dictionary<Transform, string>();

        private void Start()
        {
            weather = gameObject
                .scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WeatherPresenter>())
                .FirstOrDefault();
            if (weather == null)
                return;
            foreach (Transform building in weather.LitBuildings)
            {
                if (building == null)
                    continue;
                SpaceDoorway door = building.GetComponentInChildren<SpaceDoorway>(true);
                string code = door != null ? PlaceScenes.CodeOf(door.DestinationScene) : null;
                if (code != null)
                    buildings[building] = code;
            }
        }

        private void LateUpdate()
        {
            foreach (var entry in buildings)
                weather.SetBuildingOpen(entry.Key, OpeningHours.IsOpenNow(entry.Value));
        }

        private void OnEnable()
        {
            SpaceTravel.ClosedPlaceRefused += ShowRefusal;
        }

        private void OnDisable()
        {
            SpaceTravel.ClosedPlaceRefused -= ShowRefusal;
        }

        private void Update()
        {
            // 옮기는 동안에는 남은 시간을 띄우지 않는다. 마감은 닫힌 것을 처음 본 게임 시각부터 세므로 건너뛰어도 늦어지지 않는다.
            if (SpaceTravel.IsTraveling)
            {
                HideCountdown();
                return;
            }

            string placeCode = PlaceScenes.CodeOf(gameObject.scene.name);
            if (placeCode != watchedPlaceCode)
            {
                countdown.Reset();
                watchedPlaceCode = placeCode;
            }

            countdown.Update(OpeningHours.IsOpenNow(placeCode), GameClock.Shared.Minutes);
            if (countdown.Phase == ClosingPhase.Open)
            {
                HideCountdown();
                return;
            }

            ShowCountdown(OpeningHoursText.ClosingNotice(countdown.MinutesLeft));
            if (countdown.Phase == ClosingPhase.TimeToLeave && TryLeave())
            {
                HideCountdown();
            }
        }

        private bool TryLeave()
        {
            if (exitToTown == null || player.IsMovementLocked)
            {
                return false;
            }

            if (!SpaceTravel.TryLeaveClosedPlace(exitToTown))
            {
                return false;
            }

            // 다음 공간을 불러오는 동안 걸어 다니지 않게 한다.
            player.SetMovementLocked(true);
            return true;
        }

        private void ShowRefusal(string message)
        {
            notice.ShowBriefly(message);
        }

        private void ShowCountdown(string message)
        {
            if (!notice.IsShowing || notice.Message != message)
            {
                notice.Show(message);
            }

            isShowingCountdown = true;
        }

        private void HideCountdown()
        {
            if (!isShowingCountdown)
            {
                return;
            }

            notice.Hide();
            isShowingCountdown = false;
        }
    }
}
