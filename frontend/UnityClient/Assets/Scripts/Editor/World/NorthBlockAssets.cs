using System;
using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.ObjectHover;
using A305.ObjectHover;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    internal static class NorthBlockAssets
    {
        internal const string Folder = "Assets/Prefabs/World/NorthBlock";
        private const string Sources = "Assets/ThirdParty/FabNorthBlock";
        private const string Art = "Assets/Art/World/NorthBlock";

        internal static void Prepare()
        {
            BuilderTools.EnsureFolder(Folder);
            AddWord(
                "claw_machine",
                "claw machine",
                "인형 뽑기 기계",
                "Can I try this claw machine?",
                "이 인형 뽑기 기계를 해 봐도 될까요?"
            );
            AddWord("toy", "toy", "장난감", "I like this toy.", "이 장난감이 마음에 들어요.");
            AddWord(
                "game_console",
                "game console",
                "게임기",
                "Where is the game console?",
                "게임기는 어디에 있나요?"
            );
            ObjectWordRegistration.PrepareSource();
            Prop("ClawMachine", "ClawMachine/Toy_Claw_01.fbx", 2.1f, "claw_machine");
            Prop("Bear", "ClawMachine/Bear_01.fbx", .25f, "toy");
            Prop("Rabbit", "ClawMachine/Rabbit_01.fbx", .25f, "toy");
            Prop("Armchair", "CuteFurniture/Armchair_02.fbx", .9f, "chair");
            Prop("Bed", "CuteFurniture/Bed_02.fbx", .9f, "bed");
            Prop("Book", "CuteFurniture/Book_03.fbx", .08f, "book");
            Prop("Chair", "CuteFurniture/Chair_17.fbx", .9f, "chair");
            Prop("Cabinet", "CuteFurniture/Closet_01.fbx", 1.8f, "cabinet");
            Prop("CoffeeTable", "CuteFurniture/Coffee_Table_03.fbx", .5f, "table");
            Prop("Sofa", "CuteFurniture/Couch_08.fbx", .95f, "sofa");
            Prop("GameConsole", "CuteFurniture/GameConsole_01.fbx", .25f, "game_console");
            Prop("Table", "CuteFurniture/Kitchen_Table_09.fbx", .75f, "table");
            Prop("Lamp", "CuteFurniture/Light_05.fbx", .55f, "lamp");
            Prop("Nightstand", "CuteFurniture/Nightstand_02.fbx", .6f, "drawer");
            Prop("Plant", "CuteFurniture/Plants_05.fbx", .8f, "plant");
            Prop("Toy", "CuteFurniture/Toy_02.fbx", .3f, "toy");
            PrepareDetails();
        }

        private static void PrepareDetails()
        {
            // Regions are measured from the disconnected pieces of the normalized source meshes.
            Parts(
                "Plant",
                new[]
                {
                    (
                        "화분",
                        "flowerpot",
                        new Vector3(-.003f, .146f, 0),
                        new Vector3(.413f, .293f, .413f)
                    ),
                }
            );
            Parts(
                "Armchair",
                new[]
                {
                    (
                        "방석",
                        "cushion",
                        new Vector3(-.004f, .519f, .106f),
                        new Vector3(.771f, .256f, .771f)
                    ),
                }
            );
            Parts(
                "Sofa",
                new[]
                {
                    (
                        "방석",
                        "cushion",
                        new Vector3(-.004f, .445f, .091f),
                        new Vector3(1.983f, .220f, .661f)
                    ),
                    (
                        "등 쿠션",
                        "cushion",
                        new Vector3(-.003f, .726f, -.154f),
                        new Vector3(1.983f, .447f, .247f)
                    ),
                }
            );
            Parts(
                "Bed",
                new[]
                {
                    (
                        "왼쪽 베개",
                        "pillow",
                        new Vector3(-.269f, .550f, -.450f),
                        new Vector3(.501f, .155f, .395f)
                    ),
                    (
                        "오른쪽 베개",
                        "pillow",
                        new Vector3(.269f, .550f, -.450f),
                        new Vector3(.501f, .155f, .395f)
                    ),
                    (
                        "이불",
                        "duvet",
                        new Vector3(0, .463f, .247f),
                        new Vector3(1.309f, .153f, 1.008f)
                    ),
                    (
                        "매트리스",
                        "mattress",
                        new Vector3(0, .423f, -.367f),
                        new Vector3(1.210f, .132f, .637f)
                    ),
                    (
                        "침대 머리판",
                        "headboard",
                        new Vector3(0, .450f, -.753f),
                        new Vector3(1.416f, .900f, .105f)
                    ),
                    (
                        "수납 서랍",
                        "drawer",
                        new Vector3(0, .226f, .023f),
                        new Vector3(1.43f, .226f, 1.39f)
                    ),
                }
            );
        }

        private static void Parts(
            string name,
            (string label, string key, Vector3 center, Vector3 size)[] parts
        )
        {
            string path = "Assets/Data/ObjectHover/Parts/NorthBlock" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<ObjectWordPartsDefinition>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ObjectWordPartsDefinition>();
                var fields = new SerializedObject(profile);
                SerializedProperty entries = fields.FindProperty("parts");
                entries.arraySize = parts.Length;
                for (int index = 0; index < parts.Length; index++)
                {
                    var entry = entries.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("label").stringValue = parts[index].label;
                    entry.FindPropertyRelative("word").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(
                            ObjectWordAuthoring.WordsFolder + "/" + parts[index].key + ".asset"
                        );
                    var regions = entry.FindPropertyRelative("regions");
                    regions.arraySize = 1;
                    regions.GetArrayElementAtIndex(0).boundsValue = new Bounds(
                        parts[index].center,
                        parts[index].size + Vector3.one * .012f
                    );
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(profile, path);
            }
            BuilderTools.UpdatePrefabIfExists(
                Folder + "/Props/" + name + ".prefab",
                root =>
                {
                    HoverableObject hover = root.GetComponent<HoverableObject>();
                    if (hover.Parts == null)
                        BuilderTools.SetReference(hover, "parts", profile);
                }
            );
        }

        private static void Prop(string name, string source, float height, string word)
        {
            BuilderTools.CreatePrefabIfMissing(
                Folder + "/Props/" + name + ".prefab",
                () =>
                {
                    var root = new GameObject(name);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Sources + "/" + source);
                    if (model == null)
                        throw new InvalidOperationException("Missing downloaded model: " + source);
                    GameObject visual = BuilderTools.InstantiatePrefab(
                        model,
                        root.transform,
                        Vector3.zero,
                        Quaternion.identity
                    );
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterials = renderer
                            .sharedMaterials.Select(material =>
                                Palette(source, material != null ? material.name : "")
                            )
                            .ToArray();
                    Bounds bounds = BoundsOf(root);
                    visual.transform.localScale *= height / bounds.size.y;
                    bounds = BoundsOf(root);
                    visual.transform.localPosition -= new Vector3(
                        bounds.center.x,
                        bounds.min.y,
                        bounds.center.z
                    );
                    bounds = BoundsOf(root);
                    var collider = root.AddComponent<BoxCollider>();
                    collider.center = bounds.center;
                    collider.size = bounds.size;
                    Bind(root, word);
                    return root;
                }
            );
        }

        private static Material Palette(string source, string materialName)
        {
            bool furniture = source.StartsWith("CuteFurniture/");
            bool glass = materialName.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0;
            string texture = furniture
                ? "CuteFurniture/Textures_4.png"
                : "ClawMachine/textures/Color.png";
            if (!furniture)
            {
                foreach (string part in new[] { "Cat", "Paw", "Prize" })
                    if (materialName.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                        texture = "ClawMachine/textures/" + part + ".png";
                if (materialName.IndexOf("colorset", StringComparison.OrdinalIgnoreCase) >= 0)
                    texture = "ClawMachine/textures/Colorset_03.png";
            }
            string name = (furniture ? "Furniture" : "Claw") + "_" + materialName;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(Art + "/" + name + ".mat");
            if (existing != null)
                return existing;
            Material result = BuilderTools.GetOrCreateMaterial(
                Art,
                name,
                glass ? new Color(.82f, .94f, .93f, .12f) : Color.white
            );
            result.SetFloat("_Smoothness", glass ? .45f : .16f);
            result.enableInstancing = true;
            if (glass)
            {
                result.SetFloat("_Surface", 1);
                result.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                result.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                result.SetFloat("_ZWrite", 0);
                result.SetOverrideTag("RenderType", "Transparent");
                result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                result.renderQueue = (int)RenderQueue.Transparent;
            }
            else
                result.SetTexture(
                    "_BaseMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(Sources + "/" + texture)
                );
            return result;
        }

        internal static void Bind(GameObject target, string key)
        {
            var word = AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(
                ObjectWordAuthoring.WordsFolder + "/" + key + ".asset"
            );
            if (word == null)
                throw new InvalidOperationException("Missing object word: " + key);
            var hover = BuilderTools.GetOrAddComponent<HoverableObject>(target);
            BuilderTools.SetReference(hover, "word", word);
        }

        private static void AddWord(
            string key,
            string text,
            string meaning,
            string example,
            string translation
        )
        {
            string path = ObjectWordAuthoring.WordsFolder + "/" + key + ".asset";
            if (AssetDatabase.LoadAssetAtPath<ObjectWordDefinition>(path) != null)
                return;
            var word = ScriptableObject.CreateInstance<ObjectWordDefinition>();
            var fields = new SerializedObject(word);
            fields.FindProperty("details.text").stringValue = text;
            fields.FindProperty("details.meaningKo").stringValue = meaning;
            fields.FindProperty("details.example").stringValue = example;
            fields.FindProperty("details.exampleKo").stringValue = translation;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(word, path);
            ObjectWordAuthoring.EnsureIdentity(word);
        }

        private static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        internal static Transform Place(
            string name,
            Transform parent,
            Vector3 position,
            float yaw = 0
        )
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "/Props/" + name + ".prefab"
            );
            return BuilderTools
                .InstantiatePrefab(prefab, parent, position, Quaternion.Euler(0, yaw, 0))
                .transform;
        }
    }
}
