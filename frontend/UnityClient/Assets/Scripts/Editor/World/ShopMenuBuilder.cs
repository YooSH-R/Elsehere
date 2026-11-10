using System.Linq;
using A305.EditorTools.Common;
using TMPro;
using UnityEditor;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;
using Object = UnityEngine.Object;

namespace A305.EditorTools.World
{
    /// <summary>주문할 음식과 변경 가능한 재료를 카운터에서 읽을 수 있게 배치한다.</summary>
    public static class ShopMenuBuilder
    {
        private const string BurgerPrefab = TownStorefrontBuilder.Folder + "/MapleDiner.prefab";

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
                }
            );
            BuilderTools.UpdatePrefabIfExists(BurgerPrefab, ApplyBurgerShop);
            BuilderTools.UpdatePrefabIfExists(
                TownShopInteriorBuilder.Folder + "/MapleDinerInterior.prefab",
                root =>
                {
                    Transform pieBoard = root.transform.Find("Booths/PieBoard");
                    if (pieBoard != null)
                        Object.DestroyImmediate(pieBoard.gameObject);
                }
            );
            UpdateBurgerDialogue();
            TownStorefrontBuilder.RenameDinerLabels();
            AssetDatabase.SaveAssets();
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
                "AMERICANO  $4\nLATTE  $5\nCAPPUCCINO  $5\nMUFFIN  $3",
                new Vector2(2.12f, -.02f),
                new Vector2(1.95f, 1.15f),
                .20f
            );
            Label(
                menu,
                "Options",
                "HOT / ICED\nLARGE +$1\nREGULAR / SOY MILK\nEXTRA SHOT +$1",
                new Vector2(0, -.02f),
                new Vector2(2.05f, 1.15f),
                .18f
            );
            Label(
                menu,
                "TryOrdering",
                "TRY SAYING\nSoy milk, please.\nNo sugar, please.\nAn extra shot, please.",
                new Vector2(-2.12f, -.02f),
                new Vector2(1.95f, 1.15f),
                .18f
            );
            Label(
                menu,
                "Footer",
                "FOR HERE OR TO GO?  ·  SMALL PRICES SHOWN  ·  SOY MILK FREE",
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
                else if (child.name == "DisplayCase" || child.name == "ChocolateCake" || child.name == "CoffeeMachine")
                    Object.DestroyImmediate(child.gameObject);
                else if (child.name == "Mug")
                {
                    var cupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Prefabs/World/TinyTreats/BakeryInterior/TakeawayCup.prefab");
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
                    new Vector3(x, 3.75f, -6.35f),
                    new Vector3(.035f, .15f, .035f),
                    Navy
                );
            Transform board = Panel(
                menus,
                "BurgerMenu",
                new Vector3(0, 2.65f, -6.35f),
                0,
                6.8f,
                2.05f,
                "#542E26"
            );
            Label(
                board,
                "Title",
                "MAPLE BURGER  /  PICK YOUR BURGER",
                new Vector2(0, .76f),
                new Vector2(6.3f, .35f),
                .28f
            );
            Burger(
                board,
                "Classic",
                "CLASSIC  $7",
                "Beef · lettuce · tomato\nonion · pickles · ketchup",
                1.65f,
                .25f
            );
            Burger(
                board,
                "Cheese",
                "CHEESE  $9",
                "Beef · cheese · lettuce\nonion · pickles · ketchup",
                -1.65f,
                .25f
            );
            Burger(
                board,
                "Chicken",
                "CRISPY CHICKEN  $8",
                "Chicken · lettuce · mayo",
                1.65f,
                -.42f
            );
            Burger(
                board,
                "Veggie",
                "VEGGIE  $8",
                "Bean patty · lettuce · tomato\nonion · ketchup",
                -1.65f,
                -.42f
            );
            Label(
                board,
                "Sides",
                "FRIES $3  ·  COLA / LEMONADE $2  ·  FRIES + DRINK COMBO +$4",
                new Vector2(0, -.87f),
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
                "ADD\nCheese +$1  /  Patty +$2",
                new Vector2(0, .24f),
                new Vector2(2.5f, .52f),
                .18f
            );
            Label(
                choices,
                "Remove",
                "LEAVE OUT · FREE\nOnion / pickles / sauce",
                new Vector2(0, -.29f),
                new Vector2(2.5f, .5f),
                .18f
            );
            Label(
                choices,
                "Say",
                "No onions, please.\nCan I add cheese?",
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
            float x,
            float y
        )
        {
            Label(
                board,
                name + "Title",
                title,
                new Vector2(x, y + .15f),
                new Vector2(3, .28f),
                .22f
            );
            Label(
                board,
                name + "Ingredients",
                ingredients,
                new Vector2(x, y - .15f),
                new Vector2(3, .38f),
                .16f
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
