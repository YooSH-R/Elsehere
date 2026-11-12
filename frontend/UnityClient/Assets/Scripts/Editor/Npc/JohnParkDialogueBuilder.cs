using System;
using System.Linq;
using A305.Characters;
using A305.Dialogue;
using A305.EditorTools.Common;
using A305.EditorTools.Dialogue;
using A305.EditorTools.Player;
using A305.Npc;
using A305.Player;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Npc
{
    /// <summary>전용 리듬을 유지한 채 기존 클릭·대화 화면·음성 경로에 연결한다.</summary>
    public static class JohnParkDialogueBuilder
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/NpcJohnParkDialogueSandbox.unity";
        public const string AudioFolder = "Assets/Audio/Characters/JohnParkDialogue";
        private const string DataFolder = "Assets/Data/Npc/JohnPark";
        private const string OfferPath = "Assets/Prefabs/Npc/PasserbyOffer.prefab";

        public static void RefreshDisplayName()
        {
            BuildReviewScript();
            AssetDatabase.SaveAssets();
        }

        public static void BuildAndValidate()
        {
            JohnParkEmotionBuilder.Apply();
            Build();
            JohnParkEmotionValidation.ValidateAndCapture();
        }

        /// <summary>식당 매장 장면. 마을 매장은 따로 장면이라 존박은 식당 장면 안에 산다.</summary>
        public const string RestaurantScenePath =
            "Assets/Scenes/ShopInteriors/MapleDinerInterior.unity";

        /// <summary>
        /// 존박을 입구 화면 기준 오른쪽(-1.5, 0, -5.2)에 두고 그 장면의 플레이어·대화창에 잇는다.
        /// 예전처럼 마을 장면에 두었던 존박이 남아 있으면 지운다.
        /// </summary>
        public static void PlaceInRestaurant()
        {
            BuilderTools.ThrowIfPlaying();
            const string townPath = "Assets/Scenes/Production/TownScene.unity";
            var town = EditorSceneManager.OpenScene(townPath);
            NpcIdentity inTown = BuilderTools
                .FindAllInScene<NpcIdentity>(town)
                .SingleOrDefault(item => item.Code == JohnParkEmotionBuilder.NpcCode);
            if (inTown != null)
            {
                Object.DestroyImmediate(inTown.gameObject);
                EditorSceneManager.SaveScene(town, townPath);
            }

            var scene = EditorSceneManager.OpenScene(RestaurantScenePath);
            var shop = BuilderTools
                .FindAllInScene<Transform>(scene)
                .Single(item => item.name == "MapleDiner" && item.Find("Interior") != null);
            var identity = BuilderTools
                .FindAllInScene<NpcIdentity>(scene)
                .SingleOrDefault(item => item.Code == JohnParkEmotionBuilder.NpcCode);
            if (identity == null)
                identity = BuilderTools
                    .InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(JohnParkRhythmBuilder.PrefabPath),
                        BuilderTools.EnsureGroups(scene).Characters,
                        Vector3.zero,
                        Quaternion.identity
                    )
                    .GetComponent<NpcIdentity>();
            identity.transform.SetPositionAndRotation(
                shop.transform.TransformPoint(new Vector3(-1.5f, 0, -5.2f)),
                shop.transform.rotation
            );
            ConnectActor(
                identity,
                BuilderTools.FindSingleInScene<PlayerMover>(scene),
                BuilderTools.FindSingleInScene<DialogueController>(scene),
                BuilderTools.FindSingleInScene<PasserbyOfferView>(scene)
            );
            identity.gameObject.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(identity.transform);
            AssetDatabase.SaveAssets();
            BuilderTools.SaveScene(scene, RestaurantScenePath);
            if (!IsInsideRestaurant(shop, identity.transform))
                throw new InvalidOperationException("존박이 식당 실내에 없습니다.");
            Debug.Log("존박 식당 상주 배치 완료: 입구 화면 오른쪽, 건물 기준 (-1.5, 0, -5.2)");
        }

        /// <summary>식당 건물 1층 실내(건물 기준 x ±4, z 0~-8) 안에 있는지.</summary>
        internal static bool IsInsideRestaurant(Transform shop, Transform npc)
        {
            Vector3 local = shop.InverseTransformPoint(npc.position);
            return Mathf.Abs(local.x) < 4 && local.z < 0 && local.z > -8 && local.y < 3.5f;
        }

        [MenuItem("Tools/A305/Npc/John Park/Build Offline Dialogue Review")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            JohnParkEmotionBuilder.BuildSandbox();
            var groups = BuilderTools.NewSceneWithGroups();
            var actor = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(JohnParkRhythmBuilder.PrefabPath),
                groups.Characters,
                Vector3.zero,
                Quaternion.identity
            );
            var player = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSandboxBuilder.PlayerPrefabPath),
                groups.Characters,
                new Vector3(0, 0, 2.5f),
                Quaternion.Euler(0, 180, 0)
            );
            var camera = BuilderTools.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSandboxBuilder.CameraPrefabPath),
                groups.Systems,
                new Vector3(0, 1.4f, 5),
                Quaternion.Euler(0, 180, 0)
            );
            PlayerSandboxBuilder.ConnectPlayerAndCamera(player, camera);
            var controller = DialogueBuilder.ConnectToScene(
                groups,
                player.GetComponent<PlayerMover>(),
                camera.GetComponent<PlayerCameraRig>()
            );
            var scripts = BuildReviewScript();
            BuilderTools.SetReference(controller, "scriptSource", scripts);
            var link = BuilderTools.FindSingleInScene<DialogueServerLink>(groups.Scene);
            var settings = new SerializedObject(link);
            settings.FindProperty("useServer").boolValue = false;
            settings.FindProperty("apiSettings").objectReferenceValue = null;
            settings.FindProperty("voiceRecorder").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var offer = BuilderTools
                .InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(OfferPath),
                    groups.UI,
                    Vector3.zero,
                    Quaternion.identity
                )
                .GetComponent<PasserbyOfferView>();
            ConnectActor(
                actor.GetComponent<NpcIdentity>(),
                player.GetComponent<PlayerMover>(),
                controller,
                offer
            );
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(groups.Environment, false);
            ground.transform.localScale = Vector3.one * 2;
            ground.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    JohnParkRhythmBuilder.ArtFolder + "/PreviewFloor.mat"
                );
            var light = groups.Lighting.GetComponentInChildren<Light>();
            light.transform.rotation = Quaternion.Euler(35, -145, 0);
            light.intensity = 1.3f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(groups.Scene, ScenePath);
        }

        [MenuItem("Tools/A305/Npc/John Park/Connect Selected Performer To Dialogue")]
        public static void ConnectSelected()
        {
            if (!BuilderTools.CanBuild())
                return;
            var identity =
                Selection.activeGameObject != null
                    ? Selection.activeGameObject.GetComponentInParent<NpcIdentity>()
                    : null;
            if (
                identity == null
                || identity.Code != JohnParkEmotionBuilder.NpcCode
                || !identity.gameObject.scene.IsValid()
            )
                throw new InvalidOperationException("씬에 배치한 존박 프리팹을 선택하세요.");
            var scene = identity.gameObject.scene;
            ConnectActor(
                identity,
                BuilderTools.FindSingleInScene<PlayerMover>(scene),
                BuilderTools.FindSingleInScene<DialogueController>(scene),
                BuilderTools.FindSingleInScene<PasserbyOfferView>(scene)
            );
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Tools/A305/Npc/John Park/Update Ground Contact")]
        public static void UpdateGroundContact()
        {
            BuilderTools.ThrowIfPlaying();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] { ScenePath, RestaurantScenePath })
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    var identity = BuilderTools
                        .FindAllInScene<NpcIdentity>(scene)
                        .Single(n => n.Code == JohnParkEmotionBuilder.NpcCode);
                    var body = identity.GetComponent<CharacterController>();
                    if (body == null)
                        throw new InvalidOperationException(
                            "John의 접지 캡슐이 연결되지 않았습니다: " + path
                        );
                    // 고정 공연자는 보행용 8cm 접촉 여유를 쓰지 않는다. 메시·잡기·동작은 보존한다.
                    body.skinWidth = .005f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (setup.Any(item => item.isLoaded && item.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void ConnectActor(
            NpcIdentity identity,
            PlayerMover player,
            DialogueController dialogue,
            PasserbyOfferView offer
        )
        {
            if (player == null || dialogue == null || offer == null)
                throw new InvalidOperationException(
                    "먼저 씬의 공용 플레이어·대화·대화 제안 화면을 연결하세요."
                );
            BuilderTools.EnsureFolder(DataFolder);
            string settingsPath = DataFolder + "/PerformerSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PasserbySettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PasserbySettings>();
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("detectRadiusMeters").floatValue = 0;
                serialized.FindProperty("walkSpeedMetersPerSecond").floatValue = 0;
                serialized.FindProperty("followUpCallDistanceMeters").floatValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            var root = identity.gameObject;
            if (root.TryGetComponent(out CapsuleCollider collider))
                Object.DestroyImmediate(collider);
            var body = BuilderTools.GetOrAddComponent<CharacterController>(root);
            body.center = new Vector3(0, .85f, 0);
            body.height = 1.7f;
            body.radius = .35f;
            body.skinWidth = .005f;
            var passerby = BuilderTools.GetOrAddComponent<Passerby>(root);
            BuilderTools.SetReference(passerby, "settings", settings);
            BuilderTools.SetReference(
                passerby,
                "lineSource",
                AssetDatabase.LoadAssetAtPath<MockNpcLineSource>(NpcBuilder.MockLinesPath)
            );
            BuilderTools.SetReference(passerby, "player", player);
            BuilderTools.SetReference(passerby, "dialogue", dialogue);
            BuilderTools.SetReference(passerby, "offerView", offer);
            BuilderTools.SetReference(passerby, "animationDriver", null);
            var configuration = new SerializedObject(passerby);
            configuration.FindProperty("talkGesture").boolValue = false;
            configuration.ApplyModifiedPropertiesWithoutUndo();
            // 몸·팔을 공용 Humanoid 손짓으로 덮어쓰지 않고 전용 Generic 리듬을 계속 쓴다.
            PrefabUtility.RecordPrefabInstancePropertyModifications(passerby);
        }

        private static MockDialogueScriptSource BuildReviewScript()
        {
            BuilderTools.EnsureFolder(DataFolder);
            var scripts = BuilderTools.LoadOrCreateAsset<MockDialogueScriptSource>(
                DataFolder + "/OfflineDialogue.asset"
            );
            var data = new SerializedObject(scripts);
            var entries = data.FindProperty("entries");
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("npcCode").stringValue = JohnParkEmotionBuilder.NpcCode;
            entry.FindPropertyRelative("npcName").stringValue = "John";
            entry.FindPropertyRelative("roleLabel").stringValue = "뮤지션 · 로컬 음성 검수";
            entry.FindPropertyRelative("guidedTurns").arraySize = 0;
            entry.FindPropertyRelative("keywordReplies").arraySize = 2;
            WriteLine(entry.FindPropertyRelative("opening"), "Angry");
            WriteLine(entry.FindPropertyRelative("defaultReply"), "Surprised");
            entry.FindPropertyRelative("exampleSentence").stringValue = "sad";
            for (int i = 0; i < 2; i++)
            {
                var reply = entry.FindPropertyRelative("keywordReplies").GetArrayElementAtIndex(i);
                var keywords = reply.FindPropertyRelative("keywords");
                keywords.arraySize = 1;
                keywords.GetArrayElementAtIndex(0).stringValue = i == 0 ? "sad" : "angry";
                WriteLine(reply.FindPropertyRelative("reply"), i == 0 ? "Sad" : "Angry");
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return scripts;
        }

        private static void WriteLine(SerializedProperty line, string emotion)
        {
            var voice = AssetDatabase.LoadAssetAtPath<TextAsset>(
                AudioFolder + "/" + emotion + ".bytes"
            );
            if (voice == null)
                throw new InvalidOperationException("먼저 저장 음성 검수 자료를 준비하세요.");
            line.FindPropertyRelative("voice").objectReferenceValue = voice;
            line.FindPropertyRelative("english").stringValue =
                emotion == "Surprised"
                    ? "Wait, you put salt in the coffee? That's a new recipe. You really made my day!"
                    : "Oh, that order is wrong. I'm sorry about that. Let me make a fresh coffee for you.";
            line.FindPropertyRelative("korean").stringValue =
                emotion == "Surprised"
                    ? "커피에 소금을 넣었다고요? 새로운 레시피네요. 덕분에 정말 즐거웠어요! (기존 검수 음성)"
                    : "주문이 잘못됐네요. 죄송해요. 새 커피를 만들어 드릴게요. (기존 검수 음성)";
        }
    }
}
