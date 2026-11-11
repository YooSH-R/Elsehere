using System;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static A305.EditorTools.World.HomeInteriorKit;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    /// <summary>Corner Cafe의 재사용 가능한 실내 마감과 가구 조합.</summary>
    internal static class CafeInteriorKit
    {
        internal const string Folder = "Assets/Prefabs/Cafe/CornerCafe";

        internal static GameObject Save(string name, Func<GameObject> build, bool combine = false)
        {
            return BuilderTools.CreatePrefabIfMissing(
                Folder + "/" + name + ".prefab",
                () =>
                {
                    GameObject root = build();
                    root.name = name;
                    if (combine)
                        CombineParts(root, "Cafe_" + name);
                    return root;
                }
            );
        }

        internal static Material Finish(string name, string hex)
        {
            const string folder = "Assets/Art/Cafe/CornerCafe";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                folder + "/" + name + ".mat"
            );
            if (material != null)
                return material;
            ColorUtility.TryParseHtmlString(hex, out UnityEngine.Color color);
            material = BuilderTools.GetOrCreateMaterial(folder, name, color);
            material.SetFloat("_Smoothness", .18f);
            material.enableInstancing = true;
            return material;
        }

        internal static Transform Prop(
            string name,
            Transform parent,
            Vector3 position,
            float yaw = 0
        )
        {
            var asset = BuilderTools.LoadRequiredAsset<GameObject>(
                TinyTreatsPropsBuilder.Prefabs + name + ".prefab",
                "Tools/A305/World/Build And Preview Tiny Treats Props"
            );
            return Put(asset, parent, position, yaw);
        }

        internal static GameObject Shell()
        {
            var root = new GameObject("CafeShell");
            Transform floor = Group("Floor", root.transform, Vector3.zero);
            Material tileA = Finish("Limestone", "#EEE6D6");
            Material tileB = Finish("WarmStone", "#D6D7C5");
            for (int x = 0; x < 14; x++)
            for (int z = 0; z < 12; z++)
                Block(
                    "Tile",
                    floor,
                    V(x - 6.5f, -.035f, z - 5.5f),
                    V(.995f, .07f, .995f),
                    (x + z) % 2 == 0 ? tileA : tileB,
                    .012f
                );
            CombineParts(floor.gameObject, "Cafe_CheckerFloor");
            Solid(floor, V(0, -.09f, 0), V(14, .18f, 12));

            Transform north = Group("NorthWall", root.transform, V(0, 0, 6));
            WallPiece(north, "Plaster", V(0, 2.1f, 0), V(14, 4.2f, .2f));
            Trim(north, 14);
            Transform tiles = Group("Backsplash", north, V(0, 0, -.12f));
            for (int row = 0; row < 5; row++)
            for (int col = 0; col < 16; col++)
                Block(
                    "SubwayTile",
                    tiles,
                    V(-4.22f + col * .56f, 1.03f + row * .23f, 0),
                    V(.545f, .215f, .035f),
                    Ivory,
                    .015f
                );
            CombineParts(tiles.gameObject, "Cafe_Backsplash");

            SideWall(root.transform, "WestWall", V(-7, 0, 0), 90);
            SideWall(root.transform, "EastWall", V(7, 0, 0), -90);
            Transform south = Group("SouthWall", root.transform, V(0, 0, -6));
            foreach (int side in new[] { -1, 1 })
            {
                WallPiece(south, "WindowBase", V(side * 4, .4f, 0), V(6, .8f, .2f));
                WallPiece(south, "WindowHeader", V(side * 4, 3.85f, 0), V(6, .7f, .2f));
                WallPiece(south, "Corner", V(side * 6.7f, 2.15f, 0), V(.6f, 2.7f, .2f));
                WallPiece(south, "DoorPier", V(side * 1.3f, 2.15f, 0), V(.6f, 2.7f, .2f));
                Window(south, V(side * 4, 2.15f, 0), 4.8f, 2.7f);
            }
            WallPiece(south, "DoorLintel", V(0, 3.5f, 0), V(2, 1.4f, .2f));
            Transform ceiling = Group("Ceiling", root.transform, V(0, 4.25f, 0));
            Transform slab = Block("Plaster", ceiling, Vector3.zero, V(14, .1f, 12), Ivory);
            slab.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Solid(ceiling, Vector3.zero, V(14, .1f, 12));
            foreach (float z in new[] { -3.4f, .2f, 3.7f })
                Block("OakBeam", ceiling, V(0, -.13f, z), V(14, .2f, .17f), LightOak);
            return root;
        }

        private static void SideWall(Transform parent, string name, Vector3 position, float yaw)
        {
            Transform wall = Group(name, parent, position);
            wall.localRotation = Quaternion.Euler(0, yaw, 0);
            WallPiece(wall, "Base", V(0, .45f, 0), V(12, .9f, .2f));
            WallPiece(wall, "Header", V(0, 3.85f, 0), V(12, .7f, .2f));
            WallPiece(wall, "EndLeft", V(-5.75f, 2.2f, 0), V(.5f, 2.6f, .2f));
            WallPiece(wall, "CenterPier", V(0, 2.2f, 0), V(.6f, 2.6f, .2f));
            WallPiece(wall, "EndRight", V(5.75f, 2.2f, 0), V(.5f, 2.6f, .2f));
            Window(wall, V(-2.9f, 2.2f, 0), 5.2f, 2.6f);
            Window(wall, V(2.9f, 2.2f, 0), 5.2f, 2.6f);
            Trim(wall, 12);
        }

        private static void WallPiece(Transform parent, string name, Vector3 position, Vector3 size)
        {
            Transform part = Block(name, parent, position, size, Ivory, .02f);
            Solid(part, Vector3.zero, size);
        }

        private static void Trim(Transform wall, float width)
        {
            Block("Skirting", wall, V(0, .12f, 0), V(width, .24f, .28f), Green);
            Block("Cornice", wall, V(0, 4.03f, 0), V(width, .15f, .3f), LightOak);
        }

        private static void Window(Transform parent, Vector3 center, float width, float height)
        {
            Transform frame = Group("ShopWindow", parent, center);
            Transform glass = Block(
                "Glass",
                frame,
                Vector3.zero,
                V(width, height, .07f),
                Finish("DaylightGlass", "#A5C7C4"),
                .015f
            );
            glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Solid(frame, Vector3.zero, V(width, height, .12f));
            foreach (int side in new[] { -1, 1 })
            {
                Block(
                    "FrameSide",
                    frame,
                    V(side * width / 2, 0, 0),
                    V(.14f, height + .14f, .25f),
                    LightOak
                );
                Block(
                    "FrameRail",
                    frame,
                    V(0, side * height / 2, 0),
                    V(width + .14f, .14f, .25f),
                    LightOak
                );
            }
            Block("Mullion", frame, Vector3.zero, V(.1f, height, .22f), Ivory);
            Block("Transom", frame, V(0, height * .24f, 0), V(width, .08f, .22f), Ivory);
        }

        internal static GameObject Counter()
        {
            var root = new GameObject("ServiceCounter");
            Transform p = root.transform;
            Block("Base", p, V(0, .43f, 0), V(7.1f, .8f, 1.08f), Green, .12f);
            Block("CreamTop", p, V(0, .88f, 0), V(7.3f, .14f, 1.2f), Ivory, .06f);
            Block("ToeKick", p, V(0, .08f, -.56f), V(6.9f, .12f, .08f), Brass);
            for (int i = -16; i <= 16; i++)
                Block(
                    "FlutedOak",
                    p,
                    V(i * .2f, .43f, -.55f),
                    V(.04f, .55f, .045f),
                    LightOak,
                    .012f
                );
            Solid(p, V(0, .45f, 0), V(7.3f, .9f, 1.2f));
            // 낮은 얼굴 중심을 가진 v7 캐릭터의 입이 대화 시점에서 상판에 가리지 않게 한다.
            root.transform.localScale = V(1, .78f, 1);
            return root;
        }

        internal static GameObject Menu()
        {
            var root = new GameObject("MenuBoard");
            ShopMenuBuilder.BuildCafeMenu(root.transform);
            return root;
        }

        internal static GameObject BackBar()
        {
            var root = new GameObject("BackBar");
            Block("Cabinet", root.transform, V(0, .43f, 0), V(8.5f, .84f, .7f), Oak, .06f);
            Block("Top", root.transform, V(0, .89f, 0), V(8.6f, .1f, .8f), Ivory);
            for (int i = -4; i <= 4; i++)
            {
                Block(
                    "Door",
                    root.transform,
                    V(i * .9f, .45f, -.37f),
                    V(.83f, .66f, .04f),
                    LightOak
                );
                Block(
                    "Handle",
                    root.transform,
                    V(i * .9f + .29f, .57f, -.41f),
                    V(.025f, .15f, .04f),
                    Brass
                );
            }
            Solid(root.transform, V(0, .45f, 0), V(8.6f, .9f, .8f));
            Prop("BakeryInterior/CoffeeMachine", root.transform, V(-2.9f, .95f, 0), 180);
            foreach (float x in new[] { -1.9f, -1.6f, 2.2f, 2.5f })
                Prop("BakeryInterior/Mug", root.transform, V(x, .95f, -.1f));
            Prop("HousePlants/Succulent", root.transform, V(3.7f, .95f, 0));
            return root;
        }

        internal static GameObject Display()
        {
            var root = new GameObject("PastryDisplay");
            Prop("BakeryInterior/DisplayCase", root.transform, Vector3.zero, 180).localScale = V(
                3,
                1,
                1.3f
            );
            Prop("BakedGoods/Croissant", root.transform, V(-.27f, .12f, -.03f), -12);
            Prop("BakedGoods/Bread", root.transform, V(.2f, .13f, .03f), 10);
            Prop("BakedGoods/Muffin", root.transform, V(.02f, .13f, -.2f));
            return root;
        }

        internal static GameObject Booth()
        {
            var root = new GameObject("BoothSet");
            Transform bench = Group("Bench", root.transform, Vector3.zero);
            Block("OakBase", bench, V(0, .24f, 0), V(2.6f, .42f, .82f), Oak, .07f);
            Block("Cushion", bench, V(0, .49f, .03f), V(2.6f, .16f, .82f), Coral, .075f);
            Block("Back", bench, V(0, .87f, -.32f), V(2.6f, .85f, .18f), Coral, .085f);
            foreach (float x in new[] { -.84f, 0, .84f })
                Block("BackSeam", bench, V(x, .91f, -.213f), V(.018f, .55f, .012f), Ivory, .003f);
            Solid(bench, V(0, .64f, 0), V(2.6f, 1.28f, .82f));
            Transform table = Prop("BakeryInterior/CafeTable", root.transform, V(0, 0, 1.12f));
            table.localScale = V(1.3f, 1.12f, 1.05f);
            Prop("BakeryInterior/CafeChair", root.transform, V(0, 0, 2.05f), 180);
            Prop("BakeryInterior/Mug", root.transform, V(-.17f, .73f, 1.18f), 40);
            Prop("HousePlants/Succulent", root.transform, V(.18f, .73f, 1.09f));
            return root;
        }

        internal static GameObject TableSet()
        {
            var root = new GameObject("RoundTableSet");
            Prop("BakeryInterior/CafeTable", root.transform, Vector3.zero);
            foreach (int side in new[] { -1, 1 })
                Prop(
                    "BakeryInterior/CafeChair",
                    root.transform,
                    V(0, 0, side * .82f),
                    side > 0 ? 180 : 0
                );
            Prop("BakeryInterior/Mug", root.transform, V(.17f, .65f, .12f));
            Prop("BakeryInterior/Plate", root.transform, V(-.17f, .65f, -.1f));
            Prop("BakedGoods/Croissant", root.transform, V(-.17f, .675f, -.1f));
            return root;
        }

        internal static GameObject Pendant()
        {
            var root = new GameObject("CafePendant");
            Post("CeilingRose", root.transform, V(0, -.04f, 0), .11f, .08f, Brass);
            Post("Cord", root.transform, V(0, -.39f, 0), .018f, .7f, Ink);
            SmoothBall("Shade", root.transform, V(0, -.82f, 0), V(.83f, .38f, .83f), Green);
            SmoothBall(
                "Diffuser",
                root.transform,
                V(0, -.96f, 0),
                V(.65f, .15f, .65f),
                Paint("Diffuser", "#FFF1C9")
            );
            Transform lightRoot = Group("WarmLight", root.transform, V(0, -1.06f, 0));
            Light light = lightRoot.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new UnityEngine.Color(1, .86f, .64f);
            light.intensity = 1.4f;
            light.range = 4.8f;
            light.shadows = LightShadows.None;
            return root;
        }

        internal static GameObject Decoration()
        {
            var root = new GameObject("CafeDecoration");
            Put(Save("BackBar", BackBar), root.transform, V(0, 0, 5.55f));
            Put(Save("BoothSet", Booth), root.transform, V(-6.15f, 0, -3f), 90);
            Put(Save("BoothSet", Booth), root.transform, V(-6.15f, 0, .4f), 90);
            Put(Save("RoundTableSet", TableSet), root.transform, V(4.5f, 0, -3f));
            Put(Save("RoundTableSet", TableSet), root.transform, V(4.5f, 0, .3f));
            Put(Save("PastryDisplay", Display), root.transform, V(-2.55f, .741f, 3.7f));
            Prop("BakeryInterior/CashRegister", root.transform, V(.95f, .741f, 3.7f), 180);
            Prop("BakeryInterior/ServingTray", root.transform, V(2.3f, .741f, 3.7f));
            Prop("BakeryInterior/TakeawayCup", root.transform, V(2.3f, .771f, 3.7f));
            foreach (
                Vector3 point in new[] { V(-6.2f, 0, 4.8f), V(6.1f, 0, 4.8f), V(6.1f, 0, -5.1f) }
            )
            {
                Transform plant = Prop("HousePlants/Monstera", root.transform, point);
                plant.localScale = Vector3.one * 1.6f;
            }
            Text("Brand", "elsehere", root.transform, V(-4.9f, 3.15f, 5.8f), .42f, Ivory);
            Text(
                "BrandSubtitle",
                "CORNER CAFE",
                root.transform,
                V(-4.9f, 2.72f, 5.8f),
                .15f,
                Ivory
            );
            Text("Pickup", "PICK UP", root.transform, V(4.85f, 2.9f, 5.8f), .2f, Ivory);
            foreach (TMPro.TextMeshPro label in root.GetComponentsInChildren<TMPro.TextMeshPro>())
                label.transform.localRotation = Quaternion.identity;
            return root;
        }
    }
}
