using System.Linq;
using A305.EditorTools.Common;
using A305.EditorTools.Npc;
using A305.EditorTools.ObjectHover;
using A305.Npc;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>주문할 음식과 변경 가능한 재료를 카운터에서 읽을 수 있게 배치한다.</summary>
    public static class ShopMenuBuilder
    {
        private const string BurgerPrefab = TownStorefrontBuilder.Folder + "/MapleDiner.prefab";

        [MenuItem("Tools/A305/World/Apply Burger Table Words")]
        public static void ApplyBurgerTableWords()
        {
            BuilderTools.ThrowIfPlaying();
            NorthBlockAssets.AddBurgerTableWords();
            ObjectWordRegistration.PrepareSource();

            foreach (string name in new[] { "Ketchup", "Mustard", "NapkinDispenser" })
            {
                string key = name == "NapkinDispenser" ? "napkin" : name.ToLowerInvariant();
                string path = TownShopInteriorProps.Folder + "/" + name + ".prefab";
                BuilderTools.UpdatePrefabIfExists(
                    path,
                    root =>
                    {
                        NorthBlockAssets.Bind(root, key);
                        ObjectWordRegistration.ApplyToPrefab(root, path);
                    }
                );
            }

            string interiorPath = TownShopInteriorBuilder.Folder + "/MapleDinerInterior.prefab";
            BuilderTools.UpdatePrefabIfExists(
                interiorPath,
                root =>
                {
                    foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    {
                        string key =
                            item.name == "NapkinDispenser" ? "napkin"
                            : item.name == "Ketchup" ? "ketchup"
                            : item.name == "Mustard" ? "mustard"
                            : null;
                        if (key != null)
                            NorthBlockAssets.Bind(item.gameObject, key);
                    }
                    ObjectWordRegistration.ApplyToPrefab(root, interiorPath);
                }
            );
            AssetDatabase.SaveAssets();
            ObjectWordAuthoring.Export();
        }

        [MenuItem("Tools/A305/World/Apply Cafe And Burger Menus")]
        public static void Apply()
        {
            BuilderTools.ThrowIfPlaying();
            BuilderTools.UpdatePrefabIfExists(
                CafeInteriorKit.Folder + "/MenuBoard.prefab",
                root =>
                {
                    foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                        Object.DestroyImmediate(child.gameObject);
                    BuildCafeMenu(root.transform);
                    ObjectWordRegistration.ApplyToPrefab(
                        root,
                        CafeInteriorKit.Folder + "/MenuBoard.prefab"
                    );
                }
            );
            BuilderTools.UpdatePrefabIfExists(
                BurgerPrefab,
                root =>
                {
                    ApplyBurgerShop(root);
                    ArrangeBurgerShop(root);
                    RestoreDiningCounter(root.transform.Find("Interior"));
                    BuildOrderCounter(root.transform.Find("Interior"));
                    ObjectWordRegistration.ApplyToPrefab(root, BurgerPrefab);
                }
            );
            BuilderTools.UpdatePrefabIfExists(
                TownShopInteriorBuilder.Folder + "/MapleDinerInterior.prefab",
                root =>
                {
                    Transform pieBoard = root.transform.Find("Booths/PieBoard");
                    if (pieBoard != null)
                        Object.DestroyImmediate(pieBoard.gameObject);
                    ArrangeBurgerFurnishings(root.transform);
                    ArrangeDiningCounterProps(root.transform.Find("CounterTop"));
                    ObjectWordRegistration.ApplyToPrefab(
                        root,
                        TownShopInteriorBuilder.Folder + "/MapleDinerInterior.prefab"
                    );
                }
            );
            UpdateBurgerDialogue();
            TownStorefrontBuilder.RenameDinerLabels();
            ApplyBurgerSceneLayout();
        }

        private static void ArrangeBurgerShop(GameObject shop)
        {
            Transform interior = shop.transform.Find("Interior");
            if (interior.Find("BurgerLayoutVersion") != null)
                return;
            foreach (Transform child in interior.Cast<Transform>())
            {
                Vector3 position = child.localPosition;
                Vector3 scale = child.localScale;
                if (child.name == "Floor" || child.name == "Ceiling")
                {
                    position.z = -5;
                    scale.z = 1.25f;
                }
                else if (child.name == "FloorSurface")
                {
                    position.z = -5;
                    scale.y = 9.5f;
                }
                else if (child.name == "InteriorWall" || child.name == "Skirting")
                {
                    if (Mathf.Abs(position.x) > 3)
                    {
                        position.z = -5;
                        scale.z = 1.25f;
                    }
                    else if (position.z < -7)
                        position.z -= 2;
                }
                else if (
                    child.name == "DinerTable"
                    || child.name == "TableLeg"
                    || child.name == "BoothSeat"
                    || child.name == "BoothBack"
                    || (child.name == "Plate" && position.x > 2)
                    || (child.name == "TakeawayCup" && position.x > 2)
                )
                {
                    position.z += position.z > -4.2f ? 1f : 1.4f;
                }
                child.localPosition = position;
                child.localScale = scale;
            }

            Transform floor = shop
                .transform.Cast<Transform>()
                .First(part => part.name.Contains("GroundFloor8m"));
            Transform walls = floor.Find("HollowWalls");
            foreach (Transform wall in walls.Cast<Transform>())
            {
                if (wall.name == "SideWall")
                {
                    wall.localPosition = new Vector3(
                        wall.localPosition.x,
                        wall.localPosition.y,
                        -5
                    );
                    wall.localScale = new Vector3(1, 1, 1.25f);
                }
                else if (wall.name == "BackWall")
                    wall.localPosition = new Vector3(0, wall.localPosition.y, -9.9f);
            }
            Group("BurgerLayoutVersion", interior, Vector3.zero);
        }

        private static void RestoreDiningCounter(Transform interior)
        {
            // 식사용 긴 테이블과 네 자리는 옆 벽에 유지하고 주문대와 분리한다.
            foreach (string name in new[] { "DinerCounter", "CounterTop" })
            {
                Transform table = interior.Find(name);
                table.localPosition = new Vector3(-3, table.localPosition.y, -3.5f);
                table.localRotation = Quaternion.identity;
            }
            int stoolIndex = 0;
            int cupIndex = 0;
            foreach (Transform child in interior)
            {
                if (child.name == "Stool")
                {
                    child.localPosition = new Vector3(-2.05f, .74f, -1.6f - stoolIndex * 1.2f);
                    child.localRotation = Quaternion.identity;
                    stoolIndex++;
                }
                else if (child.name == "TakeawayCup" && child.localPosition.x < 2)
                {
                    child.localPosition = new Vector3(-2.75f, 1.06f, -2.2f - cupIndex * 1.2f);
                    cupIndex++;
                }
            }
        }

        private static void BuildOrderCounter(Transform interior)
        {
            Transform previous = interior.Find("OrderCounter");
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);
            Transform counter = Group("OrderCounter", interior, new Vector3(.5f, 0, -6.2f));
            NorthBlockInteriorBuilder.Solid(
                "CounterBody",
                counter,
                new Vector3(0, .47f, 0),
                new Vector3(4.6f, .94f, .85f),
                Color("DinerRed", "#C8463B"),
                "counter"
            );
            NorthBlockInteriorBuilder.Solid(
                "Worktop",
                counter,
                new Vector3(0, .97f, 0),
                new Vector3(4.72f, .06f, .95f),
                Cream,
                "counter",
                false
            );
            Block(
                "FrontTrim",
                counter,
                new Vector3(0, .78f, .435f),
                new Vector3(4.5f, .08f, .025f),
                Color("BurgerBun", "#F2B64E")
            );

            var registerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/World/TinyTreats/BakeryInterior/CashRegister.prefab"
            );
            GameObject register = BuilderTools.InstantiatePrefab(
                registerPrefab,
                counter,
                new Vector3(-.85f, 1, 0),
                Quaternion.Euler(0, 180, 0)
            );
            register.transform.localScale = Vector3.one * 1.6f;

            Transform terminal = Group("PaymentTerminal", counter, new Vector3(-.25f, 1, .15f));
            Block("Body", terminal, new Vector3(0, .06f, 0), new Vector3(.19f, .12f, .28f), Navy);
            Transform screen = Block(
                "Screen",
                terminal,
                new Vector3(0, .125f, -.03f),
                new Vector3(.14f, .012f, .1f),
                Color("BurgerTerminalScreen", "#8AD0D5")
            );
            screen.localRotation = Quaternion.Euler(-12, 0, 0);
            NorthBlockAssets.Bind(terminal.gameObject, "cash_register");
        }

        private static void ArrangeDiningCounterProps(Transform counter)
        {
            int ketchupIndex = 0;
            int mustardIndex = 0;
            foreach (Transform item in counter)
            {
                float x = -2.8f;
                float z;
                float yaw = 0;
                switch (item.name)
                {
                    case "Ketchup":
                        z = -1.95f - ketchupIndex++ * 2.4f;
                        break;
                    case "Mustard":
                        z = -2.08f - mustardIndex++ * 2.4f;
                        break;
                    case "NapkinDispenser":
                        z = -3.15f;
                        yaw = 90;
                        break;
                    case "DinerPlate":
                    case "Burger":
                        x = -2.7f;
                        z = -4;
                        break;
                    case "TableMenu":
                        x = -2.85f;
                        z = -5.2f;
                        yaw = 90;
                        break;
                    case "ServiceBell":
                        x = 1.7f;
                        z = -6.1f;
                        break;
                    default:
                        continue;
                }
                float height = item.name == "ServiceBell" ? 1 : item.localPosition.y;
                item.localPosition = new Vector3(x, height, z);
                item.localRotation = Quaternion.Euler(0, yaw, 0);
            }
        }

        private static void ArrangeBurgerFurnishings(Transform room)
        {
            Transform kitchen = room.Find("Kitchen");
            if (kitchen.Find("StaffPlatform") == null)
                NorthBlockInteriorBuilder.Solid(
                    "StaffPlatform",
                    kitchen,
                    new Vector3(0, .22f, -5.15f),
                    new Vector3(5.3f, .44f, 1.2f),
                    Color("BurgerKitchenSteel", "#A9B1B4"),
                    "floor"
                );
            if (room.Find("BurgerLayoutVersion") != null)
                return;
            kitchen.localPosition = new Vector3(0, 0, -2);
            Transform booths = room.Find("Booths");
            foreach (Transform item in booths.Cast<Transform>())
            {
                Vector3 position = item.localPosition;
                position.z += position.z > -4.2f ? 1f : 1.4f;
                item.localPosition = position;
            }

            Transform oldAppliances = kitchen.Find("BurgerAppliances");
            if (oldAppliances != null)
                Object.DestroyImmediate(oldAppliances.gameObject);
            Transform appliances = Group("BurgerAppliances", kitchen, Vector3.zero);
            Material steel = Color("BurgerKitchenSteel", "#A9B1B4");
            Material dark = Color("BurgerGriddle", "#3A4040");
            Block(
                "FryerBody",
                appliances,
                new Vector3(-3.15f, .43f, -7.2f),
                new Vector3(.7f, .86f, .7f),
                steel
            );
            Block(
                "FryerBasket",
                appliances,
                new Vector3(-3.15f, .9f, -7.2f),
                new Vector3(.46f, .14f, .48f),
                dark
            );
            Block(
                "FryerHandle",
                appliances,
                new Vector3(-3.15f, .92f, -6.8f),
                new Vector3(.08f, .07f, .35f),
                steel
            );
            Block(
                "GriddleBase",
                appliances,
                new Vector3(.9f, .95f, -7.25f),
                new Vector3(1.05f, .14f, .58f),
                steel
            );
            Block(
                "GriddleTop",
                appliances,
                new Vector3(.9f, 1.04f, -7.25f),
                new Vector3(.96f, .06f, .5f),
                dark
            );
            NorthBlockAssets.Bind(appliances.gameObject, "stove");
            Group("BurgerLayoutVersion", room, Vector3.zero);
        }

        [MenuItem("Tools/A305/World/Apply Burger Scene Layout")]
        public static void ApplyBurgerSceneLayout()
        {
            BuilderTools.ThrowIfPlaying();
            var town = EditorSceneManager.OpenScene(TownProductionBuilder.ScenePath);
            Transform building = TownProductionBuilder
                .Root(town, "Environment")
                .Find("TownMainstreet/BackgroundBuildings/MapleDiner");
            Transform interior = building.Find("Interior");
            if (interior != null)
            {
                // 새로 추가된 프리팹 자식도 마을에는 남기지 않는다.
                foreach (Transform item in interior.Cast<Transform>().ToArray())
                    Object.DestroyImmediate(item.gameObject);
            }
            Transform gardens = building.parent.parent.Find("StreetGardens");
            foreach (Transform tree in gardens.GetComponentsInChildren<Transform>(true))
            {
                if (tree.name != "GardenTreeV2")
                    continue;
                Vector3 local = building.InverseTransformPoint(tree.position);
                if (Mathf.Abs(local.x) < 5 && local.z < -9.8f && local.z > -12)
                {
                    tree.position += building.TransformDirection(Vector3.back) * 3.5f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tree);
                }
            }
            NpcLifeBuilder.RebakeWalkArea(town);
            EditorSceneManager.SaveScene(town);

            var shop = EditorSceneManager.OpenScene(
                TownShopSceneBuilder.GroundFloorScenePath("MapleDiner")
            );
            building = BuildingStaffBuilder.Building(shop, "MapleDiner");
            foreach (string code in new[] { "DINER_STAFF", "FIRE_CAPTAIN" })
            {
                NpcIdentity npc = BuilderTools
                    .FindAllInScene<NpcIdentity>(shop)
                    .Single(candidate => candidate.Code == code);
                npc.transform.SetPositionAndRotation(
                    building.TransformPoint(BuildingStaffBuilder.BurgerCounterStand),
                    building.rotation
                );
                PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);
                BuildingStaffBuilder.LimitToGroundFloor(shop, code, building.name);
            }
            NpcLifeBuilder.RebakeWalkArea(shop);
            EditorSceneManager.SaveScene(shop);
            AssetDatabase.SaveAssets();
            ObjectWordAuthoring.Export();
        }

        private static void UpdateBurgerDialogue()
        {
            // 확인용 대본도 실제 메뉴와 맞춘다. 다른 NPC의 대본은 그대로 유지한다.
            var defaults =
                ScriptableObject.CreateInstance<A305.Dialogue.MockDialogueScriptSource>();
            var source = new SerializedObject(defaults).FindProperty("entries");
            var asset = AssetDatabase.LoadAssetAtPath<A305.Dialogue.MockDialogueScriptSource>(
                "Assets/Data/Dialogue/MockDialogueScripts.asset"
            );
            var target = new SerializedObject(asset);
            var entries = target.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                string code = entry.FindPropertyRelative("npcCode").stringValue;
                if (code != "DINER_STAFF" && code != "FIRE_CAPTAIN")
                    continue;
                for (int j = 0; j < source.arraySize; j++)
                {
                    var original = source.GetArrayElementAtIndex(j);
                    if (original.FindPropertyRelative("npcCode").stringValue != code)
                        continue;
                    entry.FindPropertyRelative("roleLabel").stringValue = original
                        .FindPropertyRelative("roleLabel")
                        .stringValue;
                    CopyLine(
                        original.FindPropertyRelative("opening"),
                        entry.FindPropertyRelative("opening")
                    );
                    CopyLine(
                        original.FindPropertyRelative("defaultReply"),
                        entry.FindPropertyRelative("defaultReply")
                    );
                    entry.FindPropertyRelative("exampleSentence").stringValue = original
                        .FindPropertyRelative("exampleSentence")
                        .stringValue;
                    var replies = entry.FindPropertyRelative("keywordReplies");
                    var sourceReplies = original.FindPropertyRelative("keywordReplies");
                    replies.arraySize = sourceReplies.arraySize;
                    for (int k = 0; k < replies.arraySize; k++)
                    {
                        var reply = replies.GetArrayElementAtIndex(k);
                        var sourceReply = sourceReplies.GetArrayElementAtIndex(k);
                        var keywords = reply.FindPropertyRelative("keywords");
                        var sourceKeywords = sourceReply.FindPropertyRelative("keywords");
                        keywords.arraySize = sourceKeywords.arraySize;
                        for (int word = 0; word < keywords.arraySize; word++)
                            keywords.GetArrayElementAtIndex(word).stringValue = sourceKeywords
                                .GetArrayElementAtIndex(word)
                                .stringValue;
                        CopyLine(
                            sourceReply.FindPropertyRelative("reply"),
                            reply.FindPropertyRelative("reply")
                        );
                    }
                }
            }
            target.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(defaults);

            var greetings = new SerializedObject(
                AssetDatabase.LoadMainAssetAtPath("Assets/Data/Npc/MockNpcLines.asset")
            );
            entries = greetings.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                string code = entry.FindPropertyRelative("npcCode").stringValue;
                if (code == "DINER_STAFF" || code == "FIRE_CAPTAIN")
                    entry.FindPropertyRelative("greeting").stringValue =
                        "Hi, hon! Welcome to Maple Burger. What can I get you?";
            }
            greetings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CopyLine(SerializedProperty source, SerializedProperty target)
        {
            target.FindPropertyRelative("english").stringValue = source
                .FindPropertyRelative("english")
                .stringValue;
            target.FindPropertyRelative("korean").stringValue = source
                .FindPropertyRelative("korean")
                .stringValue;
            target.FindPropertyRelative("voice").objectReferenceValue = null;
        }

        internal static void BuildCafeMenu(Transform root)
        {
            // 기존 카페 메뉴는 -Z를 향한다. 판과 글자를 같은 묶음에서 돌린다.
            Transform menu = Panel(root, "CoffeeMenu", Vector3.zero, 180, 6.5f, 2.2f, "#24483D");
            Label(menu, "Title", "CORNER CAFE", new Vector2(0, .81f), new Vector2(6, .42f), .31f);
            Label(
                menu,
                "Drinks",
                "AMERICANO\nLATTE\nCAPPUCCINO",
                new Vector2(2.12f, -.02f),
                new Vector2(1.95f, 1.15f),
                .20f
            );
            Label(
                menu,
                "Options",
                "HOT / ICED\nSMALL · MEDIUM · LARGE\nREGULAR / SOY MILK\nEXTRA SHOT AVAILABLE",
                new Vector2(0, -.02f),
                new Vector2(2.05f, 1.15f),
                .18f
            );
            Label(
                menu,
                "TryOrdering",
                "BAKERY\nMUFFIN\n\nFOR HERE OR TO GO",
                new Vector2(-2.12f, -.02f),
                new Vector2(1.95f, 1.15f),
                .18f
            );
            Label(
                menu,
                "Footer",
                "QUEST DRINK  4 COINS  ·  FOR HERE OR TO GO",
                new Vector2(0, -.84f),
                new Vector2(6.05f, .25f),
                .14f
            );
        }

        internal static void ApplyBurgerShop(GameObject root)
        {
            Transform interior = root.transform.Find("Interior");
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "ShopName")
                    label.text = "MAPLE BURGER";
                else if (label.name == "Tagline")
                    label.text = "BURGERS · FRIES · MADE YOUR WAY";
                else if (label.text == "MENU" || label.text.Contains("PANCAKES"))
                    Object.DestroyImmediate(label.gameObject);
            }
            foreach (Transform child in interior.Cast<Transform>().ToArray())
            {
                if (child.name == "BurgerMenus" || child.name == "WallSign")
                    Object.DestroyImmediate(child.gameObject);
                // 판매하지 않는 케이크 전시는 새 버거 메뉴와 혼동되지 않게 뺀다.
                else if (
                    child.name == "DisplayCase"
                    || child.name == "ChocolateCake"
                    || child.name == "CoffeeMachine"
                )
                    Object.DestroyImmediate(child.gameObject);
                else if (child.name == "Mug")
                {
                    var cupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Prefabs/World/TinyTreats/BakeryInterior/TakeawayCup.prefab"
                    );
                    var cup = (GameObject)PrefabUtility.InstantiatePrefab(cupPrefab, interior);
                    cup.name = "TakeawayCup";
                    cup.transform.localPosition = child.localPosition;
                    cup.transform.localRotation = child.localRotation;
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            Transform menus = Group("BurgerMenus", interior, Vector3.zero);
            foreach (float x in new[] { -2.7f, 2.7f })
                Block(
                    "MenuHanger",
                    menus,
                    new Vector3(x, 3.84f, -8.15f),
                    new Vector3(.035f, .08f, .035f),
                    Navy
                );
            Transform board = Panel(
                menus,
                "BurgerMenu",
                new Vector3(0, 3.15f, -8.15f),
                0,
                6.8f,
                1.3f,
                "#542E26"
            );
            Label(
                board,
                "Title",
                "MAPLE BURGER  /  PICK YOUR BURGER",
                new Vector2(0, .43f),
                new Vector2(6.3f, .26f),
                .23f
            );
            Burger(
                board,
                "Classic",
                "CLASSIC",
                "Beef · lettuce · tomato\nOnion · pickles · ketchup",
                2.4f
            );
            Burger(
                board,
                "Cheese",
                "CHEESE",
                "Beef · cheese · lettuce\nOnion · pickles · ketchup",
                .8f
            );
            Burger(board, "Chicken", "CRISPY CHICKEN", "Chicken · lettuce\nMayo", -.8f);
            Burger(
                board,
                "Veggie",
                "VEGGIE",
                "Bean patty · lettuce\nTomato · onion · ketchup",
                -2.4f
            );
            Label(
                board,
                "Sides",
                "FRIES  ·  COLA / LEMONADE  ·  QUEST MEAL + DRINK  7 COINS",
                new Vector2(0, -.46f),
                new Vector2(6.3f, .19f),
                .14f
            );

            Transform choices = Panel(
                menus,
                "OrderOptions",
                new Vector3(-3.69f, 2.4f, -3.8f),
                90,
                2.85f,
                2.1f,
                "#B84732"
            );
            Label(
                choices,
                "Title",
                "MAKE IT YOURS",
                new Vector2(0, .72f),
                new Vector2(2.5f, .32f),
                .25f
            );
            Label(
                choices,
                "Add",
                "ADD\nCheese  /  Extra patty",
                new Vector2(0, .24f),
                new Vector2(2.5f, .52f),
                .18f
            );
            Label(
                choices,
                "Remove",
                "CUSTOMIZE\nOnion / pickles / sauce",
                new Vector2(0, -.29f),
                new Vector2(2.5f, .5f),
                .18f
            );
            Label(
                choices,
                "Say",
                "FRIES  ·  COLA\nFOR HERE OR TO GO",
                new Vector2(0, -.76f),
                new Vector2(2.5f, .4f),
                .18f
            );

            Transform oldBadge = root.transform.Find("BurgerBadge");
            if (oldBadge != null)
                Object.DestroyImmediate(oldBadge.gameObject);
            Transform badge = Group("BurgerBadge", root.transform, new Vector3(-2.65f, 3.2f, .38f));
            Block(
                "TopBun",
                badge,
                new Vector3(0, .14f, 0),
                new Vector3(.54f, .20f, .12f),
                Color("BurgerBun", "#F2B64E"),
                .07f
            );
            Block(
                "Lettuce",
                badge,
                new Vector3(0, .01f, .01f),
                new Vector3(.58f, .055f, .14f),
                Color("BurgerLettuce", "#78A34B"),
                .02f
            );
            Block(
                "Patty",
                badge,
                new Vector3(0, -.07f, .01f),
                new Vector3(.5f, .10f, .12f),
                Color("BurgerPatty", "#613B2A"),
                .03f
            );
            Block(
                "BottomBun",
                badge,
                new Vector3(0, -.17f, 0),
                new Vector3(.54f, .10f, .12f),
                Color("BurgerBun", "#F2B64E"),
                .04f
            );
        }

        private static void Burger(
            Transform board,
            string name,
            string title,
            string ingredients,
            float x
        )
        {
            Label(
                board,
                name + "Title",
                title,
                new Vector2(x, .12f),
                new Vector2(1.5f, .23f),
                .18f
            );
            Label(
                board,
                name + "Ingredients",
                ingredients,
                new Vector2(x, -.15f),
                new Vector2(1.5f, .34f),
                .13f
            );
        }

        private static Transform Panel(
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            float width,
            float height,
            string hex
        )
        {
            Transform panel = Group(name, parent, position);
            panel.localRotation = Quaternion.Euler(0, yaw, 0);
            Block(
                "Frame",
                panel,
                Vector3.zero,
                new Vector3(width, height, .10f),
                Color("MenuFrame", "#F4B544"),
                .04f
            );
            Block(
                "Face",
                panel,
                new Vector3(0, 0, .06f),
                new Vector3(width - .10f, height - .10f, .025f),
                Color("Menu" + name, hex),
                .01f
            );
            NorthBlockAssets.Bind(panel.gameObject, "menu");
            return panel;
        }

        private static void Label(
            Transform panel,
            string name,
            string value,
            Vector2 position,
            Vector2 size,
            float height
        )
        {
            TextMeshPro label = Text(
                name,
                value,
                panel,
                new Vector3(position.x, position.y, .083f),
                height
            );
            label.color = new Color32(255, 243, 218, 255);
            label.rectTransform.sizeDelta = size;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMax = height * 10;
            label.fontSizeMin = height * 9;
            label.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
