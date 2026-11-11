using System;
using System.Linq;
using A305.EditorTools.Common;
using UnityEditor;
using UnityEngine;
using static A305.EditorTools.World.MainstreetArtKit;

namespace A305.EditorTools.World
{
    /// <summary>
    /// 받은 모델에 없는 매장 소품을 둥근 기본 도형으로 만든다. 이발 의자·샴푸 세면대·빵 선반·오락기 같은 것이다.
    /// 조각의 원점은 바닥(벽에 거는 것은 벽에 닿는 뒷면) 가운데이고 앞은 +Z다.
    /// </summary>
    internal static class TownShopInteriorPieces
    {
        // ---------- 이발소 ----------

        // 거울 아래 서랍장. 도구를 올려 두는 이발대다.
        internal static Transform BarberStation(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "BarberStation",
                parent,
                position,
                yaw,
                "drawer",
                root =>
                {
                    Block(
                        "Body",
                        root,
                        new Vector3(0, .4f, 0),
                        new Vector3(1.1f, .8f, .55f),
                        DarkWood,
                        .03f
                    );
                    Block(
                        "Top",
                        root,
                        new Vector3(0, .82f, .01f),
                        new Vector3(1.16f, .04f, .58f),
                        Cream,
                        .015f
                    );
                    for (int column = -1; column <= 1; column += 2)
                    for (int row = 0; row < 3; row++)
                    {
                        Vector3 front = new Vector3(column * .27f, .16f + row * .22f, .28f);
                        Block("Drawer", root, front, new Vector3(.48f, .18f, .02f), Cream, .01f);
                        Block(
                            "Handle",
                            root,
                            front + new Vector3(0, 0, .02f),
                            new Vector3(.14f, .02f, .02f),
                            Steel,
                            .008f
                        );
                    }
                }
            );

        // 빗을 꽂은 파란 소독액 병. 미국 이발소마다 있는 바비사이드 병이다.
        internal static Transform BarbicideJar(Transform parent, Vector3 position) =>
            Piece(
                "BarbicideJar",
                parent,
                position,
                0,
                "jar",
                root =>
                {
                    Post(
                        "Jar",
                        root,
                        new Vector3(0, .11f, 0),
                        .06f,
                        .22f,
                        Paint("BarbicideBlue", "#3E8FD0")
                    );
                    Post("Lid", root, new Vector3(0, .23f, 0), .065f, .025f, Steel);
                    for (int index = 0; index < 3; index++)
                        Block(
                            "Comb",
                            root,
                            new Vector3(-.025f + index * .025f, .2f, 0),
                            new Vector3(.012f, .2f, .05f),
                            Paint("CombBlack", "#262626"),
                            .004f
                        ).localRotation = Quaternion.Euler(0, 0, -8 + index * 8);
                }
            );

        // 바리캉·분무기·빗·드라이어. 이발대 위 장식이다.
        internal static Transform BarberTools(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "BarberTools",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Material black = Paint("CombBlack", "#262626");
                    Block(
                        "Clipper",
                        root,
                        new Vector3(-.2f, .025f, 0),
                        new Vector3(.06f, .05f, .16f),
                        black,
                        .02f
                    );
                    Block(
                        "ClipperBlade",
                        root,
                        new Vector3(-.2f, .025f, .09f),
                        new Vector3(.055f, .03f, .03f),
                        Steel,
                        .008f
                    );
                    Post(
                        "Spray",
                        root,
                        new Vector3(0, .08f, -.05f),
                        .035f,
                        .16f,
                        Paint("SprayTeal", "#4FA7A0")
                    );
                    Block(
                        "SprayHead",
                        root,
                        new Vector3(0, .18f, -.04f),
                        new Vector3(.03f, .04f, .05f),
                        White,
                        .01f
                    );
                    Block(
                        "Comb",
                        root,
                        new Vector3(.12f, .005f, .08f),
                        new Vector3(.16f, .01f, .04f),
                        black,
                        .004f
                    );
                    Block(
                        "DryerBody",
                        root,
                        new Vector3(.24f, .05f, -.02f),
                        new Vector3(.09f, .09f, .2f),
                        Paint("DryerCoral", "#E58B63"),
                        .04f
                    );
                    Block(
                        "DryerHandle",
                        root,
                        new Vector3(.24f, .02f, .1f),
                        new Vector3(.05f, .04f, .1f),
                        Paint("DryerCoral", "#E58B63"),
                        .02f
                    );
                }
            );

        // 바퀴 달린 도구 서랍. 위에 접은 수건을 올렸다.
        internal static Transform ToolTrolley(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "ToolTrolley",
                parent,
                position,
                yaw,
                "drawer",
                root =>
                {
                    Block(
                        "Body",
                        root,
                        new Vector3(0, .45f, 0),
                        new Vector3(.45f, .7f, .4f),
                        BarberRed,
                        .04f
                    );
                    for (int row = 0; row < 3; row++)
                        Block(
                            "Drawer",
                            root,
                            new Vector3(0, .2f + row * .22f, .205f),
                            new Vector3(.38f, .16f, .02f),
                            Paint("TrolleyRedLight", "#C95B4F"),
                            .01f
                        );
                    for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        Ball(
                            "Caster",
                            root,
                            new Vector3(x * .17f, .05f, z * .15f),
                            Vector3.one * .08f,
                            Leather
                        );
                    Block(
                        "Towel",
                        root,
                        new Vector3(0, .83f, 0),
                        new Vector3(.34f, .06f, .26f),
                        White,
                        .025f
                    );
                }
            );

        // 머리를 감기는 세면대. 뒷벽에 붙이고 손님은 등을 대고 앉는다.
        internal static Transform ShampooSink(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "ShampooSink",
                parent,
                position,
                yaw,
                "sink",
                root =>
                {
                    Block(
                        "Cabinet",
                        root,
                        new Vector3(0, .4f, 0),
                        new Vector3(.9f, .8f, .55f),
                        DarkWood,
                        .03f
                    );
                    Block(
                        "Basin",
                        root,
                        new Vector3(0, .86f, .05f),
                        new Vector3(.66f, .14f, .48f),
                        White,
                        .07f
                    );
                    Block(
                        "Bowl",
                        root,
                        new Vector3(0, .91f, .07f),
                        new Vector3(.5f, .06f, .34f),
                        Paint("BasinShadow", "#C9D3D6"),
                        .03f
                    );
                    Post("Faucet", root, new Vector3(0, 1.02f, -.16f), .025f, .24f, Steel);
                    Block(
                        "Spout",
                        root,
                        new Vector3(0, 1.13f, -.09f),
                        new Vector3(.04f, .035f, .16f),
                        Steel,
                        .015f
                    );
                    Post(
                        "Bottle",
                        root,
                        new Vector3(.32f, .99f, -.17f),
                        .04f,
                        .18f,
                        Paint("SprayTeal", "#4FA7A0")
                    );
                    Post(
                        "Bottle",
                        root,
                        new Vector3(-.32f, .98f, -.17f),
                        .035f,
                        .16f,
                        Paint("BottleAmber", "#D9A441")
                    );
                }
            );

        // 샴푸 세면대 앞 등받이가 뒤로 누운 의자
        internal static Transform WashChair(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "WashChair",
                parent,
                position,
                yaw,
                "chair",
                root =>
                {
                    Post("Base", root, new Vector3(0, .02f, 0), .26f, .04f, Steel);
                    Post("Column", root, new Vector3(0, .2f, 0), .06f, .34f, Steel);
                    Block(
                        "Seat",
                        root,
                        new Vector3(0, .45f, .05f),
                        new Vector3(.62f, .16f, .6f),
                        Leather,
                        .07f
                    );
                    Block(
                        "Back",
                        root,
                        new Vector3(0, .78f, -.3f),
                        new Vector3(.62f, .6f, .14f),
                        Leather,
                        .07f
                    ).localRotation = Quaternion.Euler(-28, 0, 0);
                    foreach (int side in new[] { -1, 1 })
                        Block(
                            "Armrest",
                            root,
                            new Vector3(side * .33f, .62f, .05f),
                            new Vector3(.08f, .06f, .5f),
                            Leather,
                            .03f
                        );
                    Block(
                        "FootRest",
                        root,
                        new Vector3(0, .2f, .45f),
                        new Vector3(.5f, .05f, .2f),
                        Steel,
                        .02f
                    );
                }
            );

