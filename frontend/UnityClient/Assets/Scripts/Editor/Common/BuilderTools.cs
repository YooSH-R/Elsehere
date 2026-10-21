using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Common
{
    /// <summary>
    /// 씬·프리팹·머티리얼을 만드는 에셋 Builder들이 함께 쓰는 생성·저장 도구.
    /// 디자인 작업이 들어갈 에셋은 덮어쓰지 않도록, 대체 에셋은 없을 때만 만들고 기능 구성만 다시 맞춘다.
    /// </summary>
    public static class BuilderTools
    {
        /// <summary>규약의 최상위 그룹 이름. 씬 루트에 이 순서로 둔다.</summary>
        public static readonly string[] GroupNames =
        {
            "Systems",
            "Environment",
            "Characters",
            "UI",
            "Lighting",
        };

        /// <summary>Play Mode가 아니고, 열린 씬의 변경을 저장하거나 버리기로 했으면 true.</summary>
        public static bool CanBuild()
        {
            ThrowIfPlaying();
            return Application.isBatchMode || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        public static void ThrowIfPlaying()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Play Mode를 끝낸 뒤 실행합니다.");
            }
        }

        /// <summary>규약의 최상위 그룹을 가진 새 씬을 만든다.</summary>
        public static SceneGroups NewSceneWithGroups()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single
            );
            return EnsureGroups(scene);
        }

        /// <summary>
        /// 씬 루트에 없는 그룹을 만든다. 루트의 기본 카메라는 플레이어 카메라로 대신하므로 지우고,
        /// 루트의 조명은 Lighting 그룹으로 옮긴다. 그 밖의 객체는 건드리지 않는다.
        /// </summary>
        public static SceneGroups EnsureGroups(Scene scene)
        {
            Transform[] groups = GroupNames.Select(name => FindOrCreateRoot(scene, name)).ToArray();
            var sceneGroups = new SceneGroups(scene, groups[0], groups[1], groups[2], groups[3], groups[4]);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (GroupNames.Contains(root.name))
                {
                    continue;
                }

                if (root.GetComponent<Camera>() != null)
                {
                    Object.DestroyImmediate(root);
                }
                else if (root.GetComponent<Light>() != null)
                {
                    root.transform.SetParent(sceneGroups.Lighting, true);
                }
            }

            return sceneGroups;
        }

        /// <summary>씬 안의 <typeparamref name="T"/>를 찾는다. 없으면 null, 둘 이상이면 어느 것을 쓸지 몰라 실패한다.</summary>
        public static T FindSingleInScene<T>(Scene scene)
            where T : Component
        {
            T[] components = FindAllInScene<T>(scene);
            if (components.Length > 1)
            {
                throw new InvalidOperationException(
                    $"{scene.name} 씬에 {typeof(T).Name}가 {components.Length}개 있습니다. 하나만 둡니다."
                );
            }

            return components.Length == 1 ? components[0] : null;
        }

        public static T[] FindAllInScene<T>(Scene scene)
            where T : Component
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        public static void SaveScene(Scene scene, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException($"씬을 저장하지 못했습니다: {path}");
            }
        }

        /// <summary>Scene List 끝에 씬을 추가한다. 이미 있으면 순서를 바꾸지 않는다.</summary>
        public static void AddToSceneList(string scenePath)
        {
            if (EditorBuildSettings.scenes.Any(entry => entry.path == scenePath))
            {
                return;
            }

            ReplaceSceneList(
                EditorBuildSettings
                    .scenes.Select(entry => (entry.path, entry.enabled))
                    .Append((scenePath, true))
            );
        }

        /// <summary>
        /// <paramref name="leadingScenePaths"/>를 Scene List 앞에 이 순서로 두고, 이미 등록된 다른 씬은 그 뒤에 그대로 둔다.
        /// </summary>
        public static void PutFirstInSceneList(IReadOnlyCollection<string> leadingScenePaths)
        {
            IEnumerable<(string, bool)> others = EditorBuildSettings
                .scenes.Where(entry => !leadingScenePaths.Contains(entry.path))
                .Select(entry => (entry.path, entry.enabled));
            ReplaceSceneList(leadingScenePaths.Select(path => (path, true)).Concat(others));
        }

        public static Transform CreateEmpty(
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation
        )
        {
            var empty = new GameObject(objectName);
            empty.transform.SetParent(parent, false);
            empty.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            return empty.transform;
        }

        public static Transform CreateShape(
            PrimitiveType type,
            string shapeName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider = true
        )
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = shapeName;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = localPosition;
            shape.transform.localScale = localScale;
            shape.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (!keepCollider)
            {
                Object.DestroyImmediate(shape.GetComponent<Collider>());
            }

            return shape.transform;
        }

        public static T GetOrAddComponent<T>(GameObject owner)
            where T : Component
        {
            if (owner.TryGetComponent(out T component))
            {
                return component;
            }

            return owner.AddComponent<T>();
        }

        /// <summary>
        /// 대체 에셋용 URP Lit 머티리얼을 <paramref name="folder"/>에 만든다.
        /// 이미 있으면 디자인에서 바꾼 색·셰이더를 유지하도록 그대로 돌려준다.
        /// </summary>
        public static Material GetOrCreateMaterial(string folder, string materialName, Color color)
        {
            string path = $"{folder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                throw new InvalidOperationException("URP Lit 셰이더를 찾지 못했습니다.");
            }

            EnsureFolder(folder);
            material = new Material(litShader);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>데이터 에셋을 불러온다. 없으면 기본값으로 만들고, 있으면 수치를 그대로 둔다.</summary>
        public static T LoadOrCreateAsset<T>(string path)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>다른 Builder가 만든 에셋을 불러온다. 없으면 먼저 실행할 메뉴를 알린다.</summary>
        public static T LoadRequiredAsset<T>(string path, string builderMenu)
            where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"{path}가 없습니다. 먼저 {builderMenu}를 실행합니다."
                );
            }

            return asset;
        }

        /// <summary>
        /// 대체 에셋 프리팹을 없을 때만 만든다. 이미 있으면 디자인 작업이 들어갔을 수 있으므로 그대로 돌려준다.
        /// </summary>
        public static GameObject CreatePrefabIfMissing(string path, Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            return SavePrefab(build(), path);
        }

        /// <summary>
        /// 기능 프리팹을 만들거나, 이미 있으면 열어서 <paramref name="setUp"/>으로 기능 구성만 다시 맞춘다.
        /// <paramref name="setUp"/>은 이미 있는 컴포넌트·자식을 다시 만들지 않아야 하며, 모델을 두는 Visual 아래는 건드리지 않는다.
        /// </summary>
        public static GameObject UpdateOrCreatePrefab(
            string path,
            string rootName,
            Action<GameObject> setUp
        )
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var root = new GameObject(rootName);
                setUp(root);
                return SavePrefab(root, path);
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                setUp(contents);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(contents, path, out bool isSaved);
                if (!isSaved)
                {
                    throw new InvalidOperationException($"프리팹을 저장하지 못했습니다: {path}");
                }

                return prefab;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 프리팹으로 저장하고 원본 객체를 지운다. 같은 경로의 프리팹은 이름 기준으로 덮어써서
        /// 씬이 참조하는 내부 객체 ID가 유지된다.
        /// </summary>
        public static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool isSaved);
            Object.DestroyImmediate(root);

            if (!isSaved)
            {
                throw new InvalidOperationException($"프리팹을 저장하지 못했습니다: {path}");
            }

            return prefab;
        }

        public static GameObject InstantiatePrefab(
            GameObject prefab,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation
        )
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            return instance;
        }

        public static void SetReference(Object owner, string propertyName, Object value)
        {
            var fields = new SerializedObject(owner);
            FindRequiredProperty(fields, propertyName).objectReferenceValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetText(Object owner, string propertyName, string value)
        {
            var fields = new SerializedObject(owner);
            FindRequiredProperty(fields, propertyName).stringValue = value;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>참조 필드가 하나라도 비어 있으면 실패시켜, 연결이 빠진 씬·프리팹을 만들지 않게 한다.</summary>
        public static void RequireReferences(Object owner)
        {
            SerializedProperty property = new SerializedObject(owner).GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                bool isEmptyReference =
                    property.propertyType == SerializedPropertyType.ObjectReference
                    && property.name != "m_Script"
                    && property.objectReferenceValue == null;
                if (isEmptyReference)
                {
                    throw new InvalidOperationException(
                        $"{owner.GetType().Name}의 {property.name}가 연결되지 않았습니다."
                    );
                }
            }
        }

        public static void EnsureFolder(string folderPath)
        {
            string normalizedPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(normalizedPath))
            {
                return;
            }

            string parentPath = Path.GetDirectoryName(normalizedPath).Replace('\\', '/');
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, Path.GetFileName(normalizedPath));
        }

        private static void ReplaceSceneList(IEnumerable<(string Path, bool IsEnabled)> entries)
        {
            EditorBuildSettingsScene[] scenes = entries
                .Select(entry => new EditorBuildSettingsScene(entry.Path, entry.IsEnabled))
                .ToArray();
            // 경로가 같은 목록을 넣으면 Unity가 이전 GUID를 그대로 두므로, 다시 만든 씬의 GUID가 반영되도록 비운 뒤 넣는다.
            EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
            EditorBuildSettings.scenes = scenes;
        }

        private static Transform FindOrCreateRoot(Scene scene, string rootName)
        {
            GameObject existing = scene
                .GetRootGameObjects()
                .FirstOrDefault(root => root.name == rootName);
            if (existing != null)
            {
                return existing.transform;
            }

            var created = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created.transform;
        }

        private static SerializedProperty FindRequiredProperty(
            SerializedObject fields,
            string propertyName
        )
        {
            SerializedProperty property = fields.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{fields.targetObject.GetType().Name}에 {propertyName} 필드가 없습니다."
                );
            }

            return property;
        }
    }

    /// <summary>규약의 최상위 그룹을 가진 씬.</summary>
    public readonly struct SceneGroups
    {
        public SceneGroups(
            Scene scene,
            Transform systems,
            Transform environment,
            Transform characters,
            Transform ui,
            Transform lighting
        )
        {
            Scene = scene;
            Systems = systems;
            Environment = environment;
            Characters = characters;
            UI = ui;
            Lighting = lighting;
        }

        public Scene Scene { get; }

        public Transform Systems { get; }

        public Transform Environment { get; }

        public Transform Characters { get; }

        public Transform UI { get; }

        public Transform Lighting { get; }
    }
}
