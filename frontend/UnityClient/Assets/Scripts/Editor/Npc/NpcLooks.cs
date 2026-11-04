namespace A305.EditorTools.Npc
{
    /// <summary>
    /// NPC가 쓰는 리틀 멍 외형과 색 범위. 플레이어(기본) 외형은 NPC에 쓰지 않는다.
    /// 카페 유니폼(바리스타)은 카페에서만, 호텔 유니폼은 호텔에서만 쓰고, 경찰 제복은 호텔 유니폼의 바지·구두를 남색으로 바꾼 외형이다.
    /// 색 범위는 텍스처를 보고 정했다. 머리카락이 피부·바지와 비슷한 색인 외형은 몸 높이(머리는 0.5~0.6 위)로 나누고,
    /// 범위를 넓히면 피부까지 바뀌는 가닥 사이 선은 <see cref="NpcColorSwap.FillGaps"/>로 메운다.
    /// </summary>
    public static class NpcLooks
    {
        private const string ReviewFolder = "Assets/Prefabs/Npc/Review/LittleMung_";
        private const string TripoNpcFolder = "Assets/Art/Characters/Tripo/NPC/LittleMung_";
        private const string HotelierPrefab =
            "Assets/Prefabs/Characters/Tripo/LittleMung_HotelStaff_male.prefab";
        private const string HotelierTexture =
            "Assets/Art/Characters/Tripo/LittleMung_HotelStaff_male/Textures/Material_0_color.png";

        // 머리(얼굴·머리카락)가 그려진 몸 높이와 발(신발) 높이. 리틀 멍은 머리가 커서 몸 높이의 0.6 위가 머리다.
        private static readonly (float, float) Head = (.6f, 1f);
        private static readonly (float, float) Feet = (0f, .1f);

        // 목덜미·어깨까지 내려오는 뒷머리를 포함한 높이. 머리카락과 색이 비슷한 바지(몸 높이 0.3 아래)는 뺀다.
        private static readonly (float, float) HeadAndNape = (.4f, 1f);

        // 호텔 유니폼의 뒷머리 높이. 머리카락과 색이 비슷한 금색 옷깃·넥타이(0.48 아래)는 뺀다.
        private static readonly (float, float) HeadAboveCollar = (.5f, 1f);

        // 어깨까지 내려오는 땋은 머리를 포함한 높이(바리스타). 신발은 머리카락과 색이 비슷해 뺀다.
        private static readonly (float, float) HeadAndBraids = (.3f, 1f);

        // 셔츠·윗옷이 있는 몸통 높이(옷깃은 0.44). 같은 색인 입술(0.52)·눈썹은 뺀다.
        private static readonly (float, float) Torso = (.1f, .48f);

        // 따로 둔 눈 메시의 눈 색 텍스처. 흰자(채도 낮고 밝음)와 동공(아주 어두움)은 남기고, 어두운 눈동자는 밝혀 색이 보이게 한다.
        private static readonly NpcColorRange[] SeparateIris =
        {
            new NpcColorRange((0f, 1f), (.12f, 1f), (.12f, .95f)),
            new NpcColorRange((0f, 1f), (0f, .12f), (.08f, .35f), brighten: 1.6f),
        };

        public static readonly NpcLook Barista = new NpcLook(
            "Barista",
            "Assets/Prefabs/Npc/Barista.prefab",
            "Assets/Art/Npc/Barista/BaristaColor.png",
            onlyForCodePrefix: "CAFE_"
        )
            .Originally("blond", "sage", "brown", "green", secondClothes: "rust")
            .WithHair(new NpcColorRange((.085f, .16f), (.35f, .85f), (.55f, 1f), HeadAndBraids))
            .WithClothes(new NpcColorRange((.22f, .48f), (.12f, .6f), (.25f, .85f)))
            .WithSecondClothes(new NpcColorRange((.98f, .06f), (.45f, .85f), (.3f, .85f), Torso))
            .WithShoes(new NpcColorRange((.03f, .1f), (.25f, .7f), (.2f, .65f), Feet))
            .WithEyes(true, new NpcColorRange((.15f, .5f), (.15f, 1f), (.05f, .5f), Head, 1.4f));

        public static readonly NpcLook Hotelier = new NpcLook(
            "HotelStaff",
            HotelierPrefab,
            HotelierTexture,
            onlyForCodePrefix: "HOTEL_"
        )
            .Originally("light brown", "navy", "dark brown", "brown", secondClothes: "dark brown")
            .WithHair(new NpcColorRange((.07f, .14f), (.32f, .75f), (.4f, .86f), HeadAboveCollar))
            .WithClothes(new NpcColorRange((.58f, .72f), (.2f, .85f), (.1f, .6f)))
            .WithSecondClothes(
                new NpcColorRange((.98f, .09f), (.3f, .75f), (.08f, .42f), (.1f, .6f))
            )
            .WithShoes(new NpcColorRange((.98f, .09f), (.2f, .8f), (.05f, .42f), Feet))
            .WithEyes(true, new NpcColorRange((.02f, .12f), (.2f, 1f), (.05f, .32f), Head, 1.8f));

        // 경찰 제복: 호텔 유니폼의 어두운 갈색(조끼·바지·구두)을 남색으로 바꾼 재질(PoliceStationBuilder)이 바탕이다.
        public static readonly NpcLook PoliceOfficer = new NpcLook(
            "PoliceOfficer",
            HotelierPrefab,
            HotelierTexture,
            onlyForCodePrefix: "POLICE_",
            baseMaterialPath: "Assets/Art/World/PoliceStation/PoliceOfficerUniform.mat"
        )
            .Originally("light brown", "navy", "navy", "brown")
            .WithHair(new NpcColorRange((.07f, .14f), (.32f, .75f), (.4f, .86f), HeadAboveCollar))
            .WithClothes(new NpcColorRange((.58f, .72f), (.2f, .85f), (.1f, .6f), (.1f, 1f)))
            .WithShoes(new NpcColorRange((.58f, .72f), (.2f, .85f), (.05f, .6f), Feet))
            .WithEyes(true, new NpcColorRange((.02f, .12f), (.2f, 1f), (.05f, .32f), Head, 1.8f));

        public static readonly NpcLook BlackYoungMale = Review("BlackYoung_Male")
            .Originally("dark brown", "blue", "white", "dark brown", secondClothes: "yellow")
            .WithHair(new NpcColorRange((0f, .1f), (.25f, .8f), (.04f, .45f), (.5f, 1f)))
            .WithClothes(new NpcColorRange((.58f, .7f), (.35f, 1f), (.25f, 1f)))
            .WithSecondClothes(new NpcColorRange((.08f, .15f), (.6f, 1f), (.6f, 1f)))
            .WithShoes(new NpcColorRange((0f, 1f), (0f, .12f), (.55f, 1f), Feet))
            .WithEyes(false, SeparateIris);

        public static readonly NpcLook BusinessMale = Review("Business_Male")
            .Originally("brown", "navy", "brown", "brown")
            .WithHair(new NpcColorRange((0f, .09f), (.3f, .7f), (.1f, .42f), HeadAndNape))
            .WithClothes(new NpcColorRange((.58f, .7f), (.25f, .7f), (.15f, .5f)))
            .WithShoes(new NpcColorRange((0f, .09f), (.3f, .7f), (.05f, .42f), Feet))
            .WithEyes(false, SeparateIris);

        // 셔츠·카키 바지·갈색 구두 차림이다. 재킷이 없고 어두운 갈색은 눈썹·벨트라 바꾸지 않는다.
        // 머리카락은 밝은 가닥 아래 채도 높은 황갈색 음영까지 고른다. 피부는 더 밝고(명도 0.9 위) 눈썹은 더 어둡다(0.4 아래).
        public static readonly NpcLook MiddleAgeMale = Review("MiddleAge_Male")
            .Originally("sandy", "teal", "dark brown", "blue")
            .WithHair(new NpcColorRange((.07f, .14f), (.18f, .8f), (.4f, .88f), HeadAndNape))
            .WithClothes(new NpcColorRange((.45f, .6f), (.2f, 1f), (.2f, 1f)))
            .WithShoes(new NpcColorRange((0f, .08f), (.3f, .7f), (.05f, .4f), Feet))
            .WithEyes(false, SeparateIris);

        public static readonly NpcLook MiddleAgeFemale = Review("MiddleAge_Female")
            .Originally("caramel", "blue", "cream", "dark blue")
            .WithHair(new NpcColorRange((.085f, .14f), (.4f, .85f), (.45f, .9f), HeadAndNape))
            .WithClothes(new NpcColorRange((.55f, .72f), (.2f, 1f), (.15f, 1f)))
            .WithShoes(new NpcColorRange((0f, 1f), (0f, .3f), (.6f, 1f), Feet))
            .WithEyes(false, SeparateIris);

        public static readonly NpcLook Grandmother = Review("Grandmother")
            .Originally("grey", "plum", "brown", "grey")
            .WithHair(new NpcColorRange((0f, 1f), (0f, .22f), (.3f, .9f), HeadAndNape))
            .WithClothes(new NpcColorRange((.88f, .02f), (.25f, .7f), (.3f, .75f)))
            .WithShoes(new NpcColorRange((.03f, .12f), (.15f, .5f), (.2f, .6f), Feet))
            .WithEyes(false, SeparateIris);

        public static readonly NpcLook[] All =
        {
            Barista,
            Hotelier,
            PoliceOfficer,
            BlackYoungMale,
            BusinessMale,
            MiddleAgeMale,
            MiddleAgeFemale,
            Grandmother,
        };

        private static NpcLook Review(string name)
        {
            return new NpcLook(
                name,
                ReviewFolder + name + ".prefab",
                TripoNpcFolder + name + "/Textures/Material_0_color.png"
            );
        }
    }
}
