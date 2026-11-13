using System;
using System.Collections.Generic;
using System.Linq;
using A305.EditorTools.Common;
using A305.ObjectHover;
using UnityEditor;
using UnityEngine;

namespace A305.EditorTools.ObjectHover
{
    /// <summary>
    /// 실내 소품의 의미를 연결한다. 외형·배치는 유지하고, 합쳐진 메시의 세부 단어는 기존 Parts 방식으로 관리한다.
    /// </summary>
    internal static class IndoorObjectWordBuilder
    {
        private const string PartsFolder = "Assets/Data/ObjectHover/Parts";
        private static readonly Dictionary<string, ObjectWordDefinition> words = new();

        private static readonly Dictionary<string, string> ObjectWords = new()
        {
            ["BakeryOven"] = "oven",
            ["FlourSack"] = "flour_bag",
            ["WateringCan"] = "watering_can",
            ["WrappingRoll"] = "wrapping_paper",
            ["Bottle"] = "bottle",
            ["BarberTools"] = "barber_tools",
            ["CoatRack"] = "coat_rack",
            ["Broom"] = "broom",
            ["TowelStack"] = "rolled_towels",
            ["FoldedTowel"] = "folded_towels",
            ["BarberStation"] = "cabinet",
            ["ToolTrolley"] = "tool_cart",
            ["MagazineRack"] = "magazine_rack",
            ["Nightstand"] = "nightstand",
            ["NoticeBoard"] = "bulletin_board",
            ["BarberPole"] = "barber_pole",
            ["Burger"] = "burger",
            ["CuttingBoard"] = "cutting_board",
            ["RangeHood"] = "range_hood",
            ["CookingPot"] = "cooking_pot",
            ["FryingPan"] = "frying_pan",
            ["Jukebox"] = "jukebox",
            ["PaymentTerminal"] = "card_reader",
            ["FryerBody"] = "deep_fryer",
            ["FryerBasket"] = "deep_fryer",
            ["FryerHandle"] = "deep_fryer",
            ["GriddleBase"] = "griddle",
            ["GriddleTop"] = "griddle",
            ["MailCart"] = "mail_cart",
            ["DeskMonitor"] = "monitor",
            ["Monitor"] = "monitor",
            ["WaterCooler"] = "water_cooler",
            ["Stanchion"] = "queue_barrier",
            ["QueueRope"] = "queue_barrier",
            ["Scale"] = "scale",
            ["Badge"] = "badge",
            ["BrochureRack"] = "brochure_rack",
            ["TableLeg"] = "table",
            ["MenuHanger"] = "menu",
            ["BurgerBadge"] = "sign",
            ["PillBottle"] = "pill_bottle",
            ["MedicineBox"] = "medicine_box",
            ["HotDogRoller"] = "hot_dog_roller",
            ["UmbrellaBin"] = "umbrella_stand",
            ["BaguetteBasket"] = "basket",
            ["BunCrate"] = "wooden_crate",
            ["WoodCrate"] = "wooden_crate",
            ["ProduceCrate"] = "wooden_crate",
            ["Bouquet"] = "bouquet",
        };

        // Kenney 모델 이름은 외형으로 확인할 수 있는 상품에만 연결한다. 무표시 병·봉지의 내용물은 추측하지 않는다.
        private static readonly Dictionary<string, string> ProductWords = new()
        {
            ["cup"] = "cup",
            ["mug"] = "mug",
            ["hotDog"] = "hot_dog",
            ["candyBarWrapper"] = "candy_bar",
            ["chocolateWrapper"] = "chocolate_bar",
            ["bag"] = "snack_bag",
            ["bagFlat"] = "snack_bag",
            ["cookie"] = "cookie",
            ["lollypop"] = "lollipop",
            ["iceCream"] = "ice_cream",
            ["can"] = "can",
            ["canSmall"] = "can",
            ["carton"] = "carton",
            ["cartonSmall"] = "carton",
            ["bottleKetchup"] = "ketchup",
            ["honey"] = "honey",
            ["peanutButter"] = "peanut_butter",
            ["styrofoam"] = "takeout_container",
            ["apple"] = "apple",
            ["orange"] = "orange",
            ["banana"] = "banana",
            ["carrot"] = "carrot",
            ["cabbage"] = "cabbage",
            ["corn"] = "corn",
            ["broccoli"] = "broccoli",
        };

