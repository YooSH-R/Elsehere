using System.Collections.Generic;
using A305.Dialogue;
using A305.Player;
using A305.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace A305.Wayfinding
{
    /// <summary>
    /// 목적지(<see cref="GuideDestination"/>) 건물의 출입문 앞을 빛내고, 플레이어 발밑에서 그 출입문까지 도로 위에 빛나는 길을 깐다.
    /// 출입문은 별도 씬으로 이어지는 <see cref="SpaceDoorway"/> 또는 마을 매장으로 들어가는 <see cref="ShopDoorway"/>의 문 앞이다.
    /// 길은 걸을 바닥(NavMesh)에서 차도에 큰 비용을 매겨 찾으므로, 차도를 건너야 하면 횡단보도로 돌아간다(<see cref="GuideWalkAreas"/>).
    /// 무엇을 보일지는 <see cref="GuideVisibility"/>가, 언제 길을 다시 찾고 얼마나 깔지는 <see cref="GuideRouteClock"/>이 정한다.
    /// </summary>
    public sealed class DestinationGuide : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapScaleOffsetId = Shader.PropertyToID("_BaseMap_ST");

        // 플레이어나 출입문 앞이 걸을 바닥에서 이만큼 떨어져 있어도 가장 가까운 바닥에서 길을 찾는다.
        private const float SnapMeters = 1.5f;

        // 바닥 높이는 점 위 이 높이에서 아래로 쏘아 찾는다. 보도 턱(0.1m)보다 높고 벤치 앉는 면보다 낮다.
        private const float GroundProbeMeters = .4f;
        private const float GroundProbeDepthMeters = 1f;

        // 출입문에서 이 거리 안에서는 빛기둥이 사라지고, 이 거리 밖에서는 다 보인다.
        private const float BeamFadeStartMeters = 3f;
        private const float BeamFadeEndMeters = 8f;

        [SerializeField]
        private DestinationGuideSettings settings;

        [SerializeField]
        private PlayerMover player;

        [SerializeField]
        [Tooltip("대화 중에는 안내를 숨긴다")]
        private DialogueController dialogue;

        [SerializeField]
        [Tooltip("도로 위 길. 땅에 눕도록 Transform의 Z가 아래를 본다")]
        private LineRenderer route;

        [SerializeField]
        [Tooltip("출입문 앞으로 옮기는 빛 묶음")]
        private Transform entranceGlow;

        [SerializeField]
        [Tooltip("출입문 앞 바닥의 빛 원")]
        private Renderer pool;

        [SerializeField]
        [Tooltip("멀리서도 보이는 빛기둥. 카메라 쪽으로 돌린다")]
        private Renderer beam;

        private readonly List<Vector3> laid = new List<Vector3>();
        private readonly List<Vector3> shown = new List<Vector3>();
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private GuideRouteClock clock;
        private NavMeshPath path;
        private NavMeshQueryFilter filter;
        private MaterialPropertyBlock paint;
        private Vector3 poolScale;
        private float routeLength;
        private string placeCode;
        private Transform entrance;
        private Camera view;

        /// <summary>
        /// 이 씬에서 목적지로 들어가는 문 앞. 별도 씬과 마을 매장 출입문을 모두 찾으며, 연결된 문이 없으면 null이다.
        /// </summary>
        public static Transform FindEntrance(Scene scene, string placeCode)
        {
            if (string.IsNullOrEmpty(placeCode))
                return null;
            string placeScene = PlaceScenes.SceneOf(placeCode);
            Transform shopEntrance = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SpaceDoorway doorway in root.GetComponentsInChildren<SpaceDoorway>())
                {
                    if (placeScene != null && doorway.DestinationScene == placeScene)
                        return doorway.ArrivalPoint;
                }
                foreach (ShopDoorway doorway in root.GetComponentsInChildren<ShopDoorway>())
                {
                    if (doorway.Room == null || doorway.Room.PlaceCode != placeCode)
                        continue;
                    Transform approach = doorway.ApproachPoint;
                    // 한 건물의 2층 매장과 장소 코드를 공유해도 퀘스트 직원이 있는 아래층 문으로 안내한다.
                    if (shopEntrance == null || approach.position.y < shopEntrance.position.y)
                        shopEntrance = approach;
                }
            }

            return shopEntrance;
        }

        private void Awake()
        {
            if (
                settings == null
                || player == null
                || route == null
                || entranceGlow == null
                || pool == null
                || beam == null
            )
            {
                Debug.LogError("길 안내 설정·플레이어·길·출입문 빛 참조가 필요합니다.", this);
                enabled = false;
                return;
            }

            if (NavMesh.GetAreaFromName(GuideWalkAreas.Road) < 0)
                Debug.LogWarning("Road 구역이 없어 길 안내가 차도를 피하지 않습니다.", this);
            clock = new GuideRouteClock(
                settings.ReplanMeters,
                settings.ReplanSeconds,
                settings.RevealSeconds
            );
            // NavMeshPath는 컴포넌트 필드 초기값에서 만들 수 없어 여기서 만든다.
            path = new NavMeshPath();
            filter = GuideWalkAreas.Filter(settings.RoadCrossingCost);
            paint = new MaterialPropertyBlock();
            poolScale = pool.transform.localScale;
            route.widthMultiplier = settings.RouteWidthMeters;
            route.enabled = false;
            entranceGlow.gameObject.SetActive(false);
        }

        // 플레이어가 이번 프레임에 움직인 뒤 길을 잇는다.
        private void LateUpdate()
        {
            // 매장 실내는 마을에 겹쳐 불러와 활성 씬이 바뀌지 않으므로 입장 상태를 직접 확인한다.
            GuideDestination.Shared.EnterPlace(
                ShopInterior.Occupied != null
                    ? ShopInterior.Occupied.PlaceCode
                    : PlaceScenes.CodeOf(gameObject.scene.name)
            );
            string destination = GuideDestination.Shared.PlaceCode;
            if (destination != placeCode)
            {
                placeCode = destination;
                entrance = FindEntrance(gameObject.scene, destination);
            }

            Vector3 feet = player.transform.position;
            bool hasEntrance = entrance != null;
            if (hasEntrance && FlatDistance(feet, entrance.position) <= settings.ArrivedMeters)
                GuideDestination.Shared.Arrive();
            GuideVisibility visibility = GuideVisibility.Decide(
                hasEntrance,
                GuideDestination.Shared.Arrived,
                ShopInterior.Occupied != null
                    || SpaceTravel.IsTraveling
                    || (dialogue != null && dialogue.IsTalking),
                GuideRoutePreference.ShowsRoute
            );
            ShowEntrance(visibility.ShowsEntrance, feet);
            ShowRoute(visibility.ShowsRoute, feet);
        }

        private void ShowEntrance(bool shows, Vector3 feet)
        {
            entranceGlow.gameObject.SetActive(shows);
            if (!shows)
                return;
            entranceGlow.SetPositionAndRotation(OnGround(entrance.position), entrance.rotation);
            float pulse = Mathf.Sin(Time.time * settings.PulsesPerSecond * 2f * Mathf.PI);
            pool.transform.localScale = poolScale * (1f + .08f * pulse);
            Paint(pool, settings.GlowColor * (.85f + .15f * pulse), Vector4.zero);
            // 빛기둥은 멀리서 건물을 찾을 때 쓰고, 문 앞에 다가오면 문을 가리지 않게 옅어진다.
            float far = Mathf.InverseLerp(
                BeamFadeStartMeters,
                BeamFadeEndMeters,
                FlatDistance(feet, entrance.position)
            );
            Paint(beam, settings.GlowColor * ((.7f + .1f * pulse) * far), Vector4.zero);
            TurnBeamToCamera();
        }

        private void ShowRoute(bool shows, Vector3 feet)
        {
            if (!shows)
            {
                route.enabled = false;
                clock.Hide();
                return;
            }

            if (clock.NeedsPath(placeCode, feet, Time.time))
                FindRoute(feet);
            GuidePolyline.Take(laid, routeLength * clock.Revealed(Time.time), shown);
            route.enabled = shown.Count >= 2;
            if (!route.enabled)
                return;
            route.positionCount = shown.Count;
            for (int i = 0; i < shown.Count; i++)
                route.SetPosition(i, shown[i]);
            // 화살 무늬는 출입문 쪽 끝을 기준으로 두고 출입문 쪽으로 흘린다. 플레이어가 걸어 길 앞쪽이 줄어도 무늬가 튀지 않는다.
            float offset = Mathf.Repeat(
                -routeLength - Time.time * settings.ArrowMetersPerSecond,
                1f
            );
            Paint(route, settings.GlowColor, new Vector4(1f, 1f, offset, 0f));
        }

        private void FindRoute(Vector3 feet)
        {
            laid.Clear();
            routeLength = 0f;
            bool found =
                NavMesh.SamplePosition(feet, out NavMeshHit start, SnapMeters, filter)
                && NavMesh.SamplePosition(entrance.position, out NavMeshHit end, SnapMeters, filter)
                && NavMesh.CalculatePath(start.position, end.position, filter, path)
                && path.status != NavMeshPathStatus.PathInvalid;
            if (found)
            {
                GuidePolyline.Resample(path.corners, settings.RouteSampleMeters, laid);
                for (int i = 0; i < laid.Count; i++)
                    laid[i] = OnGround(laid[i]);
                routeLength = GuidePolyline.Length(laid);
            }

            clock.PathFound(placeCode, feet, Time.time);
        }

        // 걸을 바닥(NavMesh)은 실제 바닥보다 조금 뜨거나 가라앉아 있으므로, 실제 바닥을 찾아 그 위에 살짝 띄운다.
        // 사람·차는 바닥으로 보지 않는다.
        private Vector3 OnGround(Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(
                point + Vector3.up * GroundProbeMeters,
                Vector3.down,
                hits,
                GroundProbeMeters + GroundProbeDepthMeters,
                ~0,
                QueryTriggerInteraction.Ignore
            );
            float nearest = float.MaxValue;
            float groundY = point.y;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance >= nearest || !IsGround(hits[i].collider))
                    continue;
                nearest = hits[i].distance;
                groundY = hits[i].point.y;
            }

            return new Vector3(point.x, groundY + settings.LiftMeters, point.z);
        }

        private static bool IsGround(Collider collider) =>
            collider.GetComponentInParent<CharacterController>() == null
            && collider.GetComponentInParent<LoopingCar>() == null;

        private void TurnBeamToCamera()
        {
            if (view == null)
                view = Camera.main;
            if (view == null)
                return;
            Vector3 away = beam.transform.position - view.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > .01f)
                beam.transform.rotation = Quaternion.LookRotation(away);
        }

        // 같은 재질을 쓰는 다른 곳은 바꾸지 않도록 이 그림에만 색과 무늬 위치를 준다.
        private void Paint(Renderer target, Color color, Vector4 scaleOffset)
        {
            target.GetPropertyBlock(paint);
            paint.SetColor(BaseColorId, color);
            if (scaleOffset != Vector4.zero)
                paint.SetVector(BaseMapScaleOffsetId, scaleOffset);
            target.SetPropertyBlock(paint);
        }

        private static float FlatDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y = 0f;
            return Vector3.Distance(from, to);
        }
    }
}
