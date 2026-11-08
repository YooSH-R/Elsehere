#if UNITY_EDITOR
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
using A305.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.Npc
{
    [AddComponentMenu("")]
    public sealed class NpcDailyObservationRunner : MonoBehaviour
    {
        public Action PrepareScenario;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Commute> commutes = new List<Commute>();
        private readonly List<Visit> visits = new List<Visit>();
        private readonly HashSet<NpcOuting> visited = new HashSet<NpcOuting>();
        private int peakReserved;
        private int warmupDays;
        private readonly List<string> stalls = new List<string>();

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
            public double departureLeadMinutes;
        }

        [Serializable]
        private sealed class Report
        {
            public string method =
                "운영 TownScene·근무표·시각 배율·물리 설정 유지, 30fps 보행 프레임, UI·시각적 애니메이션 제외, 정상 신호와 보행 신호 제약 제거 비교";
            public List<Commute> commutes;
            public List<Visit> visits;
            public int peakReservedPerVenue;
            public int warmupDays;
            public List<string> stalls;
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Time.captureDeltaTime = 1f / 30f;
            if (
                !int.TryParse(
                    Environment.GetEnvironmentVariable("NPC_OBSERVATION_WARMUP_DAYS"),
                    out warmupDays
                )
            )
                warmupDays = 1;
            warmupDays = Math.Max(0, warmupDays);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            foreach (
                bool signals in Environment.GetEnvironmentVariable("NPC_OBSERVATION_SIGNALS")
                == "off"
                    ? new[] { false }
                : Environment.GetEnvironmentVariable("NPC_OBSERVATION_SIGNALS") == "on"
                    ? new[] { true }
                : new[] { true, false }
            )
            {
                UnityEngine.Random.InitState(681);
                PrepareScenario();
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
                var population = Object.FindFirstObjectByType<NpcPopulation>();
                var walkersByCode = walkers.ToDictionary(walker =>
                    walker.GetComponent<NpcIdentity>().Code
                );
                var bodies = walkersByCode.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.GetComponent<Passerby>()
                );
                bool recording =
                    signals
                    && !string.IsNullOrEmpty(
                        Environment.GetEnvironmentVariable("NPC_DAILY_REVIEW_OUTPUT")
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
                {
                    model.enabled = recording;
                    if (recording)
                        model.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
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
                var entries = data.Npcs.ToDictionary(entry => entry.NpcId);
                var previous = new Dictionary<string, NpcDayPhase>();
                var waits = new Dictionary<string, double>();
                var current = new Dictionary<string, Commute>();
                var wasCommuting = new Dictionary<string, bool>();
                using var review = new NpcDailyReviewCapture(
                    signals ? Environment.GetEnvironmentVariable("NPC_DAILY_REVIEW_OUTPUT") : null
                );
                int lastHour = -1;
                int stalledFrames = 0;
                var anchors = new Dictionary<string, Vector3>();
                var stopped = new Dictionary<string, float>();
                var reported = new Dictionary<string, Vector3>();
                double endMinute = double.TryParse(
                    Environment.GetEnvironmentVariable("NPC_OBSERVATION_END_MINUTE"),
                    out double limit
                )
                    ? limit
                    : (warmupDays + 1) * 1440;
                while (GameClock.Shared.Minutes < endMinute)
                {
                    double beforeFlow = GameClock.Shared.Minutes;
                    GameClock.Shared.Flow(Time.deltaTime);
                    stalledFrames = GameClock.Shared.Minutes <= beforeFlow ? stalledFrames + 1 : 0;
                    if (stalledFrames > 300)
                        throw new InvalidOperationException("진단 중 게임 시각이 멈췄습니다.");
                    yield return null;
                    double now = GameClock.Shared.Minutes;
                    review.Step(now, walkers);
                    int hour = (int)(now / 60);
                    if (hour != lastHour && hour % 4 == 0)
                    {
                        lastHour = hour;
                        Save();
                        Debug.Log($"일과 관찰: 신호 {signals}, 게임 {now:0}분");
                    }
                    foreach (var pair in walkersByCode)
                    {
                        string code = pair.Key;
                        NpcTownWalker walker = pair.Value;
                        if (
                            walker.isActiveAndEnabled
                            && walker.HasScheduleDeadline
                            && !bodies[code].IsWaitingAtSignal
                        )
                        {
                            anchors.TryGetValue(code, out Vector3 anchor);
                            stopped.TryGetValue(code, out float seconds);
                            if (Vector3.Distance(anchor, walker.transform.position) > .15f)
                            {
                                anchors[code] = walker.transform.position;
                                seconds = 0;
                            }
                            else
                                seconds += Time.deltaTime;
                            stopped[code] = seconds;
                            if (
                                seconds > 5
                                && (
                                    !reported.TryGetValue(code, out Vector3 last)
                                    || Vector3.Distance(last, anchor) > 1
                                )
                            )
                            {
                                reported[code] = anchor;
                                stalls.Add(DescribeStall(walker, walkers, now, signals));
                            }
                        }
                        else
                            stopped[code] = 0;
                        entries.TryGetValue(code, out NpcLifeEntry entry);
                        if (entry == null || !NpcDuty.IsStaff(entry.Role))
                            continue;
                        NpcDayPhase phase = NpcDayPlan.At(entry, now, false).Phase;
                        previous.TryGetValue(code, out NpcDayPhase before);
                        waits.TryGetValue(code, out double wait);
                        bool commuting =
                            walker.gameObject.activeSelf
                            && walker.SchedulePhase == NpcDayPhase.GoingToWork;
                        wasCommuting.TryGetValue(code, out bool previouslyCommuting);
                        if (commuting && !previouslyCommuting)
                            wait = 0;
                        wasCommuting[code] = commuting;
                        if (walker.gameObject.activeInHierarchy && bodies[code].IsWaitingAtSignal)
                            wait += Time.deltaTime;
                        waits[code] = wait;
                        if (
                            now >= warmupDays * 1440
                            && phase == NpcDayPhase.Working
                            && before != phase
                        )
                        {
                            var trip = new Commute
                            {
                                npc = code,
                                signals = signals,
                                scheduledMinute = Math.Round(now * 10) / 10,
                                departureLeadMinutes = population.CommuteMinutesFor(code),
                            };
                            commutes.Add(trip);
                            current[code] = trip;
                        }
                        if (current.TryGetValue(code, out Commute active) && !active.arrived)
                        {
                            active.delayMinutes = now - active.scheduledMinute;
                            active.signalWaitSeconds = wait;
                            active.finalPosition = walker.transform.position;
                            active.refusedConversation =
                                active.refusedConversation || walker.DeclinesConversation;
                            active.arrived = !walker.gameObject.activeInHierarchy;
                        }
                        if (phase != NpcDayPhase.Working)
                            current.Remove(code);
                        previous[code] = phase;
                    }
                    if (now >= warmupDays * 1440)
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
                            if (visited.Contains(trip))
                                continue;
                            walkersByCode.TryGetValue(trip.Npc, out NpcTownWalker walker);
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
                            if (arrived && visited.Add(trip))
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
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
            else
                EditorApplication.isPlaying = false;
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
                        warmupDays = warmupDays,
                        stalls = stalls,
                    },
                    true
                )
            );
        }

        public static string DescribeStall(
            NpcTownWalker walker,
            NpcTownWalker[] walkers,
            double now,
            bool signals
        )
        {
            Vector3 position = walker.transform.position;
            var route = new NavMeshPath();
            NavMesh.CalculatePath(position, walker.Destination, NpcTownWalker.WalkingAreas, route);
            var passing =
                typeof(NpcTownWalker).GetField("passing", PrivateInstance).GetValue(walker)
                as Passerby;
            var crossing =
                typeof(Passerby)
                    .GetField("crossing", PrivateInstance)
                    .GetValue(walker.GetComponent<Passerby>()) as NpcCrosswalk;
            string others = string.Join(
                ";",
                walkers
                    .Where(other =>
                        other != walker
                        && other.isActiveAndEnabled
                        && Vector3.Distance(position, other.transform.position) < 3
                    )
                    .Select(other =>
                        $"{other.GetComponent<NpcIdentity>().Code}@{other.transform.position:F2}->{other.Destination:F2}"
                    )
            );
            string solids = string.Join(
                ";",
                Physics
                    .OverlapSphere(position + Vector3.up * .6f, .9f)
                    .Where(collider =>
                        !collider.isTrigger && collider.GetComponentInParent<Passerby>() == null
                    )
                    .Select(collider => $"{collider.name} bounds={collider.bounds}")
            );
            var body = walker.GetComponent<Passerby>();
            string crossingState = string.Join(
                ",",
                crossing
                    .GetType()
                    .GetFields(PrivateInstance)
                    .Select(field => $"{field.Name}={field.GetValue(crossing)}")
            );
            return $"{now:F1} signals={signals} {walker.GetComponent<NpcIdentity>().Code} {walker.SchedulePhase} pos={position:F3} destination={walker.Destination:F3} path={route.status}:{string.Join(";", route.corners.Select(point => point.ToString("F2")))} crossing={crossing?.IsActive} crossingState=[{crossingState}] motion=[age={Time.frameCount - body.LastWalkFrame} requested={body.LastWalkInput:F4} detour={body.LastWalkDetour:F4} final={body.LastWalkDirection:F4} step={body.LastWalkStep:F5} moved={body.LastWalkMoved:F5}] passing={passing?.GetComponent<NpcIdentity>().Code} others=[{others}] solids=[{solids}]";
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
#endif