        // 둥글게 만 수건 더미
        internal static Transform TowelStack(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "TowelStack",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Material blue = Paint("TowelBlue", "#9CC3D5");
                    int[] counts = { 3, 2, 1 };
                    for (int row = 0; row < counts.Length; row++)
                    for (int index = 0; index < counts[row]; index++)
                    {
                        float x = (index - (counts[row] - 1) / 2f) * .13f;
                        Post(
                            "Towel",
                            root,
                            new Vector3(x, .065f + row * .11f, 0),
                            .065f,
                            .3f,
                            (row + index) % 2 == 0 ? White : blue
                        ).localRotation = Quaternion.Euler(90, 0, 0);
                    }
                }
            );

        // 미용실의 둥근 머리 건조기 의자
        internal static Transform HoodDryerChair(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "HoodDryerChair",
                parent,
                position,
                yaw,
                "chair",
                root =>
                {
                    Material mint = Paint("SalonMint", "#8FCFBF");
                    Block(
                        "Seat",
                        root,
                        new Vector3(0, .42f, .05f),
                        new Vector3(.66f, .18f, .6f),
                        mint,
                        .07f
                    );
                    Block(
                        "Back",
                        root,
                        new Vector3(0, .75f, -.24f),
                        new Vector3(.66f, .62f, .14f),
                        mint,
                        .07f
                    );
                    foreach (int side in new[] { -1, 1 })
                    {
                        Block(
                            "Armrest",
                            root,
                            new Vector3(side * .34f, .58f, .05f),
                            new Vector3(.08f, .08f, .55f),
                            mint,
                            .03f
                        );
                        Post("Leg", root, new Vector3(side * .26f, .17f, .05f), .025f, .34f, Steel);
                    }
                    Post("Stand", root, new Vector3(0, .95f, -.42f), .03f, 1.9f, Steel);
                    Block(
                        "Arm",
                        root,
                        new Vector3(0, 1.62f, -.22f),
                        new Vector3(.05f, .05f, .4f),
                        Steel,
                        .02f
                    );
                    SmoothBall(
                        "Hood",
                        root,
                        new Vector3(0, 1.48f, -.02f),
                        new Vector3(.52f, .44f, .52f),
                        Paint("DryerCream", "#F4EBD6")
                    );
                }
            );

        internal static Transform CoatRack(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "CoatRack",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Post("Pole", root, new Vector3(0, .85f, 0), .025f, 1.7f, DarkWood);
                    Post("Foot", root, new Vector3(0, .02f, 0), .2f, .04f, DarkWood);
                    for (int index = 0; index < 4; index++)
                    {
                        float angle = index * 90 * Mathf.Deg2Rad;
                        Ball(
                            "Hook",
                            root,
                            new Vector3(Mathf.Cos(angle) * .1f, 1.62f, Mathf.Sin(angle) * .1f),
                            Vector3.one * .05f,
                            Steel
                        );
                    }
                    Block(
                        "Coat",
                        root,
                        new Vector3(.12f, 1.25f, 0),
                        new Vector3(.08f, .7f, .36f),
                        Paint("CoatNavy", "#3B4E6B"),
                        .04f
                    );
                    Post("HatCrown", root, new Vector3(-.08f, 1.72f, 0), .1f, .12f, Leather);
                    Post("HatBrim", root, new Vector3(-.08f, 1.66f, 0), .16f, .02f, Leather);
                }
            );

        internal static Transform MagazineRack(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "MagazineRack",
                parent,
                position,
                yaw,
                "book",
                root =>
                {
                    foreach (int side in new[] { -1, 1 })
                        Block(
                            "Side",
                            root,
                            new Vector3(side * .24f, .45f, 0),
                            new Vector3(.03f, .9f, .3f),
                            Wood,
                            .01f
                        );
                    string[] covers = { "#E85D75", "#4E7FA6", "#F4C430", "#5E8C3A" };
                    for (int row = 0; row < 3; row++)
                    {
                        Block(
                            "Ledge",
                            root,
                            new Vector3(0, .12f + row * .28f, .06f),
                            new Vector3(.46f, .02f, .16f),
                            Wood,
                            .008f
                        );
                        Block(
                            "Magazine",
                            root,
                            new Vector3(0, .24f + row * .28f, .02f),
                            new Vector3(.36f, .24f, .015f),
                            Paint("Cover" + covers[row].TrimStart('#'), covers[row]),
                            .005f
                        ).localRotation = Quaternion.Euler(-15, 0, 0);
                    }
                    Block(
                        "Back",
                        root,
                        new Vector3(0, .45f, -.12f),
                        new Vector3(.46f, .9f, .02f),
                        Wood,
                        .008f
                    );
                }
            );

        // 벽시계. 뒷면이 벽에 닿는다.
        internal static Transform WallClock(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "WallClock",
                parent,
                position,
                yaw,
                "clock",
                root =>
                {
                    Post("Rim", root, new Vector3(0, 0, .03f), .23f, .06f, Navy).localRotation =
                        Quaternion.Euler(90, 0, 0);
                    Post("Face", root, new Vector3(0, 0, .055f), .2f, .02f, Cream).localRotation =
                        Quaternion.Euler(90, 0, 0);
                    Block(
                        "HourHand",
                        root,
                        new Vector3(.04f, .03f, .07f),
                        new Vector3(.12f, .02f, .01f),
                        Leather,
                        .004f
                    ).localRotation = Quaternion.Euler(0, 0, 35);
                    Block(
                        "MinuteHand",
                        root,
                        new Vector3(0, .07f, .072f),
                        new Vector3(.02f, .15f, .01f),
                        Leather,
                        .004f
                    );
                }
            );

        internal static Transform TrashBin(Transform parent, Vector3 position) =>
            Piece(
                "TrashBin",
                parent,
                position,
                0,
                "trash_can",
                root =>
                {
                    Post("Bin", root, new Vector3(0, .22f, 0), .17f, .44f, Steel);
                    Post(
                        "Rim",
                        root,
                        new Vector3(0, .445f, 0),
                        .18f,
                        .03f,
                        Paint("BinDark", "#6E777D")
                    );
                }
            );

        internal static Transform Broom(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "Broom",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Transform stick = Post(
                        "Stick",
                        root,
                        new Vector3(0, .75f, 0),
                        .015f,
                        1.3f,
                        Wood
                    );
                    stick.localRotation = Quaternion.Euler(0, 0, 8);
                    Block(
                        "Bristles",
                        root,
                        new Vector3(.01f, .08f, 0),
                        new Vector3(.28f, .16f, .06f),
                        Paint("BristleStraw", "#D8B66A"),
                        .02f
                    );
                }
            );

        // 글자 안내판. 벽에 붙이고 앞(+Z)으로 읽는다.
        internal static Transform WallBoard(
            string name,
            Transform parent,
            Vector3 position,
            float yaw,
            string title,
            string body,
            float width,
            float height,
            string hex
        ) =>
            Piece(
                name,
                parent,
                position,
                yaw,
                "sign",
                root =>
                {
                    Block(
                        "Board",
                        root,
                        new Vector3(0, 0, .02f),
                        new Vector3(width, height, .04f),
                        Paint("Board" + hex.TrimStart('#'), hex),
                        .02f
                    );
                    Block(
                        "Frame",
                        root,
                        new Vector3(0, 0, .01f),
                        new Vector3(width + .08f, height + .08f, .02f),
                        Wood,
                        .01f
                    );
                    Text("Title", title, root, new Vector3(0, height / 2 - .14f, .05f), .16f);
                    if (body != null)
                        Text("Body", body, root, new Vector3(0, -.08f, .05f), .1f);
                }
            );

        // ---------- 빵집 ----------

        // 비스듬한 칸에 빵을 올린 원목 벽 선반
        internal static Transform BreadRack(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "BreadRack",
                parent,
                position,
                yaw,
                "shelf",
                root =>
                {
                    foreach (int side in new[] { -1, 1 })
                        Block(
                            "Upright",
                            root,
                            new Vector3(side * .78f, .95f, 0),
                            new Vector3(.06f, 1.9f, .45f),
                            Wood,
                            .02f
                        );
                    Block(
                        "Back",
                        root,
                        new Vector3(0, .95f, -.2f),
                        new Vector3(1.5f, 1.9f, .03f),
                        Paint("RackBack", "#8A6239"),
                        .01f
                    );
                    Block(
                        "Crown",
                        root,
                        new Vector3(0, 1.93f, .02f),
                        new Vector3(1.66f, .08f, .5f),
                        Wood,
                        .02f
                    );
                    string[][] rows =
                    {
                        new[] { "Bread", "Bread", "Bread" },
                        new[] { "Croissant", "Croissant", "Croissant", "Croissant" },
                        new[] { "Bread", "Bread", "Bread" },
                        new[] { "Muffin", "Muffin", "Donut", "Donut", "Muffin" },
                    };
                    for (int row = 0; row < rows.Length; row++)
                    {
                        float y = .22f + row * .44f;
                        Block(
                            "Ledge",
                            root,
                            new Vector3(0, y, .02f),
                            new Vector3(1.5f, .03f, .4f),
                            Wood,
                            .01f
                        ).localRotation = Quaternion.Euler(10, 0, 0);
                        Block(
                            "Lip",
                            root,
                            new Vector3(0, y + .03f, .21f),
                            new Vector3(1.5f, .06f, .02f),
                            Wood,
                            .008f
                        );
                        for (int index = 0; index < rows[row].Length; index++)
                        {
                            float x =
                                (index - (rows[row].Length - 1) / 2f) * (1.3f / rows[row].Length);
                            Treat(
                                "BakedGoods",
                                rows[row][index],
                                root,
                                new Vector3(x, y + .02f, .04f),
                                rows[row][index] == "Bread" ? 90 : index * 37,
                                // 멀리서도 빵이 보이도록 진열장 빵보다 크게 둔다.
                                rows[row][index] == "Bread"
                                    ? .85f
                                    : 1.5f
                            );
                        }
                    }
                }
            );

        // 바게트를 꽂은 등나무 바구니
        internal static Transform BaguetteBasket(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "BaguetteBasket",
                parent,
                position,
                yaw,
                "bread",
                root =>
                {
                    Material wicker = Paint("Wicker", "#C99A5B");
                    Post("Basket", root, new Vector3(0, .22f, 0), .2f, .44f, wicker);
                    Post(
                        "Band",
                        root,
                        new Vector3(0, .4f, 0),
                        .21f,
                        .04f,
                        Paint("WickerDark", "#9C6E3A")
                    );
                    Material crust = Paint("BaguetteCrust", "#D9A35B");
                    for (int index = 0; index < 5; index++)
                    {
                        float angle = index * 72 * Mathf.Deg2Rad;
                        Post(
                            "Baguette",
                            root,
                            new Vector3(Mathf.Cos(angle) * .08f, .55f, Mathf.Sin(angle) * .08f),
                            .035f,
                            .6f,
                            crust
                        ).localRotation = Quaternion.Euler(
                            Mathf.Sin(angle) * 10,
                            0,
                            -Mathf.Cos(angle) * 10
                        );
                    }
                }
            );

        // 밀가루 포대. 이름표에 FLOUR를 적었다.
        internal static Transform FlourSack(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "FlourSack",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Block(
                        "Sack",
                        root,
                        new Vector3(0, .25f, 0),
                        new Vector3(.5f, .5f, .34f),
                        Paint("Burlap", "#E7D8B8"),
                        .14f
                    );
                    Block(
                        "Tie",
                        root,
                        new Vector3(0, .53f, 0),
                        new Vector3(.22f, .08f, .14f),
                        Paint("Burlap", "#E7D8B8"),
                        .04f
                    );
                    Block(
                        "Label",
                        root,
                        new Vector3(0, .28f, .171f),
                        new Vector3(.3f, .14f, .005f),
                        Paint("SackLabel", "#B8452F"),
                        .004f
                    );
                    Text("Flour", "FLOUR", root, new Vector3(0, .28f, .177f), .07f);
                }
            );

        // 유리문 세 칸 데크 오븐
        internal static Transform BakeryOven(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "BakeryOven",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Block(
                        "Body",
                        root,
                        new Vector3(0, .8f, 0),
                        new Vector3(1.5f, 1.3f, .75f),
                        Steel,
                        .05f
                    );
                    Block(
                        "Stand",
                        root,
                        new Vector3(0, .07f, 0),
                        new Vector3(1.4f, .14f, .7f),
                        Paint("BinDark", "#6E777D"),
                        .02f
                    );
                    for (int deck = 0; deck < 3; deck++)
                    {
                        float y = .38f + deck * .4f;
                        Block(
                            "Door",
                            root,
                            new Vector3(0, y, .38f),
                            new Vector3(1.3f, .3f, .03f),
                            Paint("OvenDoor", "#59646B"),
                            .02f
                        );
                        Block(
                            "Glow",
                            root,
                            new Vector3(0, y, .39f),
                            new Vector3(1.05f, .16f, .02f),
                            Paint("OvenGlow", "#F2A54A"),
                            .01f
                        );
                        Block(
                            "Handle",
                            root,
                            new Vector3(0, y + .17f, .42f),
                            new Vector3(1.1f, .03f, .03f),
                            Paint("BinDark", "#6E777D"),
                            .01f
                        );
                    }
                    Post("Flue", root, new Vector3(.5f, 1.6f, -.15f), .08f, .3f, Steel);
                }
            );

        // 바퀴 달린 쟁반 선반. 쟁반마다 빵을 올렸다.
        internal static Transform BakersRack(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "BakersRack",
                parent,
                position,
                yaw,
                "shelf",
                root =>
                {
                    for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Post(
                            "Post",
                            root,
                            new Vector3(x * .3f, .82f, z * .22f),
                            .015f,
                            1.5f,
                            Steel
                        );
                        Ball(
                            "Caster",
                            root,
                            new Vector3(x * .3f, .04f, z * .22f),
                            Vector3.one * .07f,
                            Leather
                        );
                    }
                    for (int level = 0; level < 5; level++)
                    {
                        float y = .25f + level * .3f;
                        Block(
                            "Tray",
                            root,
                            new Vector3(0, y, 0),
                            new Vector3(.6f, .02f, .44f),
                            Paint("TrayMetal", "#C7CDD1"),
                            .006f
                        );
                        for (int index = 0; index < 3; index++)
                            Treat(
                                "BakedGoods",
                                level % 2 == 0 ? "Bread" : "Croissant",
                                root,
                                new Vector3(-.18f + index * .18f, y + .01f, 0),
                                level % 2 == 0 ? 0 : 90,
                                level % 2 == 0 ? .45f : 1
                            );
                    }
                }
            );

        // ---------- 다이너 ----------

        internal static Transform Jukebox(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "Jukebox",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Material red = Paint("JukeboxRed", "#C8463B");
                    Block(
                        "Body",
                        root,
                        new Vector3(0, .6f, 0),
                        new Vector3(.85f, 1.2f, .55f),
                        red,
                        .08f
                    );
                    SmoothBall(
                        "Dome",
                        root,
                        new Vector3(0, 1.2f, 0),
                        new Vector3(.85f, .55f, .55f),
                        red
                    );
                    Block(
                        "Window",
                        root,
                        new Vector3(0, .95f, .28f),
                        new Vector3(.6f, .4f, .02f),
                        Paint("JukeboxGlass", "#F4D58D"),
                        .05f
                    );
                    Block(
                        "Grill",
                        root,
                        new Vector3(0, .38f, .28f),
                        new Vector3(.6f, .34f, .02f),
                        Paint("JukeboxChrome", "#D6DBDE"),
                        .04f
                    );
                    for (int index = 0; index < 4; index++)
                        Block(
                            "Bubble",
                            root,
                            new Vector3(-.3f + index * .2f, 1.27f, .27f),
                            new Vector3(.12f, .12f, .02f),
                            Paint(
                                "Bubble" + index,
                                new[] { "#5FA8D3", "#F4C430", "#7FAD50", "#E85D75" }[index]
                            ),
                            .05f
                        );
                }
            );

        internal static Transform GumballMachine(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "GumballMachine",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Material red = Paint("JukeboxRed", "#C8463B");
                    Post("Foot", root, new Vector3(0, .03f, 0), .18f, .06f, red);
                    Post("Pole", root, new Vector3(0, .45f, 0), .04f, .8f, Steel);
                    Block(
                        "Base",
                        root,
                        new Vector3(0, .95f, 0),
                        new Vector3(.26f, .2f, .26f),
                        red,
                        .06f
                    );
                    SmoothBall(
                        "Globe",
                        root,
                        new Vector3(0, 1.22f, 0),
                        Vector3.one * .36f,
                        Paint("GumballGlass", "#F3F0E6")
                    );
                    for (int index = 0; index < 6; index++)
                    {
                        float angle = index * 60 * Mathf.Deg2Rad;
                        Ball(
                            "Gum",
                            root,
                            new Vector3(
                                Mathf.Cos(angle) * .1f,
                                1.16f + index % 2 * .06f,
                                Mathf.Sin(angle) * .1f + .08f
                            ),
                            Vector3.one * .07f,
                            Paint(
                                "Bubble" + index % 4,
                                new[] { "#5FA8D3", "#F4C430", "#7FAD50", "#E85D75" }[index % 4]
                            )
                        );
                    }
                    Post("Cap", root, new Vector3(0, 1.41f, 0), .06f, .04f, red);
                }
            );

        // 천장에 매단 둥근 갓 조명. 원점은 천장에 닿는 곳이다.
        internal static Transform PendantLamp(Transform parent, Vector3 position, float drop) =>
            Piece(
                "PendantLamp" + Mathf.RoundToInt(drop * 100),
                parent,
                position,
                0,
                "lamp",
                root =>
                {
                    Post("Cord", root, new Vector3(0, -drop / 2, 0), .01f, drop, Leather);
                    SmoothBall(
                        "Shade",
                        root,
                        new Vector3(0, -drop - .1f, 0),
                        new Vector3(.42f, .26f, .42f),
                        Green
                    );
                    Ball(
                        "Bulb",
                        root,
                        new Vector3(0, -drop - .2f, 0),
                        Vector3.one * .1f,
                        Paint("BulbWarm", "#FFE7B3")
                    );
                }
            );

        internal static Transform NapkinDispenser(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "NapkinDispenser",
                parent,
                position,
                yaw,
                "napkin",
                root =>
                {
                    Block(
                        "Case",
                        root,
                        new Vector3(0, .07f, 0),
                        new Vector3(.16f, .14f, .09f),
                        Steel,
                        .015f
                    );
                    Block(
                        "Napkins",
                        root,
                        new Vector3(0, .14f, 0),
                        new Vector3(.12f, .03f, .07f),
                        White,
                        .008f
                    );
                }
            );

        internal static Transform ServiceBell(Transform parent, Vector3 position) =>
            Piece(
                "ServiceBell",
                parent,
                position,
                0,
                "bell",
                root =>
                {
                    Post("Plate", root, new Vector3(0, .01f, 0), .07f, .02f, DarkWood);
                    SmoothBall(
                        "Dome",
                        root,
                        new Vector3(0, .03f, 0),
                        new Vector3(.1f, .08f, .1f),
                        Paint("BellBrass", "#D8B24A")
                    );
                    Post(
                        "Button",
                        root,
                        new Vector3(0, .075f, 0),
                        .01f,
                        .03f,
                        Paint("BellBrass", "#D8B24A")
                    );
                }
            );

        // ---------- 우체국 ----------

        // 뒷벽 우편 분류 칸. 칸마다 편지를 꽂았다.
        internal static Transform SortingCubbies(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "SortingCubbies",
                parent,
                position,
                yaw,
                "shelf",
                root =>
                {
                    Block(
                        "Back",
                        root,
                        new Vector3(0, .55f, -.15f),
                        new Vector3(1.9f, 1.1f, .02f),
                        Wood,
                        .008f
                    );
                    for (int row = 0; row <= 4; row++)
                        Block(
                            "Rail",
                            root,
                            new Vector3(0, row * .27f + .01f, 0),
                            new Vector3(1.9f, .02f, .32f),
                            Wood,
                            .006f
                        );
                    for (int column = 0; column <= 6; column++)
                        Block(
                            "Divider",
                            root,
                            new Vector3(-.95f + column * (1.9f / 6), .55f, 0),
                            new Vector3(.02f, 1.1f, .32f),
                            Wood,
                            .006f
                        );
                    Material envelope = Paint("Envelope", "#F7F3EA");
                    Material kraft = Paint("Cardboard", "#C49A63");
                    for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 6; column++)
                        if ((row * 3 + column) % 4 != 1)
                            Block(
                                "Letters",
                                root,
                                new Vector3(
                                    -.95f + (column + .5f) * (1.9f / 6),
                                    row * .27f + .08f,
                                    .02f
                                ),
                                new Vector3(.22f, .1f + (row + column) % 3 * .03f, .18f),
                                (row + column) % 3 == 0 ? kraft : envelope,
                                .006f
                            );
                }
            );

        // 바퀴 달린 천 우편함 수레
        internal static Transform MailCart(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "MailCart",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Material canvas = Paint("MailCanvas", "#5D7891");
                    Block(
                        "Bin",
                        root,
                        new Vector3(0, .5f, 0),
                        new Vector3(.8f, .6f, .55f),
                        canvas,
                        .06f
                    );
                    Block(
                        "Rim",
                        root,
                        new Vector3(0, .81f, 0),
                        new Vector3(.84f, .04f, .59f),
                        Steel,
                        .015f
                    );
                    Block(
                        "Mail",
                        root,
                        new Vector3(0, .79f, 0),
                        new Vector3(.7f, .06f, .45f),
                        Paint("Envelope", "#F7F3EA"),
                        .02f
                    );
                    Block(
                        "Parcel",
                        root,
                        new Vector3(.15f, .86f, .05f),
                        new Vector3(.24f, .14f, .2f),
                        Paint("Cardboard", "#C49A63"),
                        .01f
                    );
                    for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Post(
                            "Leg",
                            root,
                            new Vector3(x * .36f, .13f, z * .24f),
                            .015f,
                            .18f,
                            Steel
                        );
                        Ball(
                            "Caster",
                            root,
                            new Vector3(x * .36f, .04f, z * .24f),
                            Vector3.one * .08f,
                            Leather
                        );
                    }
                }
            );

        // 테이프를 두른 소포 더미
        internal static Transform ParcelStack(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "ParcelStack",
                parent,
                position,
                yaw,
                "package",
                root =>
                {
                    Material kraft = Paint("Cardboard", "#C49A63");
                    Material tape = Paint("ParcelTape", "#E3C88F");
                    Vector3[] sizes =
                    {
                        new Vector3(.6f, .4f, .45f),
                        new Vector3(.45f, .3f, .38f),
                        new Vector3(.3f, .22f, .26f),
                    };
                    float y = 0;
                    for (int index = 0; index < sizes.Length; index++)
                    {
                        Vector3 size = sizes[index];
                        Vector3 center = new Vector3((index % 2) * .05f, y + size.y / 2, 0);
                        Block("Box", root, center, size, kraft, .015f).localRotation =
                            Quaternion.Euler(0, index * 14, 0);
                        Block(
                            "Tape",
                            root,
                            center + Vector3.up * (size.y / 2),
                            new Vector3(.08f, .005f, size.z + .01f),
                            tape,
                            .002f
                        ).localRotation = Quaternion.Euler(0, index * 14, 0);
                        y += size.y;
                    }
                }
            );

        // 줄서기 기둥 두 개와 줄
        internal static Transform QueueLine(
            Transform parent,
            Vector3 position,
            float yaw,
            float length
        ) =>
            Piece(
                "QueueLine" + Mathf.RoundToInt(length * 10),
                parent,
                position,
                yaw,
                "railing",
                root =>
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Post(
                            "Base",
                            root,
                            new Vector3(side * length / 2, .02f, 0),
                            .15f,
                            .04f,
                            Steel
                        );
                        Post(
                            "Post",
                            root,
                            new Vector3(side * length / 2, .47f, 0),
                            .03f,
                            .9f,
                            Steel
                        );
                        Ball(
                            "Cap",
                            root,
                            new Vector3(side * length / 2, .93f, 0),
                            Vector3.one * .07f,
                            Steel
                        );
                    }
                    Block(
                        "Rope",
                        root,
                        new Vector3(0, .82f, 0),
                        new Vector3(length, .045f, .045f),
                        Paint("BankRope", "#8C2F39"),
                        .02f
                    );
                }
            );

        // 서서 양식을 쓰는 탁자. 양식 꽂이와 펜이 있다.
        internal static Transform FormDesk(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "FormDesk",
                parent,
                position,
                yaw,
                "table",
                root =>
                {
                    Block(
                        "Top",
                        root,
                        new Vector3(0, 1.0f, .05f),
                        new Vector3(1.2f, .05f, .5f),
                        Wood,
                        .015f
                    ).localRotation = Quaternion.Euler(8, 0, 0);
                    foreach (int side in new[] { -1, 1 })
                        Block(
                            "Leg",
                            root,
                            new Vector3(side * .52f, .5f, 0),
                            new Vector3(.06f, 1.0f, .4f),
                            DarkWood,
                            .015f
                        );
                    Block(
                        "Rack",
                        root,
                        new Vector3(0, 1.15f, -.18f),
                        new Vector3(1.1f, .22f, .06f),
                        DarkWood,
                        .015f
                    );
                    for (int index = 0; index < 4; index++)
                        Block(
                            "Form",
                            root,
                            new Vector3(-.4f + index * .27f, 1.2f, -.16f),
                            new Vector3(.18f, .22f, .01f),
                            Paint("Envelope", "#F7F3EA"),
                            .003f
                        );
                    Post(
                        "Pen",
                        root,
                        new Vector3(.3f, 1.05f, .1f),
                        .01f,
                        .14f,
                        Leather
                    ).localRotation = Quaternion.Euler(0, 0, 80);
                }
            );

        // 파란 우편 투입함(미국 우체국 앞 우체통)
        internal static Transform BlueMailbox(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "BlueMailbox",
                parent,
                position,
                yaw,
                "mailbox",
                root =>
                {
                    Material blue = Paint("MailboxBlue", "#2F5DA8");
                    Block(
                        "Body",
                        root,
                        new Vector3(0, .62f, 0),
                        new Vector3(.55f, .9f, .5f),
                        blue,
                        .04f
                    );
                    SmoothBall(
                        "Top",
                        root,
                        new Vector3(0, 1.07f, 0),
                        new Vector3(.55f, .3f, .5f),
                        blue
                    );
                    foreach (int x in new[] { -1, 1 })
                    foreach (int z in new[] { -1, 1 })
                        Block(
                            "Leg",
                            root,
                            new Vector3(x * .22f, .09f, z * .19f),
                            new Vector3(.06f, .18f, .06f),
                            Leather,
                            .01f
                        );
                    Block(
                        "Slot",
                        root,
                        new Vector3(0, .98f, .25f),
                        new Vector3(.3f, .04f, .02f),
                        Leather,
                        .01f
                    );
                    Text("Mail", "U.S. MAIL", root, new Vector3(0, .7f, .255f), .06f);
                }
            );

        // ---------- 은행 ----------

        // 뒷벽 금고 문
        internal static Transform VaultDoor(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "VaultDoor",
                parent,
                position,
                yaw,
                "door",
                root =>
                {
                    Material dark = Paint("VaultDark", "#4F5A61");
                    Block(
                        "Frame",
                        root,
                        new Vector3(0, 1.0f, .04f),
                        new Vector3(1.9f, 2.0f, .08f),
                        dark,
                        .04f
                    );
                    Post("Door", root, new Vector3(0, 1.0f, .12f), .82f, .1f, Steel).localRotation =
                        Quaternion.Euler(90, 0, 0);
                    Post("Ring", root, new Vector3(0, 1.0f, .18f), .62f, .02f, dark).localRotation =
                        Quaternion.Euler(90, 0, 0);
                    Post(
                        "Hub",
                        root,
                        new Vector3(0, 1.0f, .2f),
                        .14f,
                        .06f,
                        Paint("BellBrass", "#D8B24A")
                    ).localRotation = Quaternion.Euler(90, 0, 0);
                    for (int index = 0; index < 3; index++)
                        Block(
                            "Spoke",
                            root,
                            new Vector3(0, 1.0f, .23f),
                            new Vector3(.7f, .05f, .05f),
                            Paint("BellBrass", "#D8B24A"),
                            .02f
                        ).localRotation = Quaternion.Euler(0, 0, index * 60);
                }
            );

        // 창구 컴퓨터 화면. 뒤에서 보면 회색 뒷판이다.
        internal static Transform DeskMonitor(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "DeskMonitor",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Block(
                        "Foot",
                        root,
                        new Vector3(0, .01f, 0),
                        new Vector3(.18f, .02f, .14f),
                        Leather,
                        .008f
                    );
                    Block(
                        "Neck",
                        root,
                        new Vector3(0, .1f, -.02f),
                        new Vector3(.04f, .18f, .03f),
                        Leather,
                        .008f
                    );
                    Block(
                        "Screen",
                        root,
                        new Vector3(0, .26f, 0),
                        new Vector3(.46f, .28f, .04f),
                        Leather,
                        .015f
                    );
                    Block(
                        "Display",
                        root,
                        new Vector3(0, .26f, .021f),
                        new Vector3(.42f, .24f, .005f),
                        Paint("ScreenBlue", "#5FA8D3"),
                        .004f
                    );
                    Block(
                        "Keyboard",
                        root,
                        new Vector3(0, .01f, .2f),
                        new Vector3(.4f, .02f, .13f),
                        Paint("KeyboardGrey", "#D7D9D6"),
                        .006f
                    );
                }
            );

        internal static Transform BrochureRack(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "BrochureRack",
                parent,
                position,
                yaw,
                "shelf",
                root =>
                {
                    Block(
                        "Stand",
                        root,
                        new Vector3(0, .6f, -.08f),
                        new Vector3(.5f, 1.2f, .04f),
                        DarkWood,
                        .01f
                    );
                    string[] colors =
                    {
                        "#5FA8D3",
                        "#F4C430",
                        "#7FAD50",
                        "#E85D75",
                        "#F08A4B",
                        "#B07CC6",
                    };
                    for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 2; column++)
                    {
                        float y = .55f + row * .25f;
                        Block(
                            "Pocket",
                            root,
                            new Vector3(-.12f + column * .24f, y - .06f, 0),
                            new Vector3(.2f, .1f, .08f),
                            Paint("BrochureClear", "#DDE7EA"),
                            .01f
                        );
                        string hex = colors[row * 2 + column];
                        Block(
                            "Brochure",
                            root,
                            new Vector3(-.12f + column * .24f, y, -.01f),
                            new Vector3(.16f, .2f, .015f),
                            Paint("Cover" + hex.TrimStart('#'), hex),
                            .004f
                        );
                    }
                    Block(
                        "Foot",
                        root,
                        new Vector3(0, .02f, 0),
                        new Vector3(.5f, .04f, .3f),
                        DarkWood,
                        .01f
                    );
                }
            );

        internal static Transform WaterCooler(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "WaterCooler",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Block(
                        "Body",
                        root,
                        new Vector3(0, .5f, 0),
                        new Vector3(.36f, 1.0f, .36f),
                        White,
                        .05f
                    );
                    Post(
                        "Bottle",
                        root,
                        new Vector3(0, 1.22f, 0),
                        .15f,
                        .44f,
                        Paint("WaterBlue", "#9CCBE0")
                    );
                    Block(
                        "Tap",
                        root,
                        new Vector3(0, .82f, .19f),
                        new Vector3(.06f, .06f, .04f),
                        Paint("BankRope", "#8C2F39"),
                        .015f
                    );
                    Block(
                        "Tray",
                        root,
                        new Vector3(0, .62f, .17f),
                        new Vector3(.2f, .03f, .08f),
                        Leather,
                        .01f
                    );
                }
            );

        // ---------- 빨래방 ----------

        // 개어 둔 옷을 담은 빨래 바구니
        internal static Transform LaundryBasket(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "LaundryBasket",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Material basket = Paint("BasketBlue", "#7DB7D6");
                    Block(
                        "Floor",
                        root,
                        new Vector3(0, .02f, 0),
                        new Vector3(.5f, .04f, .36f),
                        basket,
                        .015f
                    );
                    foreach (int side in new[] { -1, 1 })
                    {
                        Block(
                            "Side",
                            root,
                            new Vector3(side * .24f, .13f, 0),
                            new Vector3(.03f, .26f, .36f),
                            basket,
                            .012f
                        );
                        Block(
                            "Side",
                            root,
                            new Vector3(0, .13f, side * .165f),
                            new Vector3(.5f, .26f, .03f),
                            basket,
                            .012f
                        );
                    }
                    string[] cloth = { "#E9A6A6", "#F2E0A7", "#A9C9A4" };
                    for (int index = 0; index < 3; index++)
                        Block(
                            "Clothes",
                            root,
                            new Vector3(-.12f + index * .12f, .2f + index % 2 * .04f, 0),
                            new Vector3(.2f, .14f, .26f),
                            Paint("Towel" + index, cloth[index]),
                            .05f
                        );
                }
            );

        internal static Transform DetergentBottles(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "DetergentBottles",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    string[] colors = { "#F08A4B", "#2F7C86", "#7FAD50", "#E85D75" };
                    for (int index = 0; index < colors.Length; index++)
                    {
                        float x = -.33f + index * .22f;
                        Material body = Paint(
                            "Detergent" + colors[index].TrimStart('#'),
                            colors[index]
                        );
                        Block(
                            "Bottle",
                            root,
                            new Vector3(x, .14f, 0),
                            new Vector3(.16f, .28f, .12f),
                            body,
                            .04f
                        );
                        Post("Cap", root, new Vector3(x + .03f, .3f, 0), .035f, .05f, White);
                        Block(
                            "Label",
                            root,
                            new Vector3(x, .14f, .061f),
                            new Vector3(.11f, .1f, .005f),
                            White,
                            .004f
                        );
                    }
                }
            );

        // 바퀴 달린 빨래 수레. 위에 옷걸이 봉이 있다.
        internal static Transform LaundryCart(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "LaundryCart",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Block(
                        "Basket",
                        root,
                        new Vector3(0, .5f, 0),
                        new Vector3(.7f, .4f, .5f),
                        Paint("CartWire", "#C7CDD1"),
                        .04f
                    );
                    Block(
                        "Clothes",
                        root,
                        new Vector3(0, .66f, 0),
                        new Vector3(.6f, .14f, .4f),
                        Paint("Towel1", "#F2E0A7"),
                        .06f
                    );
                    foreach (int side in new[] { -1, 1 })
                    {
                        Post(
                            "Upright",
                            root,
                            new Vector3(side * .33f, 1.0f, -.22f),
                            .015f,
                            .9f,
                            Steel
                        );
                        foreach (int z in new[] { -1, 1 })
                        {
                            Post(
                                "Leg",
                                root,
                                new Vector3(side * .3f, .16f, z * .2f),
                                .015f,
                                .24f,
                                Steel
                            );
                            Ball(
                                "Caster",
                                root,
                                new Vector3(side * .3f, .04f, z * .2f),
                                Vector3.one * .08f,
                                Leather
                            );
                        }
                    }
                    Block(
                        "HangerBar",
                        root,
                        new Vector3(0, 1.44f, -.22f),
                        new Vector3(.7f, .03f, .03f),
                        Steel,
                        .012f
                    );
                }
            );

        // 세제 자판기
        internal static Transform DetergentVending(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "DetergentVending",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Material teal = Paint("LaundryTeal", "#2F7C86");
                    Block(
                        "Body",
                        root,
                        new Vector3(0, .85f, 0),
                        new Vector3(.8f, 1.7f, .6f),
                        teal,
                        .05f
                    );
                    Block(
                        "Glass",
                        root,
                        new Vector3(-.08f, 1.0f, .3f),
                        new Vector3(.5f, 1.0f, .02f),
                        Paint("VendingGlass", "#DDEEF0"),
                        .02f
                    );
                    string[] colors = { "#F08A4B", "#7FAD50", "#E85D75", "#F4C430" };
                    for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 3; column++)
                        Block(
                            "Box",
                            root,
                            new Vector3(-.24f + column * .16f, .62f + row * .24f, .26f),
                            new Vector3(.12f, .16f, .04f),
                            Paint("Detergent" + colors[row].TrimStart('#'), colors[row]),
                            .01f
                        );
                    Block(
                        "Coins",
                        root,
                        new Vector3(.3f, 1.05f, .31f),
                        new Vector3(.1f, .3f, .02f),
                        Steel,
                        .01f
                    );
                    Block(
                        "Tray",
                        root,
                        new Vector3(-.08f, .28f, .31f),
                        new Vector3(.4f, .14f, .03f),
                        Leather,
                        .02f
                    );
                    Text("Soap", "SOAP", root, new Vector3(0, 1.6f, .31f), .1f);
                }
            );

        // 옷걸이에 셔츠를 건 봉
        internal static Transform ClothesRack(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "ClothesRack",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        Post("Upright", root, new Vector3(side * .6f, .8f, 0), .02f, 1.6f, Steel);
                        Block(
                            "Foot",
                            root,
                            new Vector3(side * .6f, .02f, 0),
                            new Vector3(.06f, .04f, .45f),
                            Steel,
                            .01f
                        );
                    }
                    Block(
                        "Bar",
                        root,
                        new Vector3(0, 1.58f, 0),
                        new Vector3(1.24f, .03f, .03f),
                        Steel,
                        .012f
                    );
                    string[] shirts = { "#E9A6A6", "#9CC3D5", "#F7F3EA", "#A9C9A4", "#F2E0A7" };
                    for (int index = 0; index < shirts.Length; index++)
                    {
                        float x = -.44f + index * .22f;
                        Block(
                            "Shirt",
                            root,
                            new Vector3(x, 1.22f, 0),
                            new Vector3(.06f, .62f, .4f),
                            Paint("Shirt" + shirts[index].TrimStart('#'), shirts[index]),
                            .03f
                        );
                        Block(
                            "Hanger",
                            root,
                            new Vector3(x, 1.54f, 0),
                            new Vector3(.02f, .04f, .36f),
                            DarkWood,
                            .008f
                        );
                    }
                }
            );

        // 코르크 게시판과 메모
        internal static Transform NoticeBoard(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "NoticeBoard",
                parent,
                position,
                yaw,
                "note",
                root =>
                {
                    Block(
                        "Cork",
                        root,
                        new Vector3(0, 0, .02f),
                        new Vector3(1.1f, .75f, .04f),
                        Paint("NoticeCork", "#B98A5B"),
                        .015f
                    );
                    Block(
                        "Frame",
                        root,
                        new Vector3(0, 0, .01f),
                        new Vector3(1.18f, .83f, .02f),
                        Wood,
                        .008f
                    );
                    string[] notes = { "#F7F3EA", "#F4E07A", "#9CC3D5", "#F7F3EA", "#E9A6A6" };
                    for (int index = 0; index < notes.Length; index++)
                        Block(
                            "Note",
                            root,
                            new Vector3(-.4f + index * .2f, (index % 2) * .2f - .08f, .045f),
                            new Vector3(.16f, .2f, .005f),
                            Paint("Note" + notes[index].TrimStart('#'), notes[index]),
                            .003f
                        ).localRotation = Quaternion.Euler(0, 0, (index - 2) * 4);
                }
            );

        // 벽에 단 동전 교환기
        internal static Transform ChangeMachine(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "ChangeMachine",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Block(
                        "Body",
                        root,
                        new Vector3(0, 0, .14f),
                        new Vector3(.5f, .75f, .28f),
                        Steel,
                        .04f
                    );
                    Block(
                        "Panel",
                        root,
                        new Vector3(0, .12f, .285f),
                        new Vector3(.36f, .3f, .02f),
                        Paint("BinDark", "#6E777D"),
                        .02f
                    );
                    Block(
                        "Slot",
                        root,
                        new Vector3(0, .15f, .3f),
                        new Vector3(.16f, .03f, .01f),
                        Leather,
                        .008f
                    );
                    Block(
                        "Cup",
                        root,
                        new Vector3(0, -.24f, .29f),
                        new Vector3(.22f, .1f, .06f),
                        Leather,
                        .02f
                    );
                    Text("Change", "CHANGE", root, new Vector3(0, .3f, .3f), .07f);
                }
            );

        // ---------- 꽃집 ----------

        // 벽 선반과 작은 화분 세 개
        internal static Transform PlantShelf(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "PlantShelf",
                parent,
                position,
                yaw,
                "shelf",
                root =>
                {
                    Block(
                        "Board",
                        root,
                        new Vector3(0, -.02f, .14f),
                        new Vector3(1.4f, .04f, .28f),
                        Wood,
                        .01f
                    );
                    foreach (int side in new[] { -1, 1 })
                        Block(
                            "Bracket",
                            root,
                            new Vector3(side * .55f, -.1f, .08f),
                            new Vector3(.03f, .14f, .16f),
                            DarkWood,
                            .008f
                        );
                    string[] blooms = { "#E85D75", "#F4C430", "#B07CC6" };
                    Material pot = Paint("Terracotta", "#C8734F");
                    Material leaf = Paint("LeafGreen", "#6F9E45");
                    for (int index = 0; index < 3; index++)
                    {
                        float x = -.45f + index * .45f;
                        Post("Pot", root, new Vector3(x, .08f, .14f), .08f, .16f, pot);
                        SmoothBall(
                            "Leaves",
                            root,
                            new Vector3(x, .22f, .14f),
                            new Vector3(.2f, .16f, .2f),
                            leaf
                        );
                        for (int bloom = 0; bloom < 3; bloom++)
                            Ball(
                                "Bloom",
                                root,
                                new Vector3(
                                    x - .05f + bloom * .05f,
                                    .3f,
                                    .14f + (bloom % 2) * .04f
                                ),
                                Vector3.one * .06f,
                                Paint("Petal" + blooms[index].TrimStart('#'), blooms[index])
                            );
                    }
                }
            );

        // 천장 고리에 건 늘어진 화분. 원점은 천장에 닿는 곳이다.
        internal static Transform HangingPlant(Transform parent, Vector3 position) =>
            Piece(
                "HangingPlant",
                parent,
                position,
                0,
                "plant",
                root =>
                {
                    Material rope = Paint("Wicker", "#C99A5B");
                    for (int index = 0; index < 3; index++)
                    {
                        float angle = index * 120 * Mathf.Deg2Rad;
                        Post(
                            "Rope",
                            root,
                            new Vector3(Mathf.Cos(angle) * .06f, -.3f, Mathf.Sin(angle) * .06f),
                            .005f,
                            .6f,
                            rope
                        );
                    }
                    Post(
                        "Pot",
                        root,
                        new Vector3(0, -.68f, 0),
                        .14f,
                        .18f,
                        Paint("Terracotta", "#C8734F")
                    );
                    Material leaf = Paint("LeafGreen", "#6F9E45");
                    SmoothBall(
                        "Leaves",
                        root,
                        new Vector3(0, -.58f, 0),
                        new Vector3(.36f, .2f, .36f),
                        leaf
                    );
                    for (int index = 0; index < 5; index++)
                    {
                        float angle = index * 72 * Mathf.Deg2Rad;
                        SmoothBall(
                            "Trail",
                            root,
                            new Vector3(Mathf.Cos(angle) * .15f, -.82f, Mathf.Sin(angle) * .15f),
                            new Vector3(.1f, .3f, .1f),
                            leaf
                        );
                    }
                }
            );

        // 유리문 꽃 냉장고. 안에 꽃 통을 넣었다.
        internal static Transform FlowerCooler(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "FlowerCooler",
                parent,
                position,
                yaw,
                "refrigerator",
                root =>
                {
                    Block(
                        "Body",
                        root,
                        new Vector3(0, .95f, 0),
                        new Vector3(1.2f, 1.9f, .6f),
                        White,
                        .05f
                    );
                    Block(
                        "Inside",
                        root,
                        new Vector3(0, 1.0f, .05f),
                        new Vector3(1.04f, 1.6f, .5f),
                        Paint("CoolerInside", "#DCE9EC"),
                        .02f
                    );
                    string[] petals =
                    {
                        "#E85D75",
                        "#F4C430",
                        "#F7F3EA",
                        "#B07CC6",
                        "#F08A4B",
                        "#E9A6A6",
                    };
                    for (int level = 0; level < 2; level++)
                    {
                        Block(
                            "Shelf",
                            root,
                            new Vector3(0, .25f + level * .78f, .05f),
                            new Vector3(1.04f, .03f, .48f),
                            Steel,
                            .008f
                        );
                        for (int index = 0; index < 3; index++)
                            Bucket(
                                root,
                                new Vector3(-.34f + index * .34f, .27f + level * .78f, .06f),
                                petals[level * 3 + index]
                            );
                    }
                    Block(
                        "Glass",
                        root,
                        new Vector3(0, 1.0f, .31f),
                        new Vector3(1.06f, 1.62f, .02f),
                        Paint("CoolerGlass", "#E3F1F4"),
                        .02f
                    );
                    Block(
                        "Handle",
                        root,
                        new Vector3(.45f, 1.0f, .34f),
                        new Vector3(.03f, .5f, .03f),
                        Steel,
                        .01f
                    );
                }
            );

        // 꽃을 꽂은 함석 통. 바닥이나 진열대에 둔다.
        internal static Transform FlowerPail(Transform parent, Vector3 position, string petalHex) =>
            Piece(
                "FlowerPail" + petalHex.TrimStart('#'),
                parent,
                position,
                0,
                "flower",
                root => Bucket(root, Vector3.zero, petalHex)
            );

        private static void Bucket(Transform root, Vector3 bottom, string petalHex)
        {
            Post("Pail", root, bottom + Vector3.up * .13f, .13f, .26f, Steel);
            Material stem = Paint("FlowerStem", "#5E8C3A");
            Material petal = Paint("Petal" + petalHex.TrimStart('#'), petalHex);
            for (int index = 0; index < 6; index++)
            {
                float angle = index * 60 * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * .06f, 0, Mathf.Sin(angle) * .06f);
                Post("Stem", root, bottom + offset + Vector3.up * .32f, .01f, .3f, stem);
                Ball(
                    "Bloom",
                    root,
                    bottom + offset * 1.4f + Vector3.up * (.47f + index % 2 * .04f),
                    Vector3.one * .11f,
                    petal
                );
            }
        }

        // 종이로 싼 꽃다발
        internal static Transform Bouquet(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "Bouquet",
                parent,
                position,
                yaw,
                "flower",
                root =>
                {
                    Transform wrap = Post(
                        "Wrap",
                        root,
                        new Vector3(0, .06f, 0),
                        .08f,
                        .4f,
                        Paint("WrapPaper", "#F3D9C9")
                    );
                    wrap.localRotation = Quaternion.Euler(0, 0, 80);
                    string[] petals = { "#E85D75", "#F7F3EA", "#F4C430", "#E85D75" };
                    for (int index = 0; index < petals.Length; index++)
                        Ball(
                            "Bloom",
                            root,
                            new Vector3(-.22f, .07f + (index % 2) * .06f, -.06f + index * .04f),
                            Vector3.one * .1f,
                            Paint("Petal" + petals[index].TrimStart('#'), petals[index])
                        );
                    Block(
                        "Ribbon",
                        root,
                        new Vector3(.02f, .06f, 0),
                        new Vector3(.03f, .14f, .14f),
                        Paint("RibbonRed", "#C8463B"),
                        .01f
                    );
                }
            );

        // 벽에 단 포장지 롤과 리본 실패
        internal static Transform WrappingRoll(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "WrappingRoll",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    foreach (int side in new[] { -1, 1 })
                        Block(
                            "Bracket",
                            root,
                            new Vector3(side * .5f, 0, .1f),
                            new Vector3(.04f, .16f, .2f),
                            DarkWood,
                            .01f
                        );
                    Post(
                        "Paper",
                        root,
                        new Vector3(0, 0, .14f),
                        .09f,
                        .94f,
                        Paint("WrapPaper", "#F3D9C9")
                    ).localRotation = Quaternion.Euler(0, 0, 90);
                    Post(
                        "Kraft",
                        root,
                        new Vector3(0, -.22f, .14f),
                        .07f,
                        .94f,
                        Paint("Cardboard", "#C49A63")
                    ).localRotation = Quaternion.Euler(0, 0, 90);
                    string[] ribbons = { "#C8463B", "#F4C430", "#B07CC6", "#2F7C86" };
                    for (int index = 0; index < ribbons.Length; index++)
                        Post(
                            "Ribbon",
                            root,
                            new Vector3(-.3f + index * .2f, .2f, .12f),
                            .05f,
                            .05f,
                            Paint("Ribbon" + ribbons[index].TrimStart('#'), ribbons[index])
                        ).localRotation = Quaternion.Euler(0, 0, 90);
                }
            );

        internal static Transform WateringCan(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "WateringCan",
                parent,
                position,
                yaw,
                null,
                root =>
                {
                    Material green = Paint("CanGreen", "#5E9C7E");
                    Post("Body", root, new Vector3(0, .12f, 0), .1f, .24f, green);
                    Post(
                        "Spout",
                        root,
                        new Vector3(.16f, .2f, 0),
                        .015f,
                        .28f,
                        green
                    ).localRotation = Quaternion.Euler(0, 0, -55);
                    Block(
                        "Handle",
                        root,
                        new Vector3(-.04f, .28f, 0),
                        new Vector3(.16f, .03f, .03f),
                        green,
                        .01f
                    );
                }
            );

        // ---------- 오락실 ----------

        // 화면이 비스듬한 오락기. 화면 색만 다른 세 종류를 쓴다.
        internal static Transform ArcadeCabinet(
            Transform parent,
            Vector3 position,
            float yaw,
            string hex
        ) =>
            Piece(
                "ArcadeCabinet" + hex.TrimStart('#'),
                parent,
                position,
                yaw,
                "game_console",
                root =>
                {
                    Material body = Paint("Arcade" + hex.TrimStart('#'), hex);
                    Material black = Paint("CombBlack", "#262626");
                    Block(
                        "Body",
                        root,
                        new Vector3(0, .85f, -.05f),
                        new Vector3(.72f, 1.7f, .6f),
                        body,
                        .04f
                    );
                    Block(
                        "Bezel",
                        root,
                        new Vector3(0, 1.22f, .22f),
                        new Vector3(.6f, .5f, .1f),
                        black,
                        .03f
                    ).localRotation = Quaternion.Euler(-14, 0, 0);
                    Block(
                        "Screen",
                        root,
                        new Vector3(0, 1.22f, .28f),
                        new Vector3(.5f, .4f, .01f),
                        Paint("ArcadeScreen", "#4FD1C5"),
                        .01f
                    ).localRotation = Quaternion.Euler(-14, 0, 0);
                    Block(
                        "Panel",
                        root,
                        new Vector3(0, .9f, .32f),
                        new Vector3(.7f, .1f, .3f),
                        black,
                        .03f
                    ).localRotation = Quaternion.Euler(12, 0, 0);
                    Post("Stick", root, new Vector3(-.15f, .99f, .32f), .012f, .1f, Steel);
                    Ball(
                        "Knob",
                        root,
                        new Vector3(-.15f, 1.05f, .32f),
                        Vector3.one * .06f,
                        Paint("JukeboxRed", "#C8463B")
                    );
                    for (int index = 0; index < 3; index++)
                        Post(
                            "Button",
                            root,
                            new Vector3(.05f + index * .09f, .97f, .33f),
                            .025f,
                            .02f,
                            Paint(
                                "Bubble" + index,
                                new[] { "#5FA8D3", "#F4C430", "#7FAD50" }[index]
                            )
                        );
                    Block(
                        "Marquee",
                        root,
                        new Vector3(0, 1.6f, .24f),
                        new Vector3(.62f, .16f, .04f),
                        Paint("MarqueeLight", "#FFE7A3"),
                        .02f
                    );
                    Text("Title", "PLAY", root, new Vector3(0, 1.6f, .265f), .09f).color =
                        Leather.color;
                    Block(
                        "CoinDoor",
                        root,
                        new Vector3(0, .45f, .26f),
                        new Vector3(.24f, .3f, .02f),
                        black,
                        .015f
                    );
                }
            );

        // ---------- 공용 ----------

        // 액자 그림. KayKit 큰 액자는 그림 면이 비어 보여 색면 그림을 직접 넣는다. 원점은 벽에 닿는 뒷면 가운데다.
        internal static Transform FramedPrint(
            Transform parent,
            Vector3 position,
            float yaw,
            string style
        ) =>
            Piece(
                style + "Print",
                parent,
                position,
                yaw,
                "picture",
                root =>
                {
                    Vector2 size = style switch
                    {
                        "Landscape" => new Vector2(.9f, .65f),
                        "Botanical" => new Vector2(.55f, .75f),
                        _ => new Vector2(1.1f, .6f),
                    };
                    Block(
                        "Frame",
                        root,
                        new Vector3(0, 0, .02f),
                        new Vector3(size.x, size.y, .04f),
                        style == "Botanical" ? Navy : Wood,
                        .015f
                    );
                    Block(
                        "Mat",
                        root,
                        new Vector3(0, 0, .042f),
                        new Vector3(size.x - .1f, size.y - .1f, .005f),
                        Cream,
                        .003f
                    );
                    float width = size.x - .18f;
                    float height = size.y - .18f;
                    if (style == "Landscape")
                    {
                        Block(
                            "Sky",
                            root,
                            new Vector3(0, .05f, .046f),
                            new Vector3(width, height - .1f, .004f),
                            Paint("PrintSky", "#9CC3D5"),
                            .002f
                        );
                        Ball(
                            "Hill",
                            root,
                            new Vector3(-.12f, -height / 2 + .06f, .049f),
                            new Vector3(width * .7f, .22f, .004f),
                            Paint("PrintHill", "#79A331")
                        );
                        Ball(
                            "Hill",
                            root,
                            new Vector3(.16f, -height / 2 + .04f, .051f),
                            new Vector3(width * .6f, .16f, .004f),
                            Paint("PrintHillDark", "#4E7F3A")
                        );
                        Ball(
                            "Sun",
                            root,
                            new Vector3(width / 2 - .12f, height / 2 - .1f, .049f),
                            new Vector3(.1f, .1f, .004f),
                            Paint("PrintSun", "#F4C430")
                        );
                    }
                    else if (style == "Botanical")
                    {
                        Material leaf = Paint("PrintLeaf", "#5E8C3A");
                        Block(
                            "Stem",
                            root,
                            new Vector3(0, -.05f, .047f),
                            new Vector3(.015f, height - .1f, .004f),
                            leaf,
                            .002f
                        );
                        for (int index = 0; index < 4; index++)
                        {
                            int side = index % 2 == 0 ? -1 : 1;
                            Ball(
                                "Leaf",
                                root,
                                new Vector3(side * .07f, -.15f + index * .1f, .049f),
                                new Vector3(.14f, .06f, .004f),
                                leaf
                            ).localRotation = Quaternion.Euler(0, 0, side * 30);
                        }
                        Ball(
                            "Bud",
                            root,
                            new Vector3(0, height / 2 - .08f, .049f),
                            new Vector3(.08f, .08f, .004f),
                            Paint("PetalE85D75", "#E85D75")
                        );
                    }
                    else
                    {
                        string[] bands = { "#F0D29C", "#F08A4B", "#E58B63", "#293E57" };
                        for (int index = 0; index < bands.Length; index++)
                            Block(
                                "Band",
                                root,
                                new Vector3(
                                    0,
                                    height / 2 - (index + .5f) * height / bands.Length,
                                    .046f
                                ),
                                new Vector3(width, height / bands.Length, .004f),
                                Paint("PrintBand" + bands[index].TrimStart('#'), bands[index]),
                                .002f
                            );
                        Ball(
                            "Sun",
                            root,
                            new Vector3(0, -height / 8, .049f),
                            new Vector3(.18f, .18f, .004f),
                            Paint("PrintSun", "#F4C430")
                        );
                    }
                }
            );

        // 둥근 찻주전자
        internal static Transform Teapot(
            Transform parent,
            Vector3 position,
            float yaw,
            string hex
        ) =>
            Piece(
                "Teapot" + hex.TrimStart('#'),
                parent,
                position,
                yaw,
                "kettle",
                root =>
                {
                    Material body = Paint("Teapot" + hex.TrimStart('#'), hex);
                    SmoothBall(
                        "Pot",
                        root,
                        new Vector3(0, .1f, 0),
                        new Vector3(.22f, .19f, .22f),
                        body
                    );
                    Post("Lid", root, new Vector3(0, .2f, 0), .05f, .03f, body);
                    Ball("Knob", root, new Vector3(0, .225f, 0), Vector3.one * .03f, body);
                    Post(
                        "Spout",
                        root,
                        new Vector3(.14f, .13f, 0),
                        .018f,
                        .14f,
                        body
                    ).localRotation = Quaternion.Euler(0, 0, -50);
                    Block(
                        "Handle",
                        root,
                        new Vector3(-.13f, .12f, 0),
                        new Vector3(.03f, .12f, .03f),
                        body,
                        .012f
                    );
                }
            );

        // 꽃을 꽂은 꽃병
        internal static Transform Vase(Transform parent, Vector3 position, string hex) =>
            Piece(
                "Vase" + hex.TrimStart('#'),
                parent,
                position,
                0,
                "vase",
                root =>
                {
                    SmoothBall(
                        "Body",
                        root,
                        new Vector3(0, .1f, 0),
                        new Vector3(.16f, .2f, .16f),
                        Paint("Vase" + hex.TrimStart('#'), hex)
                    );
                    Post(
                        "Neck",
                        root,
                        new Vector3(0, .22f, 0),
                        .04f,
                        .06f,
                        Paint("Vase" + hex.TrimStart('#'), hex)
                    );
                    Material stem = Paint("FlowerStem", "#5E8C3A");
                    for (int index = 0; index < 3; index++)
                    {
                        float x = (index - 1) * .05f;
                        Post("Stem", root, new Vector3(x, .32f, 0), .008f, .2f, stem);
                        Ball(
                            "Bloom",
                            root,
                            new Vector3(x * 1.4f, .44f + index % 2 * .03f, 0),
                            Vector3.one * .07f,
                            Paint("PetalF7F3EA", "#F7F3EA")
                        );
                    }
                }
            );

        // 받침 두 개 사이에 판을 얹은 벽 선반. 위에 무엇을 올릴지는 배치에서 정한다.
        internal static Transform PlainShelf(Transform parent, Vector3 position, float yaw) =>
            Piece(
                "PlainShelf",
                parent,
                position,
                yaw,
                "shelf",
                root =>
                {
                    Block(
                        "Board",
                        root,
                        new Vector3(0, -.02f, .14f),
                        new Vector3(1.3f, .04f, .28f),
                        Wood,
                        .01f
                    );
                    foreach (int side in new[] { -1, 1 })
                        Block(
                            "Bracket",
                            root,
                            new Vector3(side * .5f, -.1f, .08f),
                            new Vector3(.03f, .14f, .16f),
                            DarkWood,
                            .008f
                        );
                }
            );

        // ---------- 공통 ----------

        // 바닥에 놓는 큰 조각. 이동 충돌을 단다. 벽에 거는 것과 작은 소품은 지나가도 어색하지 않아 충돌을 두지 않는다.
        private static readonly string[] SolidPieces =
        {
            "BarberStation",
            "ToolTrolley",
            "ShampooSink",
            "WashChair",
            "HoodDryerChair",
            "CoatRack",
            "MagazineRack",
            "BreadRack",
            "BakeryOven",
            "BakersRack",
            "Jukebox",
            "MailCart",
            "ParcelStack",
            "FormDesk",
            "BlueMailbox",
            "BrochureRack",
            "WaterCooler",
            "LaundryCart",
            "DetergentVending",
            "ClothesRack",
            "FlowerCooler",
            "ArcadeCabinet",
        };

        private static Transform Piece(
            string name,
            Transform parent,
            Vector3 position,
            float yaw,
            string word,
            Action<Transform> build
        ) =>
            TownShopInteriorProps.Piece(
                name,
                parent,
                position,
                yaw,
                SolidPieces.Any(solid => name.StartsWith(solid)),
                () =>
                {
                    var root = new GameObject(name);
                    build(root.transform);
                    if (word != null)
                        NorthBlockAssets.Bind(root, word);
                    return root;
                }
            );

        private static void Treat(
            string category,
            string name,
            Transform parent,
            Vector3 position,
            float yaw,
            float scale
        )
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TinyTreatsPropsBuilder.Prefabs + category + "/" + name + ".prefab"
            );
            if (prefab == null)
                throw new InvalidOperationException(
                    "Tiny Treats 프리팹이 없습니다: " + category + "/" + name
                );
            GameObject placed = BuilderTools.InstantiatePrefab(
                prefab,
                parent,
                position,
                Quaternion.Euler(0, yaw, 0)
            );
            placed.transform.localScale = Vector3.one * scale;
            // 선반에 올린 빵이라 이동 충돌이 필요 없다. 크게 키운 빵의 상자는 겉모양보다 떠 보인다.
            TownShopInteriorProps.RemoveMovementColliders(placed);
        }

        internal static Material Paint(string name, string hex)
        {
            string path = TownShopInteriorProps.ArtFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            ColorUtility.TryParseHtmlString(hex, out UnityEngine.Color color);
            Material material = BuilderTools.GetOrCreateMaterial(
                TownShopInteriorProps.ArtFolder,
                name,
                color
            );
            material.SetFloat("_Smoothness", .18f);
            material.enableInstancing = true;
            return material;
        }

        private static Material DarkWood => Paint("DarkWood", "#6B4A32");
        private static Material Steel => Paint("InteriorSteel", "#A7B0B6");
        private static Material White => Paint("SoftWhite", "#F4F1EA");
        private static Material Leather => Paint("Leather", "#2E2B2A");
        private static Material BarberRed => Paint("BarberRed", "#B23A3A");
    }
}
