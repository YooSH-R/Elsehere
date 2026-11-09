using System;
using A305.Characters;
using A305.EditorTools.Common;
using A305.Player;
using A305.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Player
{
    /// <summary>검증한 외형을 공용 Player와 공간 씬에 연결한다. 배경·NPC·카메라 배치는 유지한다.</summary>
    public static class PlayerCharacterBuilder
    {
        public const string CatalogPath = "Assets/Data/Player/Characters/Catalog.asset";
        private const string DataFolder = "Assets/Data/Player/Characters";
        private const string VisualFolder = "Assets/Prefabs/Player/Characters";

        // Tripo 생성 메시를 마스터 골격에 다시 연결한 Female Variant다. 골격·애니메이션·배율은 Female을 따른다.
        private const string MaleVisualPath = VisualFolder + "/Male.prefab";

        [MenuItem("Tools/A305/Player/Connect Selected Character")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            BuildCatalog();
            GameObject contents = PrefabUtility.LoadPrefabContents(
                PlayerSandboxBuilder.PlayerPrefabPath
            );
            try
            {
                ConfigurePlayer(contents, null);
                // 씬의 Player가 프리팹 값을 따르도록 이동 설정은 프리팹에만 연결한다.
                BuilderTools.SetReference(
                    contents.GetComponent<PlayerMover>(),
                    "movementSettings",
                    BuilderTools.LoadRequiredAsset<PlayerMovementSettings>(
                        PlayerSandboxBuilder.MovementSettingsPath,
                        "Little Mung movement settings"
                    )
                );
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerSandboxBuilder.PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            foreach (string name in new[] { "HomeScene", "TownScene", "CafeScene", "HotelScene" })
            {
                Scene scene = EditorSceneManager.OpenScene(
                    "Assets/Scenes/Production/" + name + ".unity"
                );
                PlayerMover player = BuilderTools.FindSingleInScene<PlayerMover>(scene);
                PlayerCameraRig rig = BuilderTools.FindSingleInScene<PlayerCameraRig>(scene);
                Vector3 position = player.transform.position;
                Quaternion rotation = player.transform.rotation;
                ConfigurePlayer(player.gameObject, rig);
                if (player.transform.position != position || player.transform.rotation != rotation)
                    throw new InvalidOperationException(
                        "캐릭터 연결 중 플레이어 배치가 바뀌었습니다."
                    );
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Debug.Log("공용 Player와 Production 4개 씬에 캐릭터 선택을 연결했습니다.");
        }

        public static void ConfigurePlayer(GameObject player, PlayerCameraRig rig)
        {
            var catalog = BuilderTools.LoadRequiredAsset<PlayerCharacterCatalog>(
                CatalogPath,
                "Player character catalog"
            );
            PlayerCharacterDefinition female = catalog.Resolve("F");
            var appearance = player.GetComponent<PlayerCharacterAppearance>();
            if (appearance == null)
                appearance = player.AddComponent<PlayerCharacterAppearance>();
            BuilderTools.GetOrAddComponent<PlayerCharacterVisibility>(player);
            Transform visual = appearance.VisualRoot;
            if (visual == null)
            {
                Transform old = player.transform.Find("Visual");
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
                visual = (
                    (GameObject)
                        PrefabUtility.InstantiatePrefab(female.VisualPrefab, player.transform)
                ).transform;
                visual.name = "Visual";
                visual.localPosition = female.VisualPrefab.transform.localPosition;
                visual.localRotation = female.VisualPrefab.transform.localRotation;
                visual.localScale = female.VisualPrefab.transform.localScale;
            }
            visual.gameObject.SetActive(true);
            foreach (
                string oldName in new[] { "TownVisual", "HomeVisual", "CafeVisual", "HotelVisual" }
            )
            {
                Transform old = player.transform.Find(oldName);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
            }
            BuilderTools.SetReference(appearance, "catalog", catalog);
            BuilderTools.SetReference(appearance, "previewCharacter", female);
            BuilderTools.SetReference(appearance, "visualRoot", visual);
            var driver = player.GetComponent<CharacterAnimationDriver>();
            BuilderTools.SetReference(
                driver,
                "animator",
                visual.GetComponentInChildren<Animator>()
            );
            female.FitController(player.GetComponent<CharacterController>());
            if (rig != null)
                PlayerSandboxBuilder.ConnectPlayerAndCamera(player, rig.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(appearance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(
                player.GetComponent<CharacterController>()
            );
        }

        internal static void BuildCatalog()
        {
            BuilderTools.EnsureFolder(DataFolder);
            BuilderTools.EnsureFolder(VisualFolder);
            GameObject femaleVisual = CreateVisual("Female", LittleMungStairBuilder.PrefabPath);
            var maleVisual = BuilderTools.LoadRequiredAsset<GameObject>(
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHeadProportionBuilder.PrefabPath)
                != null
                    ? PlayerHeadProportionBuilder.PrefabPath
                    : MaleVisualPath,
                "Male master visual"
            );
            PlayerCharacterDefinition female = ConnectDefinition("F", femaleVisual);
            PlayerCharacterDefinition male = ConnectDefinition("M", maleVisual);
            var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(CatalogPath);
            if (catalog != null)
                return;
            catalog = ScriptableObject.CreateInstance<PlayerCharacterCatalog>();
            var data = new SerializedObject(catalog);
            data.FindProperty("defaultCharacter").objectReferenceValue = female;
            SerializedProperty entries = data.FindProperty("characters");
            entries.arraySize = 2;
            entries.GetArrayElementAtIndex(0).objectReferenceValue = female;
            entries.GetArrayElementAtIndex(1).objectReferenceValue = male;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        private static GameObject CreateVisual(string name, string sourcePath)
        {
            string path = VisualFolder + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;
            GameObject source = PrefabUtility.LoadPrefabContents(sourcePath);
            GameObject copy = null;
            try
            {
                copy = Object.Instantiate(source.transform.Find("Visual").gameObject);
                copy.name = name;
                foreach (
                    var visibility in copy.GetComponentsInChildren<BlockoutPlayerVisibility>(true)
                )
                    Object.DestroyImmediate(visibility);
                return PrefabUtility.SaveAsPrefabAsset(copy, path);
            }
            finally
            {
                if (copy != null)
                    Object.DestroyImmediate(copy);
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        // 외형 연결은 매번 맞춘다. 캡슐·시점 수치는 처음 만들 때만 정하고 이후에는 Fit 메뉴로 맞춘다.
        private static PlayerCharacterDefinition ConnectDefinition(string id, GameObject visual)
        {
            string path = DataFolder + "/" + id + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<PlayerCharacterDefinition>(path);
            bool created = definition == null;
            if (created)
                definition = ScriptableObject.CreateInstance<PlayerCharacterDefinition>();
            var data = new SerializedObject(definition);
            data.FindProperty("characterId").stringValue = id;
            data.FindProperty("visualPrefab").objectReferenceValue = visual;
            if (created)
            {
                var source = AssetDatabase
                    .LoadAssetAtPath<GameObject>(LittleMungStairBuilder.PrefabPath)
                    .GetComponent<CharacterController>();
                data.FindProperty("heightMeters").floatValue = source.height;
                data.FindProperty("radiusMeters").floatValue = source.radius;
                data.FindProperty("stepHeightMeters").floatValue = source.stepOffset;
                data.FindProperty("skinWidthMeters").floatValue = source.skinWidth;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            if (created)
                AssetDatabase.CreateAsset(definition, path);
            else
                EditorUtility.SetDirty(definition);
            return definition;
        }
    }
}