        internal static void Apply(GameObject root)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name.StartsWith("__", StringComparison.Ordinal))
                    continue;
                string name = item.name;
                int suffix = name.LastIndexOf(" (", StringComparison.Ordinal);
                if (suffix >= 0)
                    name = name.Substring(0, suffix);

                if (ObjectWords.TryGetValue(name, out string word))
                    Bind(item, word);
                if (ProductWords.TryGetValue(name, out word) && IsProductModel(item))
                    Bind(item, word);
                if (name.StartsWith("QueueLine", StringComparison.Ordinal))
                    Bind(item, "queue_barrier");
                if (
                    name == "FrontTrim"
                    && item.parent != null
                    && item.parent.name == "OrderCounter"
                )
                    Bind(item, "counter");
                if (name == "DetergentBottles")
                    Bind(
                        item,
                        HasAncestor(item, "BarberShopInterior") ? "bottle" : "laundry_detergent"
                    );
                if (name == "Cabinet" && item.parent != null && item.parent.name == "CoffeeCounter")
                    Bind(item, "cabinet");
                if (name == "Umbrella" && item.parent != null && item.parent.name == "UmbrellaBin")
                    Bind(item, "umbrella");

                ApplyDetails(item, name);
            }
        }

        private static bool HasAncestor(Transform item, string name)
        {
            for (Transform parent = item.parent; parent != null; parent = parent.parent)
                if (parent.name == name)
                    return true;
            return false;
        }

        private static bool IsProductModel(Transform item)
        {
            // 같은 이름을 쓴 다른 공간 장식에는 상품 의미를 덮어쓰지 않는다.
            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>(true))
            {
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (
                    path.StartsWith("Assets/ThirdParty/KenneyFoodKit/", StringComparison.Ordinal)
                    || path == "Assets/ThirdParty/KenneyFurnitureKit/kitchenCoffeeMachine.obj"
                )
                    return true;
            }
            return false;
        }

        private static ObjectWordDefinition Word(string file)
        {
            if (!words.TryGetValue(file, out ObjectWordDefinition definition) || definition == null)
            {
                definition = ObjectWordAuthoring.LoadWord(file);
                words[file] = definition;
            }
            return definition;
        }

        private static void Bind(Transform item, string file)
        {
            HoverableObject target = BuilderTools.GetOrAddComponent<HoverableObject>(
                item.gameObject
            );
            if (target.Word != Word(file))
                BuilderTools.SetReference(target, "word", Word(file));
        }

        private static void ApplyDetails(Transform item, string name)
        {
            switch (name)
            {
                case "BarberTools":
                    Details(
                        item,
                        name,
                        (mesh, bounds) =>
                        {
                            if (bounds.center.x < -.1f)
                                return "hair_clippers";
                            if (bounds.center.x > .19f)
                                return "hair_dryer";
                            return mesh == "CombBlack" ? "comb" : "spray_bottle";
                        }
                    );
                    break;
                case "BarberStation":
                    Details(
                        item,
                        name,
                        (mesh, bounds) =>
                            bounds.center.z > .25f && bounds.center.y < .8f ? "drawer" : null
                    );
                    break;
                case "ToolTrolley":
                    Details(
                        item,
                        name,
                        (mesh, _) =>
                            mesh switch
                            {
                                "TrolleyRedLight" => "drawer",
                                "SoftWhite" => "folded_towels",
                                _ => null,
                            }
                    );
                    break;
                case "ShampooSink":
                    Details(
                        item,
                        name,
                        (mesh, _) =>
                            mesh switch
                            {
                                "SprayTeal" or "BottleAmber" => "bottle",
                                "InteriorSteel" => "faucet",
                                "DarkWood" => "cabinet",
                                _ => null,
                            }
                    );
                    break;
                case "HoodDryerChair":
                    Details(
                        item,
                        name,
                        (mesh, bounds) =>
                            mesh == "DryerCream" || (mesh == "InteriorSteel" && bounds.max.y > 1.3f)
                                ? "hooded_hair_dryer"
                                : null
                    );
                    break;
                case "CoatRack":
                    Details(
                        item,
                        name,
                        (mesh, _) =>
                            mesh switch
                            {
                                "CoatNavy" => "coat",
                                "Leather" => "hat",
                                _ => null,
                            }
                    );
                    break;
                case "BarbicideJar":
                    Details(item, name, (mesh, _) => mesh == "CombBlack" ? "comb" : null);
                    break;
                case "MagazineRack":
                    Details(item, name, (mesh, _) => mesh.StartsWith("Cover") ? "magazine" : null);
                    break;
                case "Nightstand":
                    Details(
                        item,
                        name,
                        (mesh, _) => mesh == "Nightstand_02_Door" ? "drawer" : null
                    );
                    break;
                case "BaguetteBasket":
                    Details(item, name, (mesh, _) => mesh == "BaguetteCrust" ? "baguette" : null);
                    break;
                case "BunCrate":
                    // KayKit의 번과 깨는 작은 분리된 모양이고, 상자 판자는 가로 또는 세로로 길다.
                    Details(
                        item,
                        name,
                        (mesh, bounds) =>
                            bounds.size.x < .4f && bounds.size.z < .4f && bounds.center.y > .05f
                                ? "bun"
                                : null
                    );
                    break;
                case "Armchair":
                    // 이 모델의 왼쪽 베개만 의자와 분리된 메시 조각이다.
                    Details(
                        item,
                        "ArmchairPillows",
                        (mesh, bounds) =>
                            mesh == "armchair_pillows" && bounds.center.x < -.1f ? "pillow" : null
                    );
                    break;
                case "SortingCubbies":
                case "MailCart":
                    Details(
                        item,
                        name,
                        (mesh, _) =>
                            mesh switch
                            {
                                "Envelope" => "envelope",
                                "Cardboard" => "package",
                                _ => null,
                            }
                    );
                    break;
                case "FormDesk":
                    Details(
                        item,
                        name,
                        (mesh, _) =>
                            mesh switch
                            {
                                "Envelope" => "form",
                                "Leather" => "pen",
                                _ => null,
                            }
                    );
                    break;
                case "DeskMonitor":
                    Details(item, name, (mesh, _) => mesh == "KeyboardGrey" ? "keyboard" : null);
                    break;
                case "BrochureRack":
                    Details(item, name, (mesh, _) => mesh.StartsWith("Cover") ? "brochure" : null);
                    break;
                case "LaundryBasket":
                case "LaundryCart":
                    Details(
                        item,
                        name,
                        (mesh, _) => mesh.StartsWith("Towel") ? "folded_towels" : null
                    );
                    break;
                case "ClothesRack":
                    Details(
                        item,
                        name,
                        (mesh, _) =>
                        {
                            if (mesh.StartsWith("Shirt"))
                                return "shirt";
                            return mesh == "DarkWood" ? "clothes_hanger" : null;
                        }
                    );
                    break;
                case "NoticeBoard":
                    Details(item, name, (mesh, _) => mesh.StartsWith("Note") ? "note" : null);
                    break;
                case "PlantShelf":
                    Details(
                        item,
                        name,
                        (mesh, _) =>
                        {
                            if (mesh.StartsWith("Petal"))
                                return "flower";
                            return mesh switch
                            {
                                "Terracotta" => "flowerpot",
                                "LeafGreen" => "plant",
                                _ => null,
                            };
                        }
                    );
                    break;
                case "HangingPlant":
                    Details(item, name, (mesh, _) => mesh == "Terracotta" ? "flowerpot" : null);
                    break;
                case "FlowerCooler":
                    Details(
                        item,
                        name,
                        (mesh, bounds) =>
                        {
                            string flower = FlowerWord(mesh);
                            if (flower != null)
                                return flower;
                            if (
                                mesh == "InteriorSteel"
                                && bounds.size.y > .2f
                                && bounds.size.x > .2f
                            )
                                return "bucket";
                            return null;
                        }
                    );
                    break;
                case "FlowerBucket":
                    Bind(item, "bucket");
                    Details(
                        item,
                        name,
                        (mesh, _) => mesh == "Stem" || mesh == "Bloom" ? "flower" : null
                    );
                    break;
                case "Bouquet":
                    Details(
                        item,
                        name,
                        (mesh, _) =>
                        {
                            if (mesh.StartsWith("Ribbon"))
                                return "ribbon";
                            return mesh == "WrapPaper" ? "wrapping_paper" : null;
                        }
                    );
                    break;
                case "WrappingRoll":
                    Details(item, name, (mesh, _) => mesh.StartsWith("Ribbon") ? "ribbon" : null);
                    break;
            }

            if (name.StartsWith("FlowerPail", StringComparison.Ordinal))
            {
                Bind(item, "bucket");
                Details(item, name, (mesh, _) => FlowerWord(mesh));
            }
            if (name.StartsWith("Vase", StringComparison.Ordinal))
                Details(item, name, (mesh, _) => FlowerWord(mesh));
        }

        private static string FlowerWord(string mesh) =>
            mesh.StartsWith("Petal", StringComparison.Ordinal) || mesh == "FlowerStem"
                ? "flower"
                : null;

        private static void Details(
            Transform item,
            string profileName,
            Func<string, Bounds, string> chooseWord
        )
        {
            HoverableObject target = item.GetComponent<HoverableObject>();
            if (target == null || target.Parts != null)
                return;
            // 더 이상 존재하지 않는 프로필 참조도 빈 값으로 정리한다.
            BuilderTools.SetReference(target, "parts", null);
            string path = PartsFolder + "/Indoor" + profileName + ".asset";
            var regionsByWord = new Dictionary<string, List<Bounds>>();
            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<TMPro.TMP_Text>() != null)
                    continue;
                Matrix4x4 matrix = item.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds[] regions = ObjectWordMeshRegions
                    .Split(filter.sharedMesh)
                    .Select(region => TransformBounds(region, matrix))
                    .ToArray();
                string[] selectedWords = regions
                    .Select(region => chooseWord(filter.name, region))
                    .ToArray();
                string[] distinctWords = selectedWords.Distinct().ToArray();
                // 화병의 꽃·선반의 상품처럼 자체 메시가 있는 물건은 독립 연결한다.
                // 주변 가구의 표면이 같은 영역에 겹쳐도 그 가구를 상품으로 오인하지 않는다.
                if (
                    filter.transform != item
                    && distinctWords.Length == 1
                    && distinctWords[0] != null
                )
                {
                    Bind(filter.transform, distinctWords[0]);
                    continue;
                }
                for (int regionIndex = 0; regionIndex < regions.Length; regionIndex++)
                {
                    string word = selectedWords[regionIndex];
                    if (word == null)
                        continue;
                    Bounds region = regions[regionIndex];
                    region.Expand(.006f);
                    if (!regionsByWord.TryGetValue(word, out List<Bounds> wordRegions))
                    {
                        wordRegions = new List<Bounds>();
                        regionsByWord.Add(word, wordRegions);
                    }
                    wordRegions.Add(region);
                }
            }
            if (regionsByWord.Count == 0)
                return;
            var profile = AssetDatabase.LoadAssetAtPath<ObjectWordPartsDefinition>(path);
            if (profile == null)
            {
                BuilderTools.EnsureFolder(PartsFolder);
                profile = ScriptableObject.CreateInstance<ObjectWordPartsDefinition>();
                var fields = new SerializedObject(profile);
                SerializedProperty parts = fields.FindProperty("parts");
                parts.arraySize = regionsByWord.Count;
                int index = 0;
                foreach (var entry in regionsByWord)
                {
                    SerializedProperty part = parts.GetArrayElementAtIndex(index++);
                    part.FindPropertyRelative("label").stringValue = Word(
                        entry.Key
                    ).Details.MeaningKo;
                    part.FindPropertyRelative("word").objectReferenceValue = Word(entry.Key);
                    SerializedProperty regions = part.FindPropertyRelative("regions");
                    regions.arraySize = entry.Value.Count;
                    for (int region = 0; region < entry.Value.Count; region++)
                        regions.GetArrayElementAtIndex(region).boundsValue = entry.Value[region];
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(profile, path);
            }
            // 기존 디자인에서 직접 지정한 세부 영역은 덮어쓰지 않는다.
            if (target.Parts == null)
                BuilderTools.SetReference(target, "parts", profile);
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                var signs = new Vector3(
                    (corner & 1) == 0 ? -1 : 1,
                    (corner & 2) == 0 ? -1 : 1,
                    (corner & 4) == 0 ? -1 : 1
                );
                result.Encapsulate(
                    matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, signs))
                );
            }
            return result;
        }
    }
}
