using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using A305.EditorTools.Common;
using A305.ObjectHover;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace A305.EditorTools.ObjectHover
{
    public static class ObjectWordAuthoring
    {
        public const string WordsFolder = "Assets/Data/ObjectHover/Words";
        private const string CatalogRelativePath =
            "backend/src/main/resources/content/object-words.json";
        private const string BindingsRelativePath =
            "backend/src/main/resources/content/object-word-bindings.json";
        private const string SeedRelativePath = "backend/src/main/resources/db/seed/world.sql";
        internal static readonly string[] SceneNames =
        {
            "HomeScene",
            "CafeScene",
            "HotelScene",
            "TownScene",
            "ShopScene",
            "PoliceStationScene",
        };
        private static string RepositoryPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));

        /// <summary>
        /// 사물 단어를 등록·내보내는 씬. 장소 씬과 마을 매장 실내 씬이며, 실내 씬은 매장이 늘어도 빠지지 않게 폴더에서 찾는다.
        /// </summary>
        public static string[] WordScenePaths() =>
            SceneNames.Select(PlaceScenePath).Concat(TownInteriorScenePaths()).ToArray();

        /// <summary>
        /// 씬에서 사물을 담은 루트. 장소 씬은 Environment이고, 매장 실내 씬은 마을 씬에 덧붙일 때 통째로 옮기는 첫 루트(ShopInterior)다.
        /// </summary>
        internal static GameObject WordRoot(Scene scene, string path) =>
            IsTownInterior(path)
                ? scene.GetRootGameObjects()[0]
                : scene.GetRootGameObjects().Single(root => root.name == "Environment");

        private static string PlaceScenePath(string name) =>
            "Assets/Scenes/Production/" + name + ".unity";

        // 마을 매장 실내 씬. 실행 중 마을 씬에 덧붙여 불러오므로(ShopInterior) 사물 카드는 마을(TOWN) 단어로 받는다.
        private static string[] TownInteriorScenePaths() =>
            AssetDatabase
                .FindAssets("t:Scene", new[] { A305.EditorTools.World.ShopInteriorScenes.Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

        internal static bool IsTownInterior(string path) =>
            path.StartsWith(
                A305.EditorTools.World.ShopInteriorScenes.Folder + "/",
                StringComparison.Ordinal
            );

        public static void EnsureIdentity(ObjectWordDefinition definition)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition));
            if (string.IsNullOrEmpty(guid))
                return;
            var fields = new SerializedObject(definition);
            SerializedProperty owner = fields.FindProperty("assetGuid");
            SerializedProperty key = fields.FindProperty("details.key");
            // 이전 JSON의 공용 키는 유지하고, 새 에셋과 복제본만 자체 GUID로 키를 만든다.
            if (
                string.IsNullOrWhiteSpace(key.stringValue)
                || (!string.IsNullOrEmpty(owner.stringValue) && owner.stringValue != guid)
            )
                key.stringValue = "word_" + guid;
            owner.stringValue = guid;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        // 이미 있는 공용 단어를 쓴다.
        public static ObjectWordDefinition LoadWord(string file)
        {
            var word = AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(
                $"{WordsFolder}/{file}.asset"
            );
            if (word == null)
                throw new InvalidOperationException("공용 단어가 없습니다: " + file);
            return word;
        }

        // Builder가 새로 놓는 사물의 단어는 뜻·예문·예문 해석과 함께 없을 때만 만든다.
        public static ObjectWordDefinition FindOrCreateWord(
            string file,
            string text,
            string meaningKo,
            string example,
            string exampleKo
        )
        {
            string path = $"{WordsFolder}/{file}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(path);
            if (definition != null)
                return definition;
            definition = ScriptableObject.CreateInstance<ObjectWordDefinition>();
            var fields = new SerializedObject(definition);
            fields.FindProperty("details.text").stringValue = text;
            fields.FindProperty("details.meaningKo").stringValue = meaningKo;
            fields.FindProperty("details.example").stringValue = example;
            fields.FindProperty("details.exampleKo").stringValue = exampleKo;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, path);
            EnsureIdentity(definition);
            return definition;
        }

        public static ObjectWordDefinition[] LoadDefinitions(bool assignIdentity = true)
        {
            var definitions = AssetDatabase
                .FindAssets("t:ObjectWordDefinition", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>)
                .ToArray();
            if (assignIdentity)
                foreach (ObjectWordDefinition definition in definitions)
                    EnsureIdentity(definition);
            return definitions
                .OrderBy(definition => definition.Key, StringComparer.Ordinal)
                .ToArray();
        }

        public static CatalogObjectWordSource RefreshSource()
        {
            ObjectWordDefinition[] definitions = LoadDefinitions();
            ObjectWordExport.SerializeWords(definitions);
            var source = BuilderTools.LoadOrCreateAsset<CatalogObjectWordSource>(
                ObjectWordRegistration.SourceAssetPath
            );
            var fields = new SerializedObject(source);
            SerializedProperty entries = fields.FindProperty("words");
            entries.arraySize = definitions.Length;
            for (int index = 0; index < definitions.Length; index++)
                entries.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            fields.ApplyModifiedPropertiesWithoutUndo();
            return source;
        }

        [MenuItem("Tools/A305/ObjectHover/Migrate Legacy JSON to Word Assets")]
        public static void MigrateAndRegister()
        {
            if (!BuilderTools.CanBuild())
                return;
            MigrateLegacyCatalog();
            ObjectWordRegistration.Build();
            AssetDatabase.DeleteAsset("Assets/Data/ObjectHover/ObjectWords.json");
            AssetDatabase.SaveAssets();
        }

        public static void MigrateLegacyCatalog()
        {
            string json = File.ReadAllText(Path.Combine(RepositoryPath, CatalogRelativePath));
            ObjectWordCatalog legacy = ObjectWordCatalog.Parse(json);
            var existingKeys = new HashSet<string>(
                LoadDefinitions().Select(word => word.Key),
                StringComparer.Ordinal
            );
            BuilderTools.EnsureFolder(WordsFolder);
            foreach (ObjectWordDetails word in legacy.Words)
            {
                if (existingKeys.Contains(word.Key))
                    continue;
                var definition = ScriptableObject.CreateInstance<ObjectWordDefinition>();
                JsonUtility.FromJsonOverwrite(
                    "{\"details\":" + JsonUtility.ToJson(word) + "}",
                    definition
                );
                string path = WordsFolder + "/" + word.Key + ".asset";
                if (File.Exists(path))
                    throw new InvalidOperationException("단어 에셋 경로가 이미 있습니다: " + path);
                AssetDatabase.CreateAsset(definition, path);
                EnsureIdentity(definition);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/A305/ObjectHover/Export Backend Data")]
        public static void Export() => WriteOutputs(false);

        [MenuItem("Tools/A305/ObjectHover/Check Backend Export")]
        public static void CheckExports() => WriteOutputs(true);

        private static void WriteOutputs(bool check)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play 종료 후 단어 데이터를 내보내세요.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("열려 있는 씬을 저장한 뒤 내보내세요.");

            if (!check)
                RefreshSource();
            ObjectWordDefinition[] definitions = LoadDefinitions(!check);
            string json = ObjectWordExport.SerializeWords(definitions);
            var source = AssetDatabase.LoadAssetAtPath<CatalogObjectWordSource>(
                ObjectWordRegistration.SourceAssetPath
            );
            if (
                source == null
                || !new HashSet<ObjectWordDefinition>(source.Words).SetEquals(definitions)
            )
                throw new InvalidOperationException(
                    "공용 단어 목록 참조가 오래됐습니다. Export Backend Data를 실행하세요."
                );
            string bindings = JsonUtility.ToJson(CollectBindings(definitions), true) + "\n";
            string seedPath = Path.Combine(RepositoryPath, SeedRelativePath);
            string seed = File.ReadAllText(seedPath).Replace("\r\n", "\n");
            const string pattern =
                @"(?s)-- BEGIN GENERATED OBJECT WORDS:.*?-- END GENERATED OBJECT WORDS";
            if (Regex.Matches(seed, pattern).Count != 1)
                throw new InvalidOperationException(
                    "world.sql의 공용 어휘 생성 구간이 하나여야 합니다."
                );
            string sql = Regex.Replace(
                seed,
                pattern,
                _ => ObjectWordExport.BuildSqlSection(definitions)
            );

            var outputs = new Dictionary<string, string>
            {
                [CatalogRelativePath] = json,
                [BindingsRelativePath] = bindings,
                [SeedRelativePath] = sql,
            };
            // 모든 단어·프리팹·씬을 검증한 후에만 전달 파일을 갱신한다.
            if (!check)
                AssetDatabase.SaveAssets();
            foreach (var output in outputs)
            {
                string path = Path.Combine(RepositoryPath, output.Key);
                if (check)
                {
                    if (
                        !File.Exists(path)
                        || File.ReadAllText(path).Replace("\r\n", "\n") != output.Value
                    )
                        throw new InvalidOperationException(
                            "내보낸 파일이 최신 에셋과 다릅니다: " + output.Key
                        );
                }
                else
                    File.WriteAllText(path, output.Value, new UTF8Encoding(false));
            }
            Debug.Log(
                (check ? "검증" : "내보내기")
                    + " 완료: 공용 단어 "
                    + definitions.Length
                    + "개, 배포용 씬 "
                    + WordScenePaths().Length
                    + "개"
            );
        }

        private static BindingsDocument CollectBindings(ObjectWordDefinition[] definitions)
        {
            var known = new HashSet<ObjectWordDefinition>(definitions);
            var prefabs = new List<PrefabWords>();
            foreach (
                string path in AssetDatabase
                    .FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(ObjectWordInventory.IsEnvironment)
                    .OrderBy(path => path, StringComparer.Ordinal)
            )
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                HoverableObject[] targets = prefab.GetComponentsInChildren<HoverableObject>(true);
                if (targets.Length == 0)
                    continue;
                prefabs.Add(
                    new PrefabWords
                    {
                        path = path,
                        guid = AssetDatabase.AssetPathToGUID(path),
                        wordKeys = WordKeys(targets, known, path),
                    }
                );
            }
            var scenes = new List<SceneWords>();
            foreach (string name in SceneNames)
            {
                string sceneCode = A305.World.PlaceScenes.CodeOf(name);
                // 마을은 실행 중 마을 씬에 덧붙여 불러오는 매장 실내 씬의 사물까지 한 장소로 묶는다.
                string[] paths =
                    sceneCode == "TOWN"
                        ? new[] { PlaceScenePath(name) }.Concat(TownInteriorScenePaths()).ToArray()
                        : new[] { PlaceScenePath(name) };
                var opened = new List<Scene>();
                try
                {
                    var targets = new List<HoverableObject>();
                    foreach (string path in paths)
                    {
                        Scene scene = EditorSceneManager.OpenPreviewScene(path);
                        opened.Add(scene);
                        targets.AddRange(
                            BoundTargets(
                                WordRoot(scene, path),
                                Path.GetFileNameWithoutExtension(path)
                            )
                        );
                    }
                    scenes.Add(
                        new SceneWords
                        {
                            sceneCode = sceneCode,
                            unityKey = name,
                            objectCount = targets.Count,
                            detailCount = targets.Sum(target =>
                                target.Parts == null ? 0 : target.Parts.Parts.Count
                            ),
                            wordKeys = WordKeys(targets, known, name),
                        }
                    );
                }
                finally
                {
                    foreach (Scene scene in opened)
                        EditorSceneManager.ClosePreviewScene(scene);
                }
            }
            return new BindingsDocument { scenes = scenes.ToArray(), prefabs = prefabs.ToArray() };
        }

        // 보이는 사물에 단어 연결이 빠졌으면 멈추고, 연결된 사물을 돌려준다.
        private static HoverableObject[] BoundTargets(GameObject root, string location)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
                if (
                    renderer.enabled
                    && renderer.GetComponent<TMPro.TMP_Text>() == null
                    && renderer.GetComponentInParent<HoverableObject>() == null
                )
                    throw new InvalidOperationException(
                        location + ": 단어 연결이 없는 사물 " + renderer.name
                    );
            return root.GetComponentsInChildren<HoverableObject>();
        }

        private static string[] WordKeys(
            IEnumerable<HoverableObject> targets,
            HashSet<ObjectWordDefinition> known,
            string location
        )
        {
            var words = new HashSet<string>(StringComparer.Ordinal);
            foreach (HoverableObject target in targets)
            {
                if (target.Word == null || !known.Contains(target.Word))
                    throw new InvalidOperationException(
                        location + ": 공용 단어 에셋 참조 누락 " + target.name
                    );
                words.Add(target.Word.Key);
                if (target.Parts == null)
                    continue;
                foreach (ObjectWordPartsDefinition.Part part in target.Parts.Parts)
                {
                    if (part.Word == null || !known.Contains(part.Word) || part.Regions.Count == 0)
                        throw new InvalidOperationException(
                            location + ": 세부 단어·영역 누락 " + part.Label
                        );
                    foreach (Bounds region in part.Regions)
                        if (region.size.x <= 0 || region.size.y <= 0 || region.size.z <= 0)
                            throw new InvalidOperationException(
                                location + ": 세부 영역 크기는 양수여야 합니다."
                            );
                    words.Add(part.Word.Key);
                }
            }
            return words.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        }

        [Serializable]
        private sealed class BindingsDocument
        {
            public int schemaVersion = 1;
            public SceneWords[] scenes;
            public PrefabWords[] prefabs;
        }

        [Serializable]
        private sealed class SceneWords
        {
            public string sceneCode;
            public string unityKey;
            public int objectCount;
            public int detailCount;
            public string[] wordKeys;
        }

        [Serializable]
        private sealed class PrefabWords
        {
            public string guid;
            public string path;
            public string[] wordKeys;
        }
    }
}
