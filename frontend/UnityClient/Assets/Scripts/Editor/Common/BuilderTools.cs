using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace A305.EditorTools.Common
{
    /// <summary>씬·프리팹·머티리얼을 만드는 에셋 Builder들이 함께 쓰는 생성·저장 도구.</summary>
    public static class BuilderTools
    {
        /// <summary>Play Mode가 아니고, 열린 씬의 변경을 저장하거나 버리기로 했으면 true.</summary>
        public static bool CanBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Play Mode를 끝낸 뒤 Builder를 실행합니다.");
            }

            return Application.isBatchMode || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        /// <summary>
        /// 규약의 최상위 그룹(Systems·Environment·Characters·UI·Lighting)을 가진 새 씬을 만든다.
        /// 기본 카메라는 지우고 기본 조명은 Lighting 그룹으로 옮긴다.
        /// </summary>
        public static SceneGroups NewSceneWithGroups()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single
            );
            var groups = new SceneGroups(
                scene,
                CreateGroup("Systems"),
                CreateGroup("Environment"),
                CreateGroup("Characters"),
                CreateGroup("UI"),
                CreateGroup("Lighting")
            );

            foreach (GameObject defaultObject in scene.GetRootGameObjects())
            {
                if (defaultObject.GetComponent<Camera>() != null)
                {
                    Object.DestroyImmediate(defaultObject);
                }
                else if (defaultObject.GetComponent<Light>() != null)
                {
                    defaultObject.transform.SetParent(groups.Lighting, true);
                }
            }

            return groups;
        }

        public static void SaveScene(Scene scene, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException($"씬을 저장하지 못했습니다: {path}");
            }
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

        /// <summary>URP Lit 머티리얼을 <paramref name="folder"/>에 만들고 색을 맞춘다. 이미 있으면 색만 바꾼다.</summary>
        public static Material GetOrCreateMaterial(string folder, string materialName, Color color)
        {
            string path = $"{folder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null)
                {
                    throw new InvalidOperationException("URP Lit 셰이더를 찾지 못했습니다.");
                }

                EnsureFolder(folder);
                material = new Material(litShader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
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

        /// <summary>다른 Builder가 만든 에셋을 불러온다. 없으면 먼저 실행할 Builder를 알린다.</summary>
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

        private static Transform CreateGroup(string groupName)
        {
            return new GameObject(groupName).transform;
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
