using System;
using System.Collections.Generic;
using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace A305.Npc
{
    /// <summary>근무지의 직원과 타운의 출퇴근 모습을 같은 게임 시각으로 관리한다.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class NpcPopulation : MonoBehaviour
    {
        [SerializeField]
        private TextAsset lifeData;

        [SerializeField]
        private Resident[] residents = Array.Empty<Resident>();

        [SerializeField]
        private Vector3 dormDoor;

        [SerializeField]
        private Vector3 park;

        [SerializeField]
        private Transform townCharacters;

        [Serializable]
        public sealed class Resident
        {
            public GameObject Worker;
            public GameObject TownPrefab;

            [NonSerialized]
            internal GameObject TownBody;
            public Vector3 WorkplaceDoor;
            public string InteriorScene;

            [NonSerialized]
            internal NpcLifeEntry Life;

            [NonSerialized]
            internal NpcDayPhase? LastPhase;

            [NonSerialized]
            internal Vector3[] Route;

            [NonSerialized]
            internal bool Warned;
        }

        private void Start()
        {
            NpcLifeData data = NpcLifeData.Parse(lifeData.text);
            foreach (Resident resident in residents)
            {
                if (resident.TownPrefab != null)
                {
                    resident.TownBody = Instantiate(resident.TownPrefab, townCharacters);
                    resident
                        .TownBody.GetComponent<Passerby>()
                        .ConnectToTown(
                            FindFirstObjectByType<PlayerMover>(),
                            FindFirstObjectByType<DialogueController>(),
                            FindFirstObjectByType<PasserbyOfferView>()
                        );
                }
                GameObject body = resident.Worker != null ? resident.Worker : resident.TownBody;
                resident.Life = data.FindNpc(body.GetComponent<NpcIdentity>().Code);
            }
            Update();
        }

        private void Update()
        {
            foreach (Resident resident in residents)
            {
                if (resident.Life == null)
                    continue;
                var workerBody =
                    resident.Worker != null ? resident.Worker.GetComponent<Passerby>() : null;
                bool needed =
                    resident.Life.NpcId == NpcDuty.MissionNpcCode
                    || (
                        workerBody != null
                        && (workerBody.IsGuidingTutorial || workerBody.IsWaitingForQuest)
                    );
                NpcDayPlan plan = NpcDayPlan.At(resident.Life, GameClock.Shared.Minutes, needed);
                if (resident.Worker != null && !IsTalking(resident.Worker))
                {
                    bool roomLoaded =
                        string.IsNullOrEmpty(resident.InteriorScene)
                        || SceneManager.GetSceneByName(resident.InteriorScene).isLoaded;
                    // 순찰차를 몰고 나간 경찰관은 돌아와 주차할 때까지 창구에 두지 않는다(PoliceCar).
                    resident.Worker.SetActive(
                        plan.Phase == NpcDayPhase.Working
                            && roomLoaded
                            && !PoliceCar.IsOutOnPatrol(resident.Life.NpcId)
                    );
                }
                if (resident.TownBody == null || IsTalking(resident.TownBody))
                    continue;
                bool appeared = !resident.TownBody.activeSelf;
                if (!plan.IsInTown)
                {
                    resident.TownBody.SetActive(false);
                    resident.LastPhase = null;
                    continue;
                }
                if (resident.LastPhase != plan.Phase)
                {
                    var endpoints = Endpoints(resident, plan);
                    resident.Route = RouteBetween(endpoints.From, endpoints.To);
                    resident.LastPhase = plan.Phase;
                }
                if (resident.Route == null)
                {
                    resident.TownBody.SetActive(false);
                    if (!resident.Warned)
                    {
                        Debug.LogWarning(
                            "NPC 출퇴근 경로를 찾지 못했습니다: " + resident.Life.NpcId,
                            this
                        );
                        resident.Warned = true;
                    }
                    // 다른 실내가 내려간 다음 프레임에는 타운 NavMesh만으로 다시 찾는다.
                    resident.LastPhase = null;
                    continue;
                }
                Vector3 target = PositionOnRoute(resident.Route, (float)plan.Progress);
                if (appeared)
                    resident.TownBody.transform.position = target;
                var walker = resident.TownBody.GetComponent<NpcTownWalker>();
                walker.Destination = target;
                walker.IsStaff = NpcDuty.IsStaff(resident.Life.Role);
                resident.TownBody.SetActive(true);
            }
        }

        private (Vector3 From, Vector3 To) Endpoints(Resident resident, NpcDayPlan plan)
        {
            // 같은 공원을 쓰더라도 서로 겹쳐 서지 않도록 NPC 코드에서 일정한 자리를 고른다.
            int slot = 0;
            foreach (char letter in resident.Life.NpcId)
                slot = (slot * 31 + letter) % 25;
            Vector3 leisure = park + new Vector3(slot % 5 - 2, 0, slot / 5 - 2);
            if (plan.IsShortBreak)
                leisure = resident.WorkplaceDoor + new Vector3(0, 0, 2);
            switch (plan.Phase)
            {
                case NpcDayPhase.GoingToWork:
                    return (plan.IsShortBreak ? leisure : dormDoor, resident.WorkplaceDoor);
                case NpcDayPhase.LeavingWork:
                    return (resident.WorkplaceDoor, leisure);
                case NpcDayPhase.GoingHome:
                    return (leisure, dormDoor);
                default:
                    // 공원 안에서도 한 자리에 서 있지 않고 짧은 산책을 한다.
                    return (leisure, leisure + Vector3.forward * 2);
            }
        }

        private static bool IsTalking(GameObject npc)
        {
            return npc.activeInHierarchy
                && npc.TryGetComponent(out Passerby body)
                && (body.Phase == PasserbyPhase.Talking || body.Phase == PasserbyPhase.Offering);
        }

        private static Vector3[] RouteBetween(Vector3 from, Vector3 to)
        {
            if (
                !NavMesh.SamplePosition(from, out NavMeshHit start, 2, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(to, out NavMeshHit end, 2, NavMesh.AllAreas)
            )
                return null;
            var path = new NavMeshPath();
            if (
                !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
            )
                return null;
            return path.corners.Length > 0 ? path.corners : new[] { start.position };
        }

        internal static Vector3 PositionOnRoute(IReadOnlyList<Vector3> route, float progress)
        {
            float total = 0;
            for (int i = 1; i < route.Count; i++)
                total += Vector3.Distance(route[i - 1], route[i]);
            float remaining = total * Mathf.Clamp01(progress);
            for (int i = 1; i < route.Count; i++)
            {
                float length = Vector3.Distance(route[i - 1], route[i]);
                if (remaining <= length)
                    return Vector3.Lerp(
                        route[i - 1],
                        route[i],
                        length > 0 ? remaining / length : 0
                    );
                remaining -= length;
            }
            return route[route.Count - 1];
        }
    }
}
