using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.UI;
using A305.EditorTools.World;
using A305.Npc;
using A305.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 먼저 말을 거는 행인의 프리팹·설정·확인용 인사말과 대화 제안 화면을 준비하고, 마을에 행인을 배치한다.
    /// 공간 씬에 붙이는 일은 <see cref="ConnectToScene"/>을 통해 공간 기능 연결 도구가 한다.
    /// 이미 있는 데이터 에셋은 그대로 두고, 프리팹은 기능 구성만 맞추며 모델을 두는 Visual 아래는 유지한다.
    /// </summary>
    public static class NpcBuilder
    {
        public const string MenuPath = "Tools/A305/Npc/Build";
        public const string PasserbyPrefabPath = "Assets/Prefabs/Npc/Passerby.prefab";

        private const string OfferPrefabPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";
        private const string TutorialSettingsPath =
            "Assets/Data/Npc/TutorialPasserbySettings.asset";
        private const string TownSettingsPath = "Assets/Data/Npc/TownPasserbySettings.asset";
        private const string CafeStaffSettingsPath = "Assets/Data/Npc/CafeStaffSettings.asset";
        private const string MockLinesPath = "Assets/Data/Npc/MockNpcLines.asset";
        private const string TownScenePath = "Assets/Scenes/Production/TownScene.unity";
        private const string CafeScenePath = "Assets/Scenes/Production/CafeScene.unity";

        // NPC 키는 ERD npcs.code 값으로 쓴다. 서버 시드 데이터와 같아야 한다.
        private const string TutorialPasserbyCode = "TUTORIAL_PASSERBY";
        private const string TownPasserbyCode = "TOWN_PASSERBY";
        private const string CafeStaffCode = "CAFE_STAFF";

        // CharacterController와 도형 크기를 맞춘 사람 키(m)
        private const float BodyHeightMeters = 1.8f;
        private const float BodyRadiusMeters = 0.3f;

        // 서버 연동 전 확인용 인사말. 서버 NPC·시나리오 데이터가 준비되면 서버 값을 쓴다.
        private static readonly (string NpcCode, string Greeting)[] MockGreetings =
        {
            (TutorialPasserbyCode, "Excuse me! Could you help me? I'm looking for the cafe."),
            (TownPasserbyCode, "Hi there! Nice weather today, isn't it?"),
            (CafeStaffCode, "Hi there! Welcome to the corner café. Would you like to order?"),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
            {
                return;
            }

            // 행인을 놓은 씬에 공간 기능 연결을 다시 적용하므로, 연결에 쓰는 다른 기능의 프리팹도 함께 준비한다.
            SpaceSceneSetup.PrepareFeatureAssets();
            PlacePasserbysInTown();
            PlaceStaffInCafe();
            AssetDatabase.SaveAssets();
            Debug.Log($"행인을 준비했습니다: {PasserbyPrefabPath}");
        }

        /// <summary>행인 설정·확인용 인사말·행인 프리팹·대화 제안 화면 프리팹을 준비한다.</summary>
        public static void PrepareAssets()
        {
            // 튜토리얼 행인은 집 앞에서 바로 다가오고 거절할 수 없다. 무시하고 멀어지면 잠시 뒤 다시 다가온다.
            CreateSettingsIfMissing(
                TutorialSettingsPath,
                detectRadiusMeters: 8f,
                restSeconds: 15f,
                canDecline: false
            );
            CreateSettingsIfMissing(
                TownSettingsPath,
                detectRadiusMeters: 6f,
                restSeconds: 60f,
                canDecline: true
            );
            // 카페 직원은 카운터 뒤에 서 있다가, 카운터 앞까지 오면 걷지 않고 그 자리에서 대화를 제안한다.
            CreateSettingsIfMissing(
                CafeStaffSettingsPath,
                detectRadiusMeters: 3.5f,
                restSeconds: 20f,
                canDecline: true,
                offerDistanceMeters: 2.5f,
                walkSpeedMetersPerSecond: 0f
            );
            PrepareMockLines();
            BuilderTools.UpdateOrCreatePrefab(PasserbyPrefabPath, "Passerby", SetUpPasserby);
            BuilderTools.UpdateOrCreatePrefab(OfferPrefabPath, "PasserbyOffer", SetUpOffer);
        }

        /// <summary>
        /// 마을에 튜토리얼 행인(집 앞)과 일반 행인(동서 길)을 둔다. 같은 NPC 키의 행인이 이미 있으면 위치를 바꾸지 않는다.
        /// </summary>
        public static void PlacePasserbysInTown()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TownScenePath) == null)
            {
                return;
            }

            Scene town = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            SceneGroups groups = BuilderTools.EnsureGroups(town);
            string[] placedCodes = BuilderTools
                .FindAllInScene<NpcIdentity>(town)
                .Select(identity => identity.Code)
                .ToArray();

            if (!placedCodes.Contains(TutorialPasserbyCode))
            {
                // 집 문 앞(-3.5, 0, -16)에서 6.7m 떨어진 큰길 위. 집을 바라본다.
                Place(
                    groups,
                    TutorialPasserbyCode,
                    "TutorialPasserby",
                    TutorialSettingsPath,
                    new Vector3(2.5f, 0f, -13f),
                    -90f
                );
            }

            if (!placedCodes.Contains(TownPasserbyCode))
            {
                // 출입구에서 떨어진 동서 길 서쪽. 건물에서 나오자마자 붙잡지 않는다.
                Place(
                    groups,
                    TownPasserbyCode,
                    "TownPasserby",
                    TownSettingsPath,
                    new Vector3(-14f, 0f, 10f),
                    90f
                );
            }

            SpaceSceneSetup.Apply(town);
            BuilderTools.SaveScene(town, TownScenePath);
        }

        /// <summary>
        /// 카페 카운터 뒤에 카페 직원을 둔다. 같은 NPC 키가 이미 있으면 위치를 바꾸지 않는다.
        /// 직원은 행인과 같은 프리팹을 쓰고, 걷지 않는 설정으로 그 자리에서 대화를 제안한다.
        /// </summary>
        public static void PlaceStaffInCafe()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CafeScenePath) == null)
            {
                return;
            }

            Scene cafe = EditorSceneManager.OpenScene(CafeScenePath, OpenSceneMode.Single);
            SceneGroups groups = BuilderTools.EnsureGroups(cafe);
            bool isPlaced = BuilderTools
                .FindAllInScene<NpcIdentity>(cafe)
                .Any(identity => identity.Code == CafeStaffCode);

            if (!isPlaced)
            {
                // 카운터(z 3.3~4.3) 뒤에서 입구를 바라본다. 카운터 앞에 선 플레이어와 약 2m 떨어진다.
                Place(
                    groups,
                    CafeStaffCode,
                    "CafeStaff",
                    CafeStaffSettingsPath,
                    new Vector3(0f, 0f, 5f),
                    180f
                );
            }

            SpaceSceneSetup.Apply(cafe);
            BuilderTools.SaveScene(cafe, CafeScenePath);
        }

        /// <summary>
        /// 씬에 대화 제안 화면이 없으면 붙이고, 씬에 놓인 모든 행인을 플레이어·대화 처리·제안 화면과 연결한다.
        /// </summary>
        public static void ConnectToScene(
            SceneGroups groups,
            PlayerMover player,
            DialogueController dialogue
        )
        {
            var offer = BuilderTools.FindSingleInScene<PasserbyOfferView>(groups.Scene);
            if (offer == null)
            {
                offer = BuilderTools
                    .InstantiatePrefab(
                        BuilderTools.LoadRequiredAsset<GameObject>(OfferPrefabPath, MenuPath),
                        groups.UI,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .GetComponent<PasserbyOfferView>();
            }

            foreach (Passerby passerby in BuilderTools.FindAllInScene<Passerby>(groups.Scene))
            {
                BuilderTools.SetReference(passerby, "player", player);
                BuilderTools.SetReference(passerby, "dialogue", dialogue);
                BuilderTools.SetReference(passerby, "offerView", offer);
                BuilderTools.RequireReferences(passerby);
            }
        }

        private static void Place(
            SceneGroups groups,
            string npcCode,
            string objectName,
            string settingsPath,
            Vector3 position,
            float yawDegrees
        )
        {
            GameObject instance = BuilderTools.InstantiatePrefab(
                BuilderTools.LoadRequiredAsset<GameObject>(PasserbyPrefabPath, MenuPath),
                groups.Characters,
                position,
                Quaternion.Euler(0f, yawDegrees, 0f)
            );
            instance.name = objectName;
            BuilderTools.SetText(instance.GetComponent<NpcIdentity>(), "code", npcCode);
            BuilderTools.SetReference(
                instance.GetComponent<Passerby>(),
                "settings",
                BuilderTools.LoadRequiredAsset<PasserbySettings>(settingsPath, MenuPath)
            );
        }

        private static void SetUpPasserby(GameObject root)
        {
            if (!root.TryGetComponent(out CharacterController characterController))
            {
                characterController = root.AddComponent<CharacterController>();
                characterController.height = BodyHeightMeters;
                characterController.radius = BodyRadiusMeters;
                characterController.center = new Vector3(0f, BodyHeightMeters / 2f, 0f);
            }

            var identity = BuilderTools.GetOrAddComponent<NpcIdentity>(root);
            var animationDriver = BuilderTools.GetOrAddComponent<CharacterAnimationDriver>(root);
            var passerby = BuilderTools.GetOrAddComponent<Passerby>(root);

            Transform lookTarget = root.transform.Find("LookTarget");
            if (lookTarget == null)
            {
                lookTarget = BuilderTools.CreateEmpty(
                    "LookTarget",
                    root.transform,
                    new Vector3(0f, 1.6f, 0f),
                    Quaternion.identity
                );
            }

            // 모델로 바꿀 때는 Visual 아래만 교체한다. 콜라이더는 루트의 CharacterController가 맡는다.
            if (root.transform.Find("Visual") == null)
            {
                CreatePlaceholderVisual(root.transform);
            }

            BuilderTools.SetReference(identity, "lookTarget", lookTarget);
            BuilderTools.SetReference(passerby, "animationDriver", animationDriver);
            SetReferenceIfEmpty(passerby, "settings", () => LoadSettings(TownSettingsPath));
            SetReferenceIfEmpty(
                passerby,
                "lineSource",
                () => BuilderTools.LoadRequiredAsset<MockNpcLineSource>(MockLinesPath, MenuPath)
            );
        }

        private static void CreatePlaceholderVisual(Transform root)
        {
            Transform visual = BuilderTools.CreateEmpty(
                "Visual",
                root,
                Vector3.zero,
                Quaternion.identity
            );
            BuilderTools.CreateShape(
                PrimitiveType.Capsule,
                "Body",
                visual,
                new Vector3(0f, BodyHeightMeters / 2f, 0f),
                new Vector3(BodyRadiusMeters * 2f, BodyHeightMeters / 2f, BodyRadiusMeters * 2f),
                BuilderTools.GetOrCreateMaterial(
                    "Assets/Art/Npc",
                    "PasserbyBody",
                    new Color(0.9f, 0.55f, 0.25f)
                ),
                keepCollider: false
            );
            // 몸이 어느 쪽을 보는지 알 수 있게 앞쪽에 표시를 단다.
            BuilderTools.CreateShape(
                PrimitiveType.Cube,
                "FrontMarker",
                visual,
                new Vector3(0f, 1.5f, BodyRadiusMeters),
                new Vector3(0.2f, 0.1f, 0.15f),
                BuilderTools.GetOrCreateMaterial(
                    "Assets/Art/Npc",
                    "PasserbyFront",
                    new Color(0.95f, 0.95f, 0.95f)
                ),
                keepCollider: false
            );
        }

        /// <summary>대화 제안 화면. 화면 아래에 행인이 건넨 말과 [대화하기]·[지금은 안 할래요]를 둔다.</summary>
        private static void SetUpOffer(GameObject root)
        {
            UiBuilderTools.EnsureOverlayCanvas(root, sortingOrder: 110, receivesClicks: true);
            var view = BuilderTools.GetOrAddComponent<PasserbyOfferView>(root);

            RectTransform panel = UiBuilderTools.FindOrCreateBottomPanel(
                root.transform,
                "Panel",
                760f,
                80f
            );
            TMP_FontAsset bold = UiBuilderTools.BoldFont();
            TMP_FontAsset regular = UiBuilderTools.RegularFont();
            UiBuilderTools.FindOrCreateText(
                panel,
                "Hint",
                regular,
                22f,
                new Color(0.8f, 0.82f, 0.86f),
                "행인이 말을 걸었어요"
            );
            TMP_Text greeting = UiBuilderTools.FindOrCreateText(
                panel,
                "Greeting",
                bold,
                30f,
                Color.white
            );
            RectTransform buttons = UiBuilderTools.FindOrCreateButtonRow(panel, "Buttons");
            Button accept = UiBuilderTools.FindOrCreateButton(
                buttons,
                "AcceptButton",
                "대화하기",
                bold
            );
            Button decline = UiBuilderTools.FindOrCreateButton(
                buttons,
                "DeclineButton",
                "지금은 안 할래요",
                bold
            );

            BuilderTools.SetReference(view, "panel", panel.gameObject);
            BuilderTools.SetReference(view, "greetingLabel", greeting);
            BuilderTools.SetReference(view, "acceptButton", accept);
            BuilderTools.SetReference(view, "declineButton", decline);
            BuilderTools.RequireReferences(view);

            // 제안할 때만 보이므로, Play 전 씬 화면에서 다른 UI와 겹쳐 보이지 않게 숨겨 둔다.
            panel.gameObject.SetActive(false);
        }

        private static void CreateSettingsIfMissing(
            string path,
            float detectRadiusMeters,
            float restSeconds,
            bool canDecline,
            float offerDistanceMeters = 2f,
            float walkSpeedMetersPerSecond = 1.6f
        )
        {
            if (AssetDatabase.LoadAssetAtPath<PasserbySettings>(path) != null)
            {
                return;
            }

            BuilderTools.EnsureFolder(System.IO.Path.GetDirectoryName(path));
            var settings = ScriptableObject.CreateInstance<PasserbySettings>();
            var fields = new SerializedObject(settings);
            fields.FindProperty("detectRadiusMeters").floatValue = detectRadiusMeters;
            fields.FindProperty("restSeconds").floatValue = restSeconds;
            fields.FindProperty("canDecline").boolValue = canDecline;
            fields.FindProperty("offerDistanceMeters").floatValue = offerDistanceMeters;
            fields.FindProperty("walkSpeedMetersPerSecond").floatValue = walkSpeedMetersPerSecond;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, path);
        }

        /// <summary>
        /// 확인용 인사말 에셋이 없으면 만들고, 있으면 아직 없는 NPC 키의 인사말만 덧붙인다. 이미 있는 인사말은 바꾸지 않는다.
        /// </summary>
        private static void PrepareMockLines()
        {
            var lines = AssetDatabase.LoadAssetAtPath<MockNpcLineSource>(MockLinesPath);
            if (lines == null)
            {
                BuilderTools.EnsureFolder(System.IO.Path.GetDirectoryName(MockLinesPath));
                lines = ScriptableObject.CreateInstance<MockNpcLineSource>();
                AssetDatabase.CreateAsset(lines, MockLinesPath);
            }

            var fields = new SerializedObject(lines);
            SerializedProperty entries = fields.FindProperty("entries");
            foreach ((string npcCode, string greeting) in MockGreetings)
            {
                if (lines.TryGetGreeting(npcCode, out _))
                {
                    continue;
                }

                entries.InsertArrayElementAtIndex(entries.arraySize);
                SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("npcCode").stringValue = npcCode;
                entry.FindPropertyRelative("greeting").stringValue = greeting;
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lines);
        }

        private static PasserbySettings LoadSettings(string path)
        {
            return BuilderTools.LoadRequiredAsset<PasserbySettings>(path, MenuPath);
        }

        private static void SetReferenceIfEmpty(
            Object owner,
            string propertyName,
            System.Func<Object> value
        )
        {
            var fields = new SerializedObject(owner);
            if (fields.FindProperty(propertyName).objectReferenceValue == null)
            {
                BuilderTools.SetReference(owner, propertyName, value());
            }
        }
    }
}
