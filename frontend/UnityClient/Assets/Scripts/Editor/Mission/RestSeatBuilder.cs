using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.Mission;
using A305.Npc;
using A305.ObjectHover;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.Mission
{
    /// <summary>
    /// 장소 씬의 의자·벤치·소파·스툴에 카페 의자와 같은 앉기 자리를 붙인다. 가구 배치와 디자인은 그대로 두고 가구 자식으로 자리 표시만 맞춘다.
    /// 앉을 가구는 호버 단어(<see cref="RestSeatLayout.IsSeatWord"/>)로 고르고, 자리는 가구가 그리는 메시로 <see cref="SeatModels"/>에서 찾는다.
    /// </summary>
    public static class RestSeatBuilder
    {
        // 다른 방식으로 앉는 장소. 카페는 첫날 미션이 자리를 관리해 CafeSeatBuilder가, 식당 부스·스툴은 주문 대기를 세는 DinerMealRest가 맡는다.
        private static readonly string[] ScenesSeatedElsewhere =
        {
            "CafeScene",
            "MapleDinerInterior",
        };

        // 등받이 없는 자리가 마주할 사물
        private static readonly string[] FacingSurfaceWords =
        {
            "table",
            "counter",
            "arcade game",
            "claw machine",
        };

        private static readonly float[] OneSeat = { 0f };

        // 앉는 면은 가구 원점 아래 바닥에서 잰 크기 1 기준 값이다. 앞(+z)이 앉아서 보는 방향이고, 여러 자리는 가구 폭 방향(x)으로 놓는다.
        // 같은 모델을 그리는 가구는 프리팹 중첩이나 풀기와 상관없이 같은 모양으로 본다.
        private static readonly RestSeatModel[] SeatModels =
        {
            // 메인스트리트 벤치(MainstreetArtKit.Bench): 좌판 널 윗면 0.325m, 앞 끝 0.205m. 폭 1.6m를 반씩 나눠 두 자리
            new RestSeatModel(
                "Assets/Art/World/MainstreetArt/Meshes/ParkBench_HoneyWood.asset",
                .325f,
                .205f,
                new[] { -.4f, .4f }
            ),
            // 공원 벤치(TinyTreats bench.fbx를 폭 1.6m로 0.8배): 좌판 윗면 0.40m, 앞 모서리 0.527m. 폭을 반씩 나눠 두 자리
            new RestSeatModel(
                "Assets/ThirdParty/TinyTreats/PrettyPark/Models/bench.fbx",
                .40f,
                .527f,
                new[] { -.4f, .4f }
            ),
            // 세이지 소파(HomeInteriorKit.Sofa): 쿠션 윗면 0.705m, 앞 모서리 0.485m. 쿠션 두 개의 가운데가 두 자리
            new RestSeatModel(
                "Assets/Art/World/MainstreetArt/Meshes/Home_SageSofa_Sage.asset",
                .705f,
                .485f,
                new[] { -.57f, .57f }
            ),
            // 카페 의자(TinyTreats chair.fbx): 카페의 같은 의자와 같은 값
            new RestSeatModel(
                "Assets/ThirdParty/TinyTreats/BakeryInterior/Models/chair.fbx",
                .33f,
                .30f,
                OneSeat
            ),
            // 노스블록 의자(Chair_17): 등받이 없는 높은 의자. 둥근 좌판 윗면 0.90m, 반지름 0.28m
            new RestSeatModel(
                "Assets/ThirdParty/FabNorthBlock/CuteFurniture/Chair_17.fbx",
                .90f,
                .28f,
                OneSeat,
                backless: true
            ),
            // 노스블록 안락의자(Armchair_02): 쿠션 윗면 0.62m, 앞 모서리 0.45m
            new RestSeatModel(
                "Assets/ThirdParty/FabNorthBlock/CuteFurniture/Armchair_02.fbx",
                .62f,
                .45f,
                OneSeat
            ),
            // 노스블록 소파(Couch_08): 쿠션 윗면 0.54m, 앞 모서리 0.40m. 쿠션 세 개마다 한 자리
            new RestSeatModel(
                "Assets/ThirdParty/FabNorthBlock/CuteFurniture/Couch_08.fbx",
                .54f,
                .40f,
                new[] { -.65f, 0f, .65f }
            ),
            // KayKit 나무 스툴: 좌판 윗면 0.35m, 반지름 0.25m
            new RestSeatModel(
                "Assets/ThirdParty/KayKitFurnitureBits/chair_stool_wood.fbx",
                .35f,
                .25f,
                OneSeat,
                backless: true
            ),
            // KayKit 나무 의자: 좌판 윗면 0.34m, 앞 모서리 0.26m
            new RestSeatModel(
                "Assets/ThirdParty/KayKitFurnitureBits/chair_B_wood.fbx",
                .34f,
                .26f,
                OneSeat
            ),
            // KayKit 쿠션 의자: 쿠션 윗면 0.36m, 앞 모서리 0.26m
            new RestSeatModel(
                "Assets/ThirdParty/KayKitFurnitureBits/chair_A.fbx",
                .36f,
                .26f,
                OneSeat
            ),
            // KayKit 쿠션 안락의자: 좌판 윗면 0.34m, 앞 모서리 0.55m
            new RestSeatModel(
                "Assets/ThirdParty/KayKitFurnitureBits/armchair_pillows.fbx",
                .34f,
                .55f,
                OneSeat
            ),
            // 미용실 드라이어 의자: 좌판 윗면 0.52m, 앞 모서리 0.33m
            new RestSeatModel(
                "Assets/Art/World/TownInteriors/Meshes/HoodDryerChair_SalonMint.asset",
                .52f,
                .33f,
                OneSeat
            ),
            // 미용실 세발 의자: 좌판 윗면 0.52m, 앞 모서리 0.33m. 등받이가 뒤 세면대 쪽으로 누워 있다.
            new RestSeatModel(
                "Assets/Art/World/TownInteriors/Meshes/WashChair_Leather.asset",
                .52f,
                .33f,
                OneSeat
            ),
            // 이발 의자(MainStreetBarber의 BarberChair 좌판): 원점이 좌판 높이에 있고 거울(-x)을 본다. 좌판 윗면 0.64m, 앞 모서리 0.31m
            new RestSeatModel(
                "Assets/Art/World/MainstreetArt/Meshes/Round_0.62_0.16_0.62_0.015.asset",
                .64f,
                .31f,
                OneSeat,
                yawDegrees: -90f
            ),
        };

        [MenuItem("Tools/A305/Mission/Connect Seat Sitting")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled)
                    continue;
                if (ScenesSeatedElsewhere.Contains(Path.GetFileNameWithoutExtension(entry.path)))
                    continue;
                Scene scene = EditorSceneManager.OpenScene(entry.path);
                if (Connect(scene))
                    BuilderTools.SaveScene(scene, entry.path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                "Seat sitting connected to chairs, benches, sofas and stools in place scenes."
            );
        }

        // 앉을 가구가 있어 자리를 맞췄으면 true
        private static bool Connect(Scene scene)
        {
            HoverableObject[] hoverables = BuilderTools.FindAllInScene<HoverableObject>(scene);
            HoverableObject[] furniture = hoverables.Where(IsSeatFurniture).ToArray();
            if (furniture.Length == 0)
                return false;

            PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
            DialogueController dialogue = BuilderTools.FindSingleInScene<DialogueController>(scene);
            PasserbyOfferView offer = BuilderTools.FindSingleInScene<PasserbyOfferView>(scene);
            if (player == null || dialogue == null || offer == null)
                throw new InvalidOperationException(
                    $"{scene.name}에 앉기에 필요한 플레이어·대화·상호작용 화면이 없습니다."
                );
            BuilderTools.GetOrAddComponent<PlayerSeatPose>(player.gameObject);
            HoverableObject[] surfaces = hoverables
                .Where(item =>
                    item.gameObject.activeInHierarchy
                    && item.Word != null
                    && FacingSurfaceWords.Contains(item.Word.Details.Text)
                )
                .ToArray();

            var unknown = new List<string>();
            foreach (HoverableObject item in furniture)
            {
                RestSeatModel model = ModelOf(item);
                if (model == null)
                {
                    unknown.Add(item.name);
                    continue;
                }
                Vector3 furnitureScale = TidyScale(item.transform.lossyScale);
                RestSeatModel placed = model.ScaledBy(furnitureScale);
                float yawDegrees = placed.Backless
                    ? RestSeatLayout.BacklessYawDegrees(
                        SurfaceOffsets(item.transform, placed, surfaces)
                    )
                    : placed.YawDegrees;
                for (int index = 0; index < model.SeatOffsetsMeters.Length; index++)
                {
                    RestSeat seat = PlaceSeat(
                        item.transform,
                        $"RestSeat{index + 1}",
                        model.SeatOffsetsMeters[index],
                        yawDegrees,
                        furnitureScale
                    );
                    BuilderTools.SetReference(seat, "player", player);
                    BuilderTools.SetReference(seat, "dialogue", dialogue);
                    BuilderTools.SetReference(seat, "offerView", offer);
                    var fields = new SerializedObject(seat);
                    fields.FindProperty("cushionTopMeters").floatValue = placed.CushionTopMeters;
                    fields.FindProperty("frontEdgeMeters").floatValue = placed.FrontEdgeMeters;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (unknown.Count > 0)
                throw new InvalidOperationException(
                    $"{scene.name}의 {string.Join(", ", unknown)}에 맞는 앉기 자리 모델이 없습니다. "
                        + "RestSeatBuilder.SeatModels에 메시 경로와 잰 값을 추가합니다."
                );
            return true;
        }

        private static bool IsSeatFurniture(HoverableObject item) =>
            item.gameObject.activeInHierarchy
            && item.Word != null
            && RestSeatLayout.IsSeatWord(item.Word.Details.Text);

        private static RestSeatModel ModelOf(HoverableObject furniture)
        {
            foreach (MeshFilter filter in furniture.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                RestSeatModel model = SeatModels.FirstOrDefault(item => item.MeshPath == meshPath);
                if (model != null)
                    return model;
            }
            return null;
        }

        // 자리는 가구 로컬 좌표로 둔다. 원점은 가구 아래 바닥이라, 이발 의자처럼 가구 원점이 바닥보다 높아도 그린 모양의 가장 낮은 곳에 둔다.
        // 가구 원점에서 잰 모델 값(크기 1 기준)을 그대로 로컬 위치로 쓰므로 크기를 바꿔 놓은 가구에서도 함께 줄어든다.
        private static RestSeat PlaceSeat(
            Transform furniture,
            string name,
            float offsetMeters,
            float yawDegrees,
            Vector3 furnitureScale
        )
        {
            Transform marker = furniture.Find(name);
            if (marker == null)
            {
                marker = new GameObject(name).transform;
                marker.SetParent(furniture, false);
            }
            float floorOffset = Tidy(DrawnBounds(furniture).min.y - furniture.position.y);
            marker.localPosition = new Vector3(offsetMeters, floorOffset / furnitureScale.y, 0f);
            marker.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            // 앉는 면 값은 미터 단위라 자리 크기는 1로 둔다.
            marker.localScale = new Vector3(
                1f / furnitureScale.x,
                1f / furnitureScale.y,
                1f / furnitureScale.z
            );
            return BuilderTools.GetOrAddComponent<RestSeat>(marker.gameObject);
        }

        // 회전한 가구의 lossyScale과 그린 모양 경계에 생기는 부동소수 오차(0.99999976 등)가 씬에 남지 않게 다듬는다.
        private static Vector3 TidyScale(Vector3 scale) =>
            new Vector3(Tidy(scale.x), Tidy(scale.y), Tidy(scale.z));

        private static float Tidy(float value)
        {
            float rounded = Mathf.Round(value * 10000f) / 10000f;
            // -0은 0으로 저장해 기존 자리 값이 바뀌어 보이지 않게 한다.
            return rounded == 0f ? 0f : rounded;
        }

        // 가구 방향 좌표에서 잰, 자리 중심에서 주변 탁자·카운터·오락기의 가장 가까운 점까지
        private static List<Vector3> SurfaceOffsets(
            Transform furniture,
            RestSeatModel model,
            IEnumerable<HoverableObject> surfaces
        )
        {
            Vector3 seatCenter =
                new Vector3(
                    furniture.position.x,
                    DrawnBounds(furniture).min.y,
                    furniture.position.z
                )
                + Vector3.up * model.CushionTopMeters;
            Quaternion toFurniture = Quaternion.Inverse(FurnitureYaw(furniture));
            var offsets = new List<Vector3>();
            foreach (HoverableObject surface in surfaces)
            {
                Collider[] colliders = surface
                    .GetComponentsInChildren<Collider>()
                    .Where(collider => !collider.isTrigger)
                    .ToArray();
                foreach (Collider collider in colliders)
                {
                    Vector3 offset = collider.bounds.ClosestPoint(seatCenter) - seatCenter;
                    offset.y = 0f;
                    offsets.Add(toFurniture * offset);
                }
            }
            return offsets;
        }

        private static Quaternion FurnitureYaw(Transform furniture) =>
            Quaternion.Euler(0f, furniture.eulerAngles.y, 0f);

        private static Bounds DrawnBounds(Transform furniture)
        {
            Renderer[] renderers = furniture.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
