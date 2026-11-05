using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.World;
using A305.Npc;
using A305.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 외형 표(<see cref="NpcCast"/>)의 NPC 중 다른 Builder가 두지 않는 직원·행인을 일터에 두고, 카페·호텔·드럭스토어·마을의 모든 NPC에
    /// 외형 표의 배색을 입힌 뒤 생활(<see cref="NpcLifeBuilder"/>)을 붙인다. 이미 있는 NPC는 위치를 바꾸지 않는다.
    /// Mia·Jake·Alex·Ben·택시 기사·거리 매장 직원은 각자의 Builder가, 경찰관은 <see cref="PoliceStationBuilder"/>가 둔다.
    /// </summary>
    public static class NpcCastBuilder
    {
        public const string MenuPath = "Tools/A305/Npc/Build Cast";
        private const string LineupMenuPath = "Tools/A305/Npc/Render Cast Lineups";

        // 외형별로 줄 세운 그림을 저장할 폴더. 비우면 저장소의 artifacts/unity/npc-cast/에 저장한다.
        private const string LineupOutputVariable = "NPC_CAST_LINEUP_OUTPUT";

        private const string CafeScenePath = "Assets/Scenes/Production/CafeScene.unity";
        private const string HotelScenePath = "Assets/Scenes/Production/HotelScene.unity";
        private const string ShopScenePath = "Assets/Scenes/Production/ShopScene.unity";
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";

        // 줄 세운 그림: NPC 한 명이 차지하는 칸의 폭과, 위(몸 전체)·가운데(얼굴)·아래(뒤통수) 줄의 높이(px)
        private const int LineupCellPixels = 560;
        private const int LineupBodyPixels = 950;
        private const int LineupHeadPixels = 550;

        // 근무 중인 직원 중 카운터에 가장 가까운 사람이 응대하므로(NpcDuty) 새 직원은 원래 직원(Mia·Jake·Alex)보다 카운터에서 조금 멀게
        // 서고, 한 사람 폭인 카운터 뒤 통로와 그 입구는 막지 않는다. 마을 행인은 다른 확인이 플레이어를 세우는 집 앞·경찰서·동서 길 서쪽에서
        // 감지 거리(6m)보다 먼 본길 북쪽 인도와 공원에서 지낸다. 좌표는 월드 기준이고 가구와 겹치면 가까운 빈자리로 옮긴다.
        private static readonly Newcomer[] Newcomers =
        {
            // 카페 카운터(x -3.65~3.65, z 3.1~4.3) 양 끝의 손님 쪽 모서리. Mia는 카운터에서 0.42m, 두 사람은 0.66m
            new Newcomer(
                "CAFE_BARISTA",
                "CafeBarista",
                CafeScenePath,
                -4.3f,
                3f,
                180,
                "CAFE_STAFF"
            ),
            new Newcomer("CAFE_SERVER", "CafeServer", CafeScenePath, 4.3f, 3f, 180, "CAFE_STAFF"),
            // 호텔 리셉션 데스크(x -2.9~2.9, z 4.0~5.2)의 손님 쪽 양옆과 뒤쪽 양 끝. Jake는 데스크에서 0.49m
            new Newcomer(
                "HOTEL_RECEPTIONIST",
                "HotelReceptionist",
                HotelScenePath,
                2.6f,
                3.3f,
                180,
                "HOTEL_STAFF"
            ),
            new Newcomer(
                "HOTEL_NIGHT_MANAGER",
                "HotelNightManager",
                HotelScenePath,
                -2.6f,
                3.3f,
                180,
                "HOTEL_STAFF"
            ),
            new Newcomer(
                "HOTEL_CONCIERGE",
                "HotelConcierge",
                HotelScenePath,
                3.9f,
                6.1f,
                180,
                "HOTEL_STAFF"
            ),
            new Newcomer(
                "HOTEL_BELLHOP",
                "HotelBellhop",
                HotelScenePath,
                -3.9f,
                6.1f,
                180,
                "HOTEL_STAFF"
            ),
            // 드럭스토어 계산대(x -2.5~2.5, z 2.0~2.8) 뒤. Alex는 계산대에서 0.78m, 두 사람은 1.38m
            new Newcomer(
                "SHOP_PHARMACIST",
                "ShopPharmacist",
                ShopScenePath,
                1.8f,
                4.2f,
                180,
                "SHOP_STAFF"
            ),
            new Newcomer(
                "SHOP_NIGHT_CLERK",
                "ShopNightClerk",
                ShopScenePath,
                -1.8f,
                4.2f,
                180,
                "SHOP_STAFF"
            ),
            // 마을 행인. Arthur·Margaret 부부는 나란히 북쪽을 보고 선다.
            new Newcomer(
                "TOWN_RETIREE",
                "TownRetiree",
                TownScenePath,
                -5f,
                15.5f,
                0,
                "TOWN_PASSERBY"
            ),
            new Newcomer(
                "FIRE_CAPTAIN",
                "FireCaptain",
                TownScenePath,
                9.6f,
                19.4f,
                45,
                "TOWN_PASSERBY"
            ),
            new Newcomer(
                "TOWN_STUDENT",
                "TownStudent",
                TownScenePath,
                12.6f,
                22.6f,
                225,
                "TOWN_PASSERBY"
            ),
            new Newcomer(
                "TOWN_GRANDFATHER",
                "TownGrandfather",
                TownScenePath,
                5f,
                27f,
                0,
                "TOWN_PASSERBY"
            ),
            new Newcomer(
                "TOWN_GRANDMOTHER",
                "TownGrandmother",
                TownScenePath,
                5.8f,
                27f,
                0,
                "TOWN_PASSERBY"
            ),
            new Newcomer(
                "TOWN_SHOPPER",
                "TownShopper",
                TownScenePath,
                -5f,
                27.5f,
                180,
                "TOWN_PASSERBY"
            ),
            new Newcomer(
                "FIRE_FIGHTER",
                "FireFighter",
                TownScenePath,
                -5f,
                36.5f,
                0,
                "TOWN_PASSERBY"
            ),
            new Newcomer(
                "TOWN_HANDYMAN",
                "TownHandyman",
                TownScenePath,
                5.2f,
                43f,
                180,
                "TOWN_PASSERBY"
            ),
            new Newcomer(
                "FIRE_ROOKIE",
                "FireRookie",
                TownScenePath,
                -5f,
                45.5f,
                0,
                "TOWN_PASSERBY"
            ),
            new Newcomer(
                "TOWN_EVENING_WALKER",
                "TownEveningWalker",
                TownScenePath,
                5.2f,
                52f,
                180,
                "TOWN_PASSERBY"
            ),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            NpcBuilder.PrepareMockLines();
            BuildScene(CafeScenePath);
            BuildScene(HotelScenePath);
            BuildScene(ShopScenePath);
            BuildScene(TownScenePath, PoliceStationBuilder.PlaceOfficers);

            // 생활 데이터가 있는 NPC에 생활을 붙이고 바닥을 다시 굽는다. 그 뒤 경찰관이 경찰서 안에서만 지내게 한다.
            NpcLifeBuilder.Build();
            Scene town = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            PoliceStationBuilder.KeepOfficersInStation(town);
            BuilderTools.SaveScene(town, TownScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("외형 표의 NPC를 장면에 두고 배색과 생활을 입혔습니다.");
        }

        // 기존 메뉴 호출과 자동화 스크립트를 유지한다.
        [MenuItem("Tools/A305/Npc/Connect Reviewed Staff Visuals")]
        public static void ConnectReviewedStaffVisuals() => RefreshSceneVisuals();

        // 배치·일과·대사·NavMesh를 다시 만들지 않고 모든 기존 NPC에 외형 표를 적용한다.
        [MenuItem("Tools/A305/Npc/Refresh Scene Visuals")]
        public static void RefreshSceneVisuals()
        {
            BuilderTools.ThrowIfPlaying();
            foreach (var look in NpcCast.Members.Select(member => member.Look).Distinct())
            {
                var prefab = BuilderTools.LoadRequiredAsset<GameObject>(look.PrefabPath, MenuPath);
                if (look.PrefabPath.StartsWith("Assets/Prefabs/Npc/Review/"))
                {
                    var rig = Tripo.TripoMasterRig.Inspect(
                        prefab,
                        Tripo.TripoMasterWorkflow.RequireMaster()
                    );
                    var motion = Tripo.TripoMasterMotionAudit.Inspect(prefab);
                    if (!rig.Passed || !motion.Passed)
                        throw new InvalidOperationException(
                            "마스터 골격·동작 검사를 통과하지 못했습니다: " + look.Name
                        );
                }
                var animator = prefab.GetComponentInChildren<Animator>();
                if (
                    animator == null
                    || animator.avatar == null
                    || !animator.avatar.isValid
                    || !animator.avatar.isHuman
                    || A305.Characters.CharacterFaceController.MissingChannels(
                        prefab.transform,
                        true
                    ).Length != 0
                )
                    throw new InvalidOperationException(
                        "몸 Avatar와 공통 표정이 준비되지 않은 외형입니다: " + look.Name
                    );
            }
            var connected = new HashSet<string>();
            foreach (
                string path in new[] { CafeScenePath, HotelScenePath, ShopScenePath, TownScenePath }
            )
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var npc in BuilderTools.FindAllInScene<NpcIdentity>(scene))
                {
                    var member =
                        NpcCast.Find(npc.Code)
                        ?? throw new InvalidOperationException(
                            "외형 표에 없는 NPC입니다: " + npc.Code
                        );
                    var position = npc.transform.position;
                    var rotation = npc.transform.rotation;
                    var routine = npc.GetComponent<NpcRoutine>();
                    string routineBefore =
                        routine != null ? EditorJsonUtility.ToJson(routine) : null;
                    // 운전석에 맞춘 Joe의 좌석 자세와 시선은 유지한다.
                    if (npc.Code != "TAXI_DRIVER")
                        ConnectLook(npc, member.Look);
                    NpcCastDresser.Dress(npc.gameObject, member);
                    if (
                        npc.transform.position != position
                        || npc.transform.rotation != rotation
                        || (routine != null && EditorJsonUtility.ToJson(routine) != routineBefore)
                    )
                        throw new InvalidOperationException(
                            "NPC 배치 또는 생활 설정이 바뀌었습니다: " + npc.Code
                        );
                    if (!connected.Add(npc.Code))
                        throw new InvalidOperationException("중복 배치된 NPC입니다: " + npc.Code);
                    Debug.Log($"NPC 외형 연결: {npc.Code} → {member.Look.PrefabPath}");
                }
                BuilderTools.SaveScene(scene, path);
            }
            var missing = NpcCast
                .Members.Where(member => !connected.Contains(member.Code))
                .Select(member => member.Code);
            if (missing.Any())
                throw new InvalidOperationException("씬에 없는 NPC: " + string.Join(", ", missing));
            RefreshCommuterVisuals();
            AssetDatabase.SaveAssets();
        }

        // 출퇴근 프리팹도 같은 검수 외형을 참조한다. 출현·경로 설정과 프리팹 GUID는 유지한다.
        internal static void RefreshCommuterVisuals()
        {
            const string folder = "Assets/Prefabs/Npc/Commuters";
            if (!AssetDatabase.IsValidFolder(folder))
                return;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var npc = root.GetComponent<NpcIdentity>();
                    var member = npc != null ? NpcCast.Find(npc.Code) : null;
                    if (member == null || root.GetComponent<NpcTownWalker>() == null)
                        throw new InvalidOperationException(
                            "출퇴근 NPC 연결을 확인하세요: " + path
                        );
                    bool active = root.activeSelf;
                    root.SetActive(true);
                    ConnectLook(npc, member.Look);
                    NpcCastDresser.Dress(root, member);
                    root.SetActive(active);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        /// <summary>
        /// 같은 외형을 쓰는 NPC를 외형 표 순서대로 왼쪽부터 세워, 위에는 몸 전체를, 가운데에는 얼굴을, 아래에는 뒤통수를 찍은 그림을 외형마다 하나씩 저장한다.
        /// 서로 다른 사람으로 보이는지 눈으로 확인하는 용도이며, NPC 순서는 같은 이름의 .txt에 적는다. 경찰 모자는 찍지 않는다.
        /// </summary>
        [MenuItem(LineupMenuPath)]
        public static void RenderLineups()
        {
            BuilderTools.ThrowIfPlaying();
            string folder = Environment.GetEnvironmentVariable(LineupOutputVariable);
            if (string.IsNullOrEmpty(folder))
            {
                folder = Path.Combine(
                    Path.GetDirectoryName(Application.dataPath),
                    "..",
                    "..",
                    "artifacts",
                    "unity",
                    "npc-cast"
                );
            }

            Directory.CreateDirectory(folder);
            foreach (NpcLook look in NpcLooks.All)
            {
                NpcCastMember[] members = NpcCast
                    .Members.Where(member => member.Look == look)
                    .ToArray();
                if (members.Length > 0)
                {
                    RenderLineup(look.Name, members, folder);
                }
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("NPC 외형 줄 세운 그림을 저장했습니다: " + Path.GetFullPath(folder));
        }

        public static void RenderReviewedStaffLineups()
        {
            string folder =
                Environment.GetEnvironmentVariable(LineupOutputVariable)
                ?? throw new InvalidOperationException("Set NPC_CAST_LINEUP_OUTPUT.");
            Directory.CreateDirectory(folder);
            foreach (
                var look in new[]
                {
                    NpcLooks.CafeMale,
                    NpcLooks.HotelierFemale,
                    NpcLooks.Grandfather,
                    NpcLooks.PoliceOfficer,
                }
            )
                RenderLineup(
                    look.Name,
                    NpcCast.Members.Where(member => member.Look == look).ToArray(),
                    folder
                );
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // ---------- 장면 ----------

        public static void RefreshAndRenderSceneVisuals()
        {
            RefreshSceneVisuals();
            RenderWorkplaceLineups();
        }

        [MenuItem("Tools/A305/Npc/Render Workplace Lineups")]
        public static void RenderWorkplaceLineups()
        {
            BuilderTools.ThrowIfPlaying();
            string folder =
                Environment.GetEnvironmentVariable(LineupOutputVariable)
                ?? throw new InvalidOperationException("NPC_CAST_LINEUP_OUTPUT을 지정하세요.");
            Directory.CreateDirectory(folder);
            var sceneScales = new Dictionary<string, Vector3>();
            foreach (
                string path in new[] { CafeScenePath, HotelScenePath, ShopScenePath, TownScenePath }
            )
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var npc in BuilderTools.FindAllInScene<NpcIdentity>(scene))
                {
                    Transform visual = npc.GetComponentInChildren<Animator>().transform;
                    while (visual.parent != npc.transform)
                        visual = visual.parent;
                    sceneScales.Add(npc.Code, visual.localScale);
                }
            }
            foreach (
                string prefix in new[] { "CAFE_", "HOTEL_", "SHOP_", "POLICE_", "TOWN_", "FIRE_" }
            )
                RenderLineup(
                    prefix.TrimEnd('_'),
                    NpcCast.Members.Where(member => member.Code.StartsWith(prefix)).ToArray(),
                    folder,
                    sceneScales
                );
            RenderLineup(
                "STORES",
                NpcCast
                    .Members.Where(member =>
                        member.Code.EndsWith("_STAFF")
                        && !member.Code.StartsWith("CAFE_")
                        && !member.Code.StartsWith("HOTEL_")
                        && !member.Code.StartsWith("SHOP_")
                    )
                    .ToArray(),
                folder,
                sceneScales
            );
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void BuildScene(string scenePath, Action<Scene> placeOthers = null)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (Newcomer newcomer in Newcomers.Where(item => item.ScenePath == scenePath))
            {
                Place(scene, newcomer);
            }

            placeOthers?.Invoke(scene);
            foreach (NpcIdentity npc in BuilderTools.FindAllInScene<NpcIdentity>(scene))
            {
                NpcCastMember member = NpcCast.Find(npc.Code);
                if (member != null)
                {
                    NpcCastDresser.Dress(npc.gameObject, member);
                }
            }

            NpcBuilder.ConnectToScene(
                BuilderTools.EnsureGroups(scene),
                BuilderTools.FindSingleInScene<PlayerMover>(scene),
                BuilderTools.FindSingleInScene<DialogueController>(scene)
            );
            BuilderTools.SaveScene(scene, scenePath);
        }

        // 행인 프리팹을 두고 외형 표의 외형을 붙인다. 설정(다가오는 거리 등)은 같은 일터의 원래 NPC와 같다.
        private static void Place(Scene scene, Newcomer newcomer)
        {
            NpcIdentity colleague = Find(scene, newcomer.ColleagueCode);
            if (colleague == null)
            {
                throw new InvalidOperationException(
                    $"{newcomer.Code}의 설정을 따를 {newcomer.ColleagueCode}가 장면에 없습니다: {scene.path}"
                );
            }

            NpcIdentity npc = Find(scene, newcomer.Code);
            if (npc == null)
            {
                GameObject placed = BuilderTools.InstantiatePrefab(
                    BuilderTools.LoadRequiredAsset<GameObject>(
                        NpcBuilder.PasserbyPrefabPath,
                        MenuPath
                    ),
                    BuilderTools.EnsureGroups(scene).Characters,
                    NpcPlacement.FreeSpot(null, newcomer.Stand),
                    Quaternion.Euler(0f, newcomer.YawDegrees, 0f)
                );
                placed.name = newcomer.ObjectName;
                npc = placed.GetComponent<NpcIdentity>();
                BuilderTools.SetText(npc, "code", newcomer.Code);
            }

            var passerby = npc.GetComponent<Passerby>();
            BuilderTools.SetReference(
                passerby,
                "settings",
                new SerializedObject(colleague.GetComponent<Passerby>())
                    .FindProperty("settings")
                    .objectReferenceValue
            );
            BuilderTools.SetReference(
                passerby,
                "lineSource",
                BuilderTools.LoadRequiredAsset<MockNpcLineSource>(
                    NpcBuilder.MockLinesPath,
                    MenuPath
                )
            );
            ConnectLook(npc, NpcCast.Find(newcomer.Code).Look);
        }

        private static void ConnectLook(NpcIdentity npc, NpcLook look)
        {
            // 각 외형의 실제 키·눈 위치와 자기 Avatar를 사용한다. 동료의 몸 설정을 복사하지 않는다.
            NpcVisualBuilder.Connect(
                npc,
                look.PrefabPath,
                look != NpcLooks.BusinessMale,
                look.InitialScale
            );
            // 기존 카페·호텔 Builder가 찾는 자식 이름은 유지한다.
            string childName =
                look == NpcLooks.Barista ? "Barista"
                : look == NpcLooks.Hotelier ? "Hotelier"
                : null;
            if (childName != null)
            {
                var visual = npc
                    .transform.Cast<Transform>()
                    .Single(child =>
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child)
                        == look.PrefabPath
                    );
                visual.name = childName;
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.gameObject);
            }
        }

        private static NpcIdentity Find(Scene scene, string code)
        {
            return BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .SingleOrDefault(npc => npc.Code == code);
        }

        // ---------- 줄 세운 그림 ----------

        private static void RenderLineup(
            string name,
            IReadOnlyList<NpcCastMember> members,
            string folder,
            IReadOnlyDictionary<string, Vector3> sceneScales = null
        )
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.62f, .62f, .62f);
            var light = new GameObject("LineupLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
            // NPC는 +z를 보고 서고 카메라는 그 앞에서 찍으므로, 빛도 앞 위쪽에서 비춘다.
            light.transform.rotation = Quaternion.Euler(30f, 200f, 0f);

            var copies = new List<GameObject>();
            foreach (NpcCastMember member in members)
            {
                GameObject prefab = BuilderTools.LoadRequiredAsset<GameObject>(
                    member.Look.PrefabPath,
                    LineupMenuPath
                );
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (sceneScales != null && sceneScales.TryGetValue(member.Code, out var scale))
                    copy.transform.localScale = scale;
                else
                    copy.transform.localScale *= member.Look.InitialScale;
                Animator animator = copy.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.Rebind();
                    animator.Update(0);
                }

                var controller = BuilderTools.GetOrAddComponent<CharacterController>(copy);
                foreach (var clearance in copy.GetComponentsInChildren<LittleMungArmClearance>())
                {
                    if (!clearance.enabled)
                        continue;
                    clearance.Initialize(controller);
                    clearance.CorrectPose();
                }

                NpcCastDresser.Dress(copy, member);
                copies.Add(copy);
            }

            // 외형의 키에 맞춰 위 줄이 몸 전체를 담고, 한 칸에 한 명씩 서게 간격을 정한다.
            float top = copies.Max(TopOf);
            float bodyMeters = top + .15f;
            float spacing = bodyMeters * LineupCellPixels / LineupBodyPixels;
            // 카메라가 -z를 보므로 화면 왼쪽이 +x다. 외형 표 순서가 왼쪽부터 오게 +x부터 세운다.
            for (int index = 0; index < copies.Count; index++)
            {
                copies[index].transform.position = new Vector3(
                    ((copies.Count - 1) / 2f - index) * spacing,
                    0f,
                    0f
                );
            }

            int width = LineupCellPixels * copies.Count;
            int height = LineupBodyPixels + LineupHeadPixels * 2;
            var target = new RenderTexture(width, height, 24);
            var camera = new GameObject("LineupCamera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.82f, .84f, .86f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 30f;
            camera.targetTexture = target;
            camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            float headFraction = (float)LineupHeadPixels / height;
            camera.rect = new Rect(0f, headFraction * 2f, 1f, 1f - headFraction * 2f);
            camera.orthographicSize = bodyMeters / 2f;
            camera.transform.position = new Vector3(0f, bodyMeters / 2f - .05f, 10f);
            camera.Render();

            // 얼굴 칸은 눈 높이(머리 꼭대기에서 0.6m쯤 아래)를 크게 찍고, 뒤통수 칸은 돌려 세운 뒷모습의 머리를 찍는다.
            RenderHeadRow(camera, copies, top - .62f, .42f, headFraction);
            foreach (GameObject copy in copies)
            {
                copy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            RenderHeadRow(camera, copies, top - .5f, .5f, 0f);

            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            File.WriteAllLines(
                Path.Combine(folder, name + ".txt"),
                members.Select((member, index) => $"{index + 1}. {member}")
            );
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }

        // 한 사람씩 칸을 나눠 같은 높이의 머리를 찍는다. bottom은 그 줄이 그림에서 시작하는 높이(비율)다.
        private static void RenderHeadRow(
            Camera camera,
            IReadOnlyList<GameObject> copies,
            float centerHeight,
            float halfHeightMeters,
            float bottom
        )
        {
            float rowFraction = (float)LineupHeadPixels / camera.targetTexture.height;
            for (int index = 0; index < copies.Count; index++)
            {
                camera.rect = new Rect(
                    (float)index / copies.Count,
                    bottom,
                    1f / copies.Count,
                    rowFraction
                );
                camera.orthographicSize = halfHeightMeters;
                camera.transform.position = new Vector3(
                    copies[index].transform.position.x,
                    centerHeight,
                    10f
                );
                camera.Render();
            }
        }

        // 서 있는 자세로 구운 몸 메시에서 가장 높은 곳(머리 꼭대기)
        private static float TopOf(GameObject copy)
        {
            float top = 0f;
            foreach (
                SkinnedMeshRenderer skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>()
            )
            {
                var baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked, true);
                    foreach (Vector3 vertex in baked.vertices)
                    {
                        top = Mathf.Max(top, skin.transform.TransformPoint(vertex).y);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }

            return top;
        }

        /// <summary>외형 표의 NPC 중 이 Builder가 장면에 두는 한 명. 자리는 월드 좌표이고 바닥 높이에 세운다.</summary>
        private sealed class Newcomer
        {
            public Newcomer(
                string code,
                string objectName,
                string scenePath,
                float x,
                float z,
                float yawDegrees,
                string colleagueCode
            )
            {
                Code = code;
                ObjectName = objectName;
                ScenePath = scenePath;
                Stand = new Vector3(x, 0f, z);
                YawDegrees = yawDegrees;
                ColleagueCode = colleagueCode;
            }

            public string Code { get; }
            public string ObjectName { get; }
            public string ScenePath { get; }
            public Vector3 Stand { get; }
            public float YawDegrees { get; }

            /// <summary>다가오는 거리 등 행동 설정을 따르는 같은 일터의 원래 NPC</summary>
            public string ColleagueCode { get; }
        }
    }
}
