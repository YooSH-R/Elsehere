using System.Collections.Generic;
using System.Linq;
using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace A305.Npc
{
    /// <summary>문에 도착한 방문객만 로드된 시설 안에서 사물을 이용한다. 직원 역할은 맡지 않는다.</summary>
    public sealed class NpcFacilityVisitors : MonoBehaviour
    {
        private static NpcFacilityVisitors shared;
        private NpcOutingCatalog catalog;
        private NpcLifeData data;
        private readonly Dictionary<string, GameObject> guests =
            new Dictionary<string, GameObject>();

        public static void Ensure(NpcOutingCatalog source)
        {
            if (source == null || shared != null)
                return;
            shared = new GameObject("NpcFacilityVisitors").AddComponent<NpcFacilityVisitors>();
            DontDestroyOnLoad(shared.gameObject);
            shared.catalog = source;
            shared.data = NpcLifeData.Parse(source.LifeData.text);
        }

        public static void Recall(string code)
        {
            if (shared != null && shared.guests.TryGetValue(code, out GameObject guest))
            {
                if (guest != null)
                {
                    guest.SetActive(false);
                    Destroy(guest);
                }
                shared.guests.Remove(code);
            }
            NpcOutings.Shared.Release(code);
        }

        private void OnDestroy()
        {
            foreach (GameObject guest in guests.Values)
                if (guest != null)
                    Destroy(guest);
            if (shared == this)
                shared = null;
        }

        private void Update()
        {
            if (catalog == null)
                return;
            // 시연의 로비·주문 동선을 지나가는 방문객은 만들지 않는다. 각 매장 직원은 NpcPopulation이 유지한다.
            if (NpcPopulation.UsesShowcasePopulation)
            {
                foreach (GameObject guest in guests.Values)
                    if (guest != null)
                    {
                        guest.SetActive(false);
                        Destroy(guest);
                    }
                guests.Clear();
                return;
            }
            double now = GameClock.Shared.Minutes;
            foreach (var pair in guests.ToArray())
                if (NpcOutings.Shared.Find(pair.Key) == null || pair.Value == null)
                {
                    if (pair.Value != null)
                        Destroy(pair.Value);
                    guests.Remove(pair.Key);
                }
            bool townLoaded = SceneManager.GetSceneByName("TownScene").isLoaded;
            // 실내만 로드한 장면은 기존 출퇴근과 같이 그 시각의 위치를 복원한다.
            if (!townLoaded)
                foreach (NpcLifeEntry entry in data.Npcs)
                {
                    NpcDayPlan plan = NpcDayPlan.At(entry, now, false);
                    if (
                        plan.Phase != NpcDayPhase.Leisure
                        || plan.IsShortBreak
                        || entry.NpcId == NpcDuty.MissionNpcCode
                    )
                        continue;
                    NpcOuting trip = NpcOutings.Shared.Reserve(
                        entry.NpcId,
                        entry.Workplace,
                        now,
                        NpcOutings.UntilOutingEnds(plan, now),
                        catalog.Venues,
                        venue =>
                            NpcOutings.Shared.Welcomes(venue.Code) && NpcOutings.Suits(plan, venue)
                    );
                    trip?.Arrive(now);
                }
            foreach (NpcOuting trip in NpcOutings.Shared.Trips.ToArray())
            {
                if (trip.Venue.IsOutdoor || !trip.Arrived || trip.ReadyToLeave)
                    continue;
                Scene room = SceneManager.GetSceneByName(trip.Venue.Scene);
                guests.TryGetValue(trip.Npc, out GameObject guest);
                bool mustLeave =
                    now >= trip.Until
                    || !NpcOutings.Shared.Welcomes(trip.Venue.Code)
                    || trip.Npc == NpcDuty.MissionNpcCode;
                if (!room.isLoaded)
                {
                    if (guest != null)
                        Destroy(guest);
                    guests.Remove(trip.Npc);
                    if (mustLeave)
                        trip.ReadyToLeave = true;
                    continue;
                }
                if (guest == null && !mustLeave)
                {
                    guest = CreateGuest(trip, room);
                    if (guest != null)
                        guests[trip.Npc] = guest;
                }
                if (!mustLeave)
                    continue;
                if (guest == null)
                {
                    trip.ReadyToLeave = true;
                    continue;
                }
                var body = guest.GetComponent<Passerby>();
                if (
                    body.Phase == PasserbyPhase.Talking
                    || body.Phase == PasserbyPhase.Offering
                    || body.IsWaitingForQuest
                )
                    continue;
                guest.GetComponent<NpcRoutine>().enabled = false;
                var walker = guest.GetComponent<NpcTownWalker>();
                walker.enabled = true;
                walker.HasScheduleDeadline = false;
                walker.Destination = trip.Venue.Inside;
                if (
                    Vector3
                        .ProjectOnPlane(guest.transform.position - trip.Venue.Inside, Vector3.up)
                        .magnitude > .5f
                )
                    continue;
                Destroy(guest);
                guests.Remove(trip.Npc);
                trip.ReadyToLeave = true;
            }
        }

        private GameObject CreateGuest(NpcOuting trip, Scene room)
        {
            var player = FindFirstObjectByType<PlayerMover>();
            var dialogue = FindFirstObjectByType<DialogueController>();
            var offer = FindFirstObjectByType<PasserbyOfferView>();
            GameObject prefab = catalog.Visitors.FirstOrDefault(item =>
                item.GetComponent<NpcIdentity>().Code == trip.Npc
            );
            if (
                player == null
                || dialogue == null
                || offer == null
                || prefab == null
                || !NavMesh.SamplePosition(
                    trip.Venue.Inside,
                    out NavMeshHit hit,
                    2,
                    NavMesh.AllAreas
                )
            )
                return null;
            GameObject guest = Instantiate(prefab);
            guest.name = trip.Npc + "Visitor";
            SceneManager.MoveGameObjectToScene(guest, room);
            guest.GetComponent<NpcTownWalker>().enabled = false;
            var capsule = guest.GetComponent<CharacterController>();
            capsule.enabled = false;
            guest.transform.position = hit.position;
            capsule.enabled = true;
            var body = guest.GetComponent<Passerby>();
            body.ConnectToTown(player, dialogue, offer);
            guest.SetActive(true);
            if (NpcWalkingSpace.TryFindArrival(body, hit.position, out Vector3 arrival))
            {
                capsule.enabled = false;
                guest.transform.position = arrival;
                capsule.enabled = true;
            }
            else
            {
                Destroy(guest);
                return null;
            }
            var routine = guest.AddComponent<NpcRoutine>();
            routine.ConfigureVisit(
                data,
                data.FindNpc(trip.Npc),
                trip.Venue.Code,
                catalog.LifeAnimator
            );
            body.UseVisitRoutine(routine);
            return guest;
        }
    }
}
