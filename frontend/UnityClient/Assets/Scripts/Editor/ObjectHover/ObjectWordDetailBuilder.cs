using System;
using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.ObjectHover;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.ObjectHover
{
    /// <summary>기존 외형은 유지하고, 세부 단어와 편집 가능한 Data 에셋을 처음 연결한다.</summary>
    public static class ObjectWordDetailBuilder
    {
        private const string PartsFolder = "Assets/Data/ObjectHover/Parts";
        private const string Home = "Home/CozyCottage/";
        private static Dictionary<string, ObjectWordDefinition> words;

        [MenuItem("Tools/A305/ObjectHover/Set Up Detailed Words")]
        public static void Build()
        {
            if (!BuilderTools.CanBuild())
                return;
            EnsureWords();
            words = ObjectWordAuthoring.LoadDefinitions().ToDictionary(word => word.Key);
            ObjectWordRegistration.PrepareSource();
            BuilderTools.EnsureFolder(PartsFolder);

            Bind(
                Home + "SageBed",
                "SageBed",
                new[]
                {
                    Part("왼쪽 베개", "pillow", V(-.44f, .78f, -.9f), V(.75f, .22f, .5f)),
                    Part("오른쪽 베개", "pillow", V(.44f, .78f, -.9f), V(.75f, .22f, .5f)),
                    Part("접힌 담요", "blanket", V(0, .82f, .9f), V(1.94f, .11f, .5f)),
                    Part("이불", "duvet", V(0, .7f, .28f), V(1.91f, .23f, 2f)),
                    Part("매트리스", "mattress", V(0, .54f, 0), V(1.85f, .33f, 2.56f)),
                    Part("침대 머리판", "headboard", V(0, .79f, -1.24f), V(2.06f, 1.37f, .29f)),
                }
            );
            Bind(
                Home + "KitchenCounter",
                "KitchenCounter",
                new[]
                {
                    new PartSpec(
                        "수전",
                        "faucet",
                        new Bounds(V(-.57f, 1.25f, -.25f), V(.09f, .42f, .09f)),
                        new Bounds(V(-.57f, 1.43f, -.1f), V(.09f, .09f, .35f))
                    ),
                    Part("싱크대", "sink", V(-.57f, 1.062f, .04f), V(.88f, .065f, .56f)),
                    Part("가스레인지", "stove", V(.78f, 1.063f, 0), V(.85f, .065f, .61f)),
                    Part("하부 수납장", "cabinet", V(0, .47f, .02f), V(2.82f, .92f, .88f)),
                }
            );
            Bind(
                Home + "SageSofa",
                "SageSofa",
                new[]
                {
                    Part("왼쪽 등 쿠션", "cushion", V(-.85f, .96f, -.12f), V(.6f, .6f, .36f)),
                    Part("오른쪽 등 쿠션", "cushion", V(.85f, .96f, -.12f), V(.6f, .6f, .36f)),
                    Part("왼쪽 방석", "cushion", V(-.57f, .58f, .06f), V(1.12f, .27f, .87f)),
                    Part("오른쪽 방석", "cushion", V(.57f, .58f, .06f), V(1.12f, .27f, .87f)),
                }
            );
            var books = new List<PartSpec>();
            for (int row = 0; row < 3; row++)
            for (int book = 0; book < 6; book++)
            {
                float height = .33f + (book % 3) * .055f;
                books.Add(
                    Part(
                        $"책 {row + 1}-{book + 1}",
                        "book",
                        V(-.55f + book * .2f, .17f + row * .64f + height / 2, .07f),
                        V(.16f, height + .02f, .37f)
                    )
                );
            }
            Bind(Home + "OakBookcase", "OakBookcase", books.ToArray());
            Bind(
                Home + "CoffeeTable",
                "CoffeeTable",
                new[] { Part("탁자 위 책", "book", V(.23f, .63f, 0), V(.39f, .075f, .29f)) }
            );
            Bind(
                Home + "SideTable",
                "SideTable",
                new[] { Part("서랍", "drawer", V(0, .4f, .3f), V(.54f, .3f, .14f)) }
            );
            Bind(
                Home + "RetroRefrigerator",
                "RetroRefrigerator",
                new[]
                {
                    Part("자석", "magnet", V(.12f, 1.74f, .51f), V(.075f, .075f, .04f)),
                    Part("메모", "note", V(.12f, 1.64f, .494f), V(.25f, .28f, .03f)),
                    Part("냉동실 손잡이", "handle", V(-.31f, 1.57f, .53f), V(.07f, .2f, .1f)),
                    Part("냉장실 손잡이", "handle", V(-.31f, 1.06f, .53f), V(.07f, .32f, .1f)),
                    Part("냉동실", "freezer", V(0, 1.62f, .435f), V(.9f, .62f, .12f)),
                }
            );
            Bind(
                Home + "CurtainedWindow",
                "CurtainedWindow",
                new[]
                {
                    Part("왼쪽 커튼", "curtain", V(-1.13f, -.05f, .175f), V(.46f, 2.06f, .21f)),
                    Part("오른쪽 커튼", "curtain", V(1.13f, -.05f, .175f), V(.46f, 2.06f, .21f)),
                }
            );
            Bind(
                "Cafe/CornerCafe/BoothSet",
                "CafeBench",
                new[] { Part("벤치 방석", "cushion", V(0, .49f, .03f), V(2.62f, .18f, .84f)) },
                "Bench"
            );
            foreach (string name in new[] { "Monstera", "SnakePlant", "Succulent" })
            {
                string path = "World/TinyTreats/HousePlants/" + name;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/" + path + ".prefab"
                );
                Bounds pot = FindPotBounds(prefab);
                pot.Expand(.012f);
                Bind(path, name, new[] { new PartSpec("화분", "flowerpot", pot) });
                Debug.Log(name + " flowerpot region: " + pot);
            }

            var settings = BuilderTools.LoadOrCreateAsset<ObjectHoverSettings>(
                "Assets/Data/ObjectHover/ObjectHoverSettings.asset"
            );
            EditorUtility.SetDirty(settings);
            ObjectHoverBuilder.PrepareAssets();
            AssetDatabase.SaveAssets();
            ObjectWordRegistration.Build();
        }

        private static void Bind(
            string prefabName,
            string profileName,
            PartSpec[] defaults,
            string childPath = null
        )
        {
            ObjectWordPartsDefinition profile = LoadOrCreateProfile(profileName, defaults);
            BuilderTools.UpdatePrefabIfExists(
                "Assets/Prefabs/" + prefabName + ".prefab",
                root =>
                {
                    GameObject item =
                        childPath == null ? root : root.transform.Find(childPath).gameObject;
                    HoverableObject target = item.GetComponent<HoverableObject>();
                    if (target == null)
                        throw new InvalidOperationException(
                            prefabName + ": 공용 단어 연결이 먼저 필요합니다."
                        );
                    if (target.Parts == null)
                        BuilderTools.SetReference(target, "parts", profile);
                }
            );
        }

        private static ObjectWordPartsDefinition LoadOrCreateProfile(
            string name,
            PartSpec[] defaults
        )
        {
            string path = PartsFolder + "/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<ObjectWordPartsDefinition>(path);
            if (profile != null)
                return profile;
            profile = ScriptableObject.CreateInstance<ObjectWordPartsDefinition>();
            var fields = new SerializedObject(profile);
            SerializedProperty entries = fields.FindProperty("parts");
            entries.arraySize = defaults.Length;
            for (int index = 0; index < defaults.Length; index++)
            {
                PartSpec item = defaults[index];
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("label").stringValue = item.Label;
                entry.FindPropertyRelative("word").objectReferenceValue = words[item.Key];
                SerializedProperty regions = entry.FindPropertyRelative("regions");
                regions.arraySize = item.Regions.Length;
                for (int region = 0; region < item.Regions.Length; region++)
                    regions.GetArrayElementAtIndex(region).boundsValue = item.Regions[region];
            }
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static Bounds FindPotBounds(GameObject root)
        {
            var regions = new List<Bounds>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Matrix4x4 matrix =
                    root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Bounds local in ObjectWordMeshRegions.Split(filter.sharedMesh))
                {
                    var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 offset = Vector3.Scale(
                            local.extents,
                            V(
                                (corner & 1) == 0 ? -1 : 1,
                                (corner & 2) == 0 ? -1 : 1,
                                (corner & 4) == 0 ? -1 : 1
                            )
                        );
                        bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center + offset));
                    }
                    regions.Add(bounds);
                }
            }
            float floor = regions.Min(region => region.min.y);
            // 바닥에 닿는 모양 중 가장 큰 용기를 선택한다. 잎과 줄기는 별도 모양이다.
            return regions
                .Where(region => region.min.y <= floor + .02f)
                .OrderByDescending(region => region.size.x * region.size.y * region.size.z)
                .First();
        }

        private static void EnsureWords()
        {
            AddWord("duvet", "comforter", "이불", "This comforter is warm.", "이 이불은 따뜻해요.");
            AddWord(
                "mattress",
                "mattress",
                "매트리스",
                "This mattress is comfortable.",
                "이 매트리스는 편안해요."
            );
            AddWord(
                "headboard",
                "headboard",
                "침대 머리판",
                "The bed has a wooden headboard.",
                "침대에 나무 머리판이 있어요."
            );
            AddWord(
                "cushion",
                "cushion",
                "쿠션",
                "Put the cushion on the sofa.",
                "쿠션을 소파 위에 놓으세요."
            );
            AddWord(
                "flowerpot",
                "flowerpot",
                "화분",
                "The plant is growing in a flowerpot.",
                "식물이 화분에서 자라고 있어요."
            );
            AddWord(
                "drawer",
                "drawer",
                "서랍",
                "Open the drawer to find a pen.",
                "펜을 찾으려면 서랍을 여세요."
            );
            AddWord(
                "freezer",
                "freezer",
                "냉동실",
                "Keep the ice cream in the freezer.",
                "아이스크림은 냉동실에 보관하세요."
            );
            AddWord(
                "note",
                "note",
                "메모",
                "There is a note on the refrigerator.",
                "냉장고에 메모가 붙어 있어요."
            );
            AddWord(
                "magnet",
                "magnet",
                "자석",
                "The magnet holds the note in place.",
                "자석이 메모를 고정하고 있어요."
            );
            AddWord(
                "handle",
                "handle",
                "손잡이",
                "Pull the handle to open the door.",
                "손잡이를 당겨 문을 여세요."
            );
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
            fields.FindProperty("details.key").stringValue = key;
            fields.FindProperty("details.languageCode").stringValue = "en-US";
            fields.FindProperty("details.text").stringValue = text;
            fields.FindProperty("details.meaningKo").stringValue = meaning;
            fields.FindProperty("details.example").stringValue = example;
            fields.FindProperty("details.exampleKo").stringValue = translation;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(word, path);
            ObjectWordAuthoring.EnsureIdentity(word);
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static PartSpec Part(string label, string key, Vector3 center, Vector3 size) =>
            new PartSpec(label, key, new Bounds(center, size));

        private sealed class PartSpec
        {
            public string Label { get; }
            public string Key { get; }
            public Bounds[] Regions { get; }

            public PartSpec(string label, string key, params Bounds[] regions)
            {
                Label = label;
                Key = key;
                Regions = regions;
            }
        }
    }
}
