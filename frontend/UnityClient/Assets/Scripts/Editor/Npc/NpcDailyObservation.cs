using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using A305.Dialogue;
using A305.Npc;
using A305.Player;
using A305.Tutorial;
using A305.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>보행 속도·게임 시각·신호의 비율을 유지해 연속 이틀을 관찰한다. 첫날은 준비, 둘째 날만 집계한다.</summary>
    [InitializeOnLoad]
    public static class NpcDailyObservation
    {
        private const string RunningKey = "A305.NpcDailyObservation";

        static NpcDailyObservation() => EditorApplication.playModeStateChanged += Changed;

        [MenuItem("Tools/A305/Npc/Observe Daily Commutes")]
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene
            );
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (
                state != PlayModeStateChange.EnteredPlayMode
                || !SessionState.GetBool(RunningKey, false)
            )
                return;
            SessionState.SetBool(RunningKey, false);
            new GameObject("NpcDailyObservation").AddComponent<NpcDailyObservationRunner>();
        }
    }

    public sealed class NpcDailyObservationRunner : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Commute> commutes = new List<Commute>();
        private readonly List<Visit> visits = new List<Visit>();
        private readonly HashSet<string> visited = new HashSet<string>();
        private int peakReserved;

        [Serializable]
        public sealed class Visit
        {
            public string npc;
            public string venue;
            public bool signals;
            public bool outdoor;
            public double minute;
        }

        [Serializable]
        public sealed class Commute
        {
            public string npc;
            public bool signals;
            public double scheduledMinute;
            public double delayMinutes;
            public double signalWaitSeconds;
            public bool arrived;
            public bool refusedConversation;
            public Vector3 finalPosition;
        }

        [Serializable]
        private sealed class Report
        {
            public string method =
                "운영 TownScene·근무표·시각 배율·물리 설정 유지, 보행 프레임 0.1초, 준비 하루+측정 하루, UI·시각적 애니메이션 제외, 정상 신호와 보행 신호 제약 제거 비교";
            public List<Commute> commutes;
            public List<Visit> visits;
            public int peakReservedPerVenue;
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Time.captureDeltaTime = .1f;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            foreach (
                bool signals in Environment.GetEnvironmentVariable("NPC_OBSERVATION_SIGNALS")
                == "off"
                    ? new[] { false }
                    : new[] { true, false }
            )
            {
                UnityEngine.Random.InitState(681);
                TutorialState.Clear();
                TutorialState.MarkFinishedOnServer();
                NpcDuty.MissionNpcCode = null;
                typeof(GameClock)
                    .GetMethod("ResetShared", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);
                GameClock
                    .Shared.TickAsync(
                        new ObservationClock(),
                        Time.realtimeSinceStartupAsDouble + 1e6
                    )
                    .GetAwaiter()
                    .GetResult();
                SceneManager.sceneLoaded += DisconnectApis;
                yield return SceneManager.LoadSceneAsync("TownScene");
                SceneManager.sceneLoaded -= DisconnectApis;
                yield return null;
                Object
                    .FindFirstObjectByType<PlayerMover>()
                    .Teleport(new Vector3(0, 100, 0), Quaternion.identity);
                var walkers = Object.FindObjectsByType<NpcTownWalker>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );
                foreach (
                    TrafficLights lights in Object.FindObjectsByType<TrafficLights>(
                        FindObjectsSortMode.None
                    )
                )
                    lights.ObserveWithoutPedestrianWait = !signals;
                foreach (
                    Animator model in Object.FindObjectsByType<Animator>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    )
                )
                    model.enabled = false;
                foreach (
                    Canvas canvas in Object.FindObjectsByType<Canvas>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    )
                )
                    canvas.enabled = false;
                var data = NpcLifeData.Parse(
                    File.ReadAllText(Path.Combine(Application.dataPath, "Data/Npc/NpcLife.json"))
                );
                var previous = new Dictionary<string, NpcDayPhase>();
                var waits = new Dictionary<string, double>();
                var current = new Dictionary<string, Commute>();
                int lastHour = -1;
                int stalledFrames = 0;
                while (GameClock.Shared.Minutes < 2880)
                {
                    double beforeFlow = GameClock.Shared.Minutes;
                    GameClock.Shared.Flow(Time.deltaTime);
                    stalledFrames = GameClock.Shared.Minutes <= beforeFlow ? stalledFrames + 1 : 0;
                    if (stalledFrames > 300)
                        throw new InvalidOperationException("진단 중 게임 시각이 멈췄습니다.");
                    yield return null;
                    double now = GameClock.Shared.Minutes;
                    int hour = (int)(now / 60);
                    if (hour != lastHour && hour % 4 == 0)
                    {
                        lastHour = hour;
                        Save();
                        Debug.Log($"일과 관찰: 신호 {signals}, 게임 {now:0}분");
                    }
                    foreach (NpcTownWalker walker in walkers)
                    {
                        string code = walker.GetComponent<NpcIdentity>().Code;
                        NpcLifeEntry entry = data.FindNpc(code);
                        if (entry == null || !NpcDuty.IsStaff(entry.Role))
                            continue;
                        NpcDayPhase phase = NpcDayPlan.At(entry, now, false).Phase;
                        previous.TryGetValue(code, out NpcDayPhase before);
                        waits.TryGetValue(code, out double wait);
                        if (phase == NpcDayPhase.GoingToWork && before != phase)
                            wait = 0;
                        if (
                            walker.gameObject.activeInHierarchy
                            && walker.GetComponent<Passerby>().IsWaitingAtSignal
                        )
                            wait += Time.deltaTime;
                        waits[code] = wait;
                        if (now >= 1440 && phase == NpcDayPhase.Working && before != phase)
                        {
                            var trip = new Commute
                            {
                                npc = code,
                                signals = signals,
                                scheduledMinute = Math.Round(now * 10) / 10,
                            };
                            commutes.Add(trip);
                            current[code] = trip;
                        }
                        if (current.TryGetValue(code, out Commute active) && !active.arrived)
                        {
                            active.delayMinutes = now - active.scheduledMinute;
                            active.signalWaitSeconds = wait;
                            active.finalPosition = walker.transform.position;
                            active.refusedConversation |= walker.DeclinesConversation;
                            active.arrived = !walker.gameObject.activeInHierarchy;
                        }
                        if (phase != NpcDayPhase.Working)
                            current.Remove(code);
                        previous[code] = phase;
                    }
                    if (now >= 1440)
                    {
                        var activeTrips = NpcOutings
                            .Shared.Trips.Where(trip =>
                                trip.Until > now || (trip.Arrived && !trip.ReadyToLeave)
                            )
                            .ToArray();
                        foreach (var group in activeTrips.GroupBy(trip => trip.Venue.Code))
                            peakReserved = Math.Max(peakReserved, group.Count());
                        foreach (NpcOuting trip in activeTrips)
                        {
                            var walker = walkers.FirstOrDefault(item =>
                                item.GetComponent<NpcIdentity>().Code == trip.Npc
                            );
                            bool arrived =
                                trip.Arrived
                                || (
                                    trip.Venue.IsOutdoor
                                    && walker != null
                                    && walker.gameObject.activeSelf
                                    && Vector3
                                        .ProjectOnPlane(
                                            walker.transform.position - trip.Venue.Entrance,
                                            Vector3.up
                                        )
                                        .magnitude < 1
                                );
                            string key = $"{signals}:{trip.Npc}:{trip.Venue.Code}:{trip.Until:0}";
                            if (arrived && visited.Add(key))
                                visits.Add(
                                    new Visit
                                    {
                                        npc = trip.Npc,
                                        venue = trip.Venue.Code,
                                        outdoor = trip.Venue.IsOutdoor,
                                        signals = signals,
                                        minute = now,
                                    }
                                );
                        }
                    }
                }
                Save();
                Debug.Log(
                    $"NPC 일과 관찰 완료: 신호 {signals}, 출근 {commutes.Count(trip => trip.signals == signals)}건"
                );
            }
            Time.captureDeltaTime = 0;
            Save();
            EditorApplication.Exit(0);
        }

        private void Save()
        {
            string output =
                Environment.GetEnvironmentVariable("NPC_DAILY_OBSERVATION_OUTPUT")
                ?? Path.GetFullPath("../../artifacts/npc-681/baseline.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(
                output,
                JsonUtility.ToJson(
                    new Report
                    {
                        commutes = commutes,
                        visits = visits,
                        peakReservedPerVenue = peakReserved,
                    },
                    true
                )
            );
        }

        private static void DisconnectApis(Scene scene, LoadSceneMode mode)
        {
            foreach (
                GameClockDriver clock in Object.FindObjectsByType<GameClockDriver>(
                    FindObjectsSortMode.None
                )
            )
                clock.enabled = false;
            foreach (
                MonoBehaviour item in Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsSortMode.None
                )
            )
                if (
                    item.GetType().Name == "LearningController"
                    || item.GetType().Name == "ObjectHoverController"
                    || item.GetType().Name == "WeatherPresenter"
                )
                    item.enabled = false;
            var dialogue = Object.FindFirstObjectByType<DialogueController>();
            foreach (string name in new[] { "conversationApi", "followUpApi" })
                typeof(DialogueController).GetField(name, PrivateInstance).SetValue(dialogue, null);
            var arrival = Object.FindFirstObjectByType<SpaceArrival>();
            if (arrival != null)
                typeof(SpaceArrival)
                    .GetField("apiSettings", PrivateInstance)
                    .SetValue(arrival, null);
        }

        private sealed class ObservationClock : IGameClockApi
        {
            public Task<GameClockTime> BeatAsync(
                bool interactionPaused = false,
                bool interactionWasPaused = false,
                long? activeSeconds = null
            ) =>
                Task.FromResult(
                    new GameClockTime
                    {
                        Day = 1,
                        MinuteOfDay = 0,
                        Paused = false,
                    }
                );
        }
    }
}
