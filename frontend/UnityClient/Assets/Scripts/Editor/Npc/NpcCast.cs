using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// 모든 NPC의 외형 표. NPC마다 외형(<see cref="NpcLooks"/>)과 머리·옷·신발·눈동자 배색을 정한다. 배색을 비운 부분은 외형의 원래 색이다.
    /// 같은 외형을 쓰는 NPC는 서로 다른 사람처럼 보여야 한다. 눈동자 색이 같으면 머리와 옷 색이 달라야 하고, 옷 색이 같으면 눈동자·머리·신발
    /// 색이 모두 달라야 한다(EditMode 테스트 NpcCastTests가 지킨다). 외형은 성별·나이대가 인격 카드와 맞게 골랐다.
    /// </summary>
    public static class NpcCast
    {
        public static readonly IReadOnlyList<NpcCastMember> Members = new[]
        {
            // ---------- 카페: 카페 유니폼은 카페 직원만 ----------
            Member("CAFE_STAFF", NpcLooks.Barista),
            Member(
                "CAFE_BARISTA",
                NpcLooks.Barista,
                hair: Palette.DarkBrownHair,
                secondClothes: Palette.Navy,
                shoes: Palette.BlackShoes,
                eyes: Palette.BrownEyes
            ),
            Member(
                "CAFE_SERVER",
                NpcLooks.BlackYoungMale,
                hair: Palette.BlackHair,
                clothes: Palette.Black,
                secondClothes: Palette.White,
                eyes: Palette.BrownEyes
            ),
            // ---------- 호텔: 호텔 유니폼은 호텔 직원만 ----------
            Member("HOTEL_STAFF", NpcLooks.Hotelier),
            Member(
                "HOTEL_RECEPTIONIST",
                NpcLooks.Hotelier,
                hair: Palette.BlackHair,
                secondClothes: Palette.Burgundy,
                eyes: Palette.GreenEyes
            ),
            Member(
                "HOTEL_BELLHOP",
                NpcLooks.Hotelier,
                hair: Palette.DarkBrownHair,
                clothes: Palette.Red,
                eyes: Palette.AmberEyes
            ),
            Member(
                "HOTEL_CONCIERGE",
                NpcLooks.MiddleAgeMale,
                hair: Palette.GreyHair,
                clothes: Palette.Charcoal,
                eyes: Palette.GreenEyes
            ),
            Member(
                "HOTEL_NIGHT_MANAGER",
                NpcLooks.MiddleAgeFemale,
                hair: Palette.GreyHair,
                clothes: Palette.Navy,
                eyes: Palette.GreyEyes
            ),
            // ---------- 드럭스토어 ----------
            Member("SHOP_STAFF", NpcLooks.BlackYoungMale),
            Member(
                "SHOP_PHARMACIST",
                NpcLooks.MiddleAgeFemale,
                hair: Palette.ChestnutHair,
                clothes: Palette.White,
                eyes: Palette.BlueEyes
            ),
            Member(
                "SHOP_NIGHT_CLERK",
                NpcLooks.MiddleAgeMale,
                hair: Palette.DarkBrownHair,
                clothes: Palette.Navy,
                eyes: Palette.HazelEyes
            ),
            // ---------- 경찰서: 같은 제복이라 눈동자·머리·신발로 구분한다 ----------
            Member("POLICE_OFFICER", NpcLooks.PoliceOfficer),
            Member(
                "POLICE_SERGEANT",
                NpcLooks.PoliceOfficer,
                hair: Palette.GreyHair,
                shoes: Palette.BlackShoes,
                eyes: Palette.BlueEyes
            ),
            Member(
                "POLICE_PATROL_OFFICER",
                NpcLooks.PoliceOfficer,
                hair: Palette.BlackHair,
                shoes: Palette.BrownShoes,
                eyes: Palette.GreenEyes
            ),
            Member(
                "POLICE_ROOKIE",
                NpcLooks.PoliceOfficer,
                hair: Palette.BlondHair,
                shoes: Palette.GreyShoes,
                eyes: Palette.GreyEyes
            ),
            // ---------- 마을 행인 ----------
            Member("TOWN_PASSERBY", NpcLooks.BusinessMale),
            Member(
                "TOWN_RETIREE",
                NpcLooks.Grandmother,
                clothes: Palette.Navy,
                eyes: Palette.BrownEyes
            ),
            Member(
                "TOWN_STUDENT",
                NpcLooks.BlackYoungMale,
                clothes: Palette.Denim,
                secondClothes: Palette.Red,
                eyes: Palette.HazelEyes
            ),
            Member(
                "TOWN_EVENING_WALKER",
                NpcLooks.MiddleAgeFemale,
                hair: Palette.DarkBrownHair,
                clothes: Palette.Charcoal,
                eyes: Palette.AmberEyes
            ),
            Member(
                "TOWN_SHOPPER",
                NpcLooks.MiddleAgeFemale,
                hair: Palette.BlackHair,
                clothes: Palette.Purple,
                eyes: Palette.TealEyes
            ),
            Member(
                "TOWN_HANDYMAN",
                NpcLooks.MiddleAgeMale,
                hair: Palette.GreyHair,
                clothes: Palette.Red,
                eyes: Palette.GreyEyes
            ),
            Member(
                "FIRE_CAPTAIN",
                NpcLooks.BusinessMale,
                hair: Palette.BlackHair,
                clothes: Palette.Burgundy,
                eyes: Palette.BlueEyes
            ),
            Member(
                "FIRE_FIGHTER",
                NpcLooks.MiddleAgeFemale,
                hair: Palette.BlondHair,
                clothes: Palette.Orange,
                eyes: Palette.VioletEyes
            ),
            Member(
                "FIRE_ROOKIE",
                NpcLooks.BlackYoungMale,
                hair: Palette.BlackHair,
                clothes: Palette.Navy,
                secondClothes: Palette.Charcoal,
                eyes: Palette.GreenEyes
            ),
            // 할아버지 외형이 없어 중년 남성 외형을 흰 머리·크림색 셔츠로 나이 들어 보이게 한다. 함께 걷는 개(Pepper) 모델은 없다.
            Member(
                "TOWN_GRANDFATHER",
                NpcLooks.MiddleAgeMale,
                hair: Palette.WhiteHair,
                clothes: Palette.Cream
            ),
            Member(
                "TOWN_GRANDMOTHER",
                NpcLooks.Grandmother,
                clothes: Palette.Pink,
                eyes: Palette.GreenEyes
            ),
            // ---------- 택시 ----------
            Member("TAXI_DRIVER", NpcLooks.MiddleAgeMale),
            // ---------- 거리 매장(BuildingStaffBuilder) ----------
            Member(
                "POST_OFFICE_STAFF",
                NpcLooks.MiddleAgeFemale,
                clothes: Palette.Berry,
                eyes: Palette.BrownEyes
            ),
            Member(
                "DINER_STAFF",
                NpcLooks.MiddleAgeFemale,
                clothes: Palette.Red,
                eyes: Palette.GreenEyes
            ),
            Member(
                "BANK_STAFF",
                NpcLooks.BusinessMale,
                hair: Palette.SilverHair,
                clothes: Palette.Slate,
                eyes: Palette.GreyEyes
            ),
            Member(
                "LAUNDROMAT_STAFF",
                NpcLooks.MiddleAgeMale,
                clothes: Palette.Mustard,
                eyes: Palette.BrownEyes
            ),
            Member(
                "FLOWER_SHOP_STAFF",
                NpcLooks.Grandmother,
                hair: Palette.LavenderHair,
                clothes: Palette.Green,
                eyes: Palette.TealEyes
            ),
            Member(
                "BARBER_STAFF",
                NpcLooks.MiddleAgeMale,
                hair: Palette.BlackHair,
                clothes: Palette.White,
                eyes: Palette.BrownEyes
            ),
            Member(
                "BAKERY_STAFF",
                NpcLooks.Grandmother,
                hair: Palette.WhiteHair,
                clothes: Palette.Rust,
                eyes: Palette.BlueEyes
            ),
            Member(
                "ARCADE_STAFF",
                NpcLooks.BlackYoungMale,
                hair: Palette.AuburnHair,
                clothes: Palette.Purple,
                secondClothes: Palette.Green,
                eyes: Palette.AmberEyes
            ),
            Member(
                "HOME_STORE_STAFF",
                NpcLooks.MiddleAgeFemale,
                clothes: Palette.Leaf,
                eyes: Palette.HazelEyes
            ),
        };

        public static NpcCastMember Find(string code)
        {
            return Members.SingleOrDefault(member => member.Code == code);
        }

        private static NpcCastMember Member(
            string code,
            NpcLook look,
            NpcTint? hair = null,
            NpcTint? clothes = null,
            NpcTint? secondClothes = null,
            NpcTint? shoes = null,
            NpcTint? eyes = null
        )
        {
            return new NpcCastMember(code, look, hair, clothes, secondClothes, shoes, eyes);
        }

        /// <summary>외형 표에 쓰는 색. 머리는 원래 머리색의 밝기를 배수로 맞춰 검은색·흰색도 만든다.</summary>
        public static class Palette
        {
            public static readonly NpcTint BlackHair = new NpcTint("black", .07f, .3f, .32f);
            public static readonly NpcTint DarkBrownHair = new NpcTint(
                "dark brown",
                .07f,
                .6f,
                .55f
            );
            public static readonly NpcTint ChestnutHair = new NpcTint("chestnut", .04f, .6f, .6f);
            public static readonly NpcTint AuburnHair = new NpcTint("auburn", .01f, .6f, 1.3f);
            public static readonly NpcTint BlondHair = new NpcTint("blond", .13f, .45f, 1.25f);
            public static readonly NpcTint GreyHair = new NpcTint("grey", .6f, .04f, .85f);
            public static readonly NpcTint SilverHair = new NpcTint("silver", .6f, .05f, 1.4f);
            public static readonly NpcTint WhiteHair = new NpcTint("white", .1f, .05f, 1.25f);
            public static readonly NpcTint LavenderHair = new NpcTint(
                "lavender",
                .78f,
                .18f,
                1.05f
            );

            public static readonly NpcTint Navy = new NpcTint("navy", .62f, .55f, .8f);
            public static readonly NpcTint Slate = new NpcTint("slate", .62f, .2f);
            public static readonly NpcTint Red = new NpcTint("red", 0f, .7f);
            public static readonly NpcTint Berry = new NpcTint("berry", .97f, .55f);
            public static readonly NpcTint Burgundy = new NpcTint("burgundy", .97f, .65f, .8f);
            public static readonly NpcTint Rust = new NpcTint("rust", .02f, .45f);
            public static readonly NpcTint Orange = new NpcTint("orange", .07f, .75f, 1.2f);
            public static readonly NpcTint Mustard = new NpcTint("mustard", .12f, .55f);
            public static readonly NpcTint Green = new NpcTint("green", .33f, .45f);
            public static readonly NpcTint Leaf = new NpcTint("leaf", .28f, .35f);
            public static readonly NpcTint Purple = new NpcTint("purple", .78f, .45f);
            public static readonly NpcTint Pink = new NpcTint("pink", .93f, .3f, 1.25f);
            public static readonly NpcTint Denim = new NpcTint("denim", .6f, .4f, 1.1f);
            public static readonly NpcTint White = new NpcTint("white", 0f, .03f, 1.9f);
            public static readonly NpcTint Cream = new NpcTint("cream", .11f, .15f, 1.5f);
            public static readonly NpcTint Charcoal = new NpcTint("charcoal", 0f, 0f, .6f);
            public static readonly NpcTint Black = new NpcTint("black", 0f, 0f, .35f);

            public static readonly NpcTint BlackShoes = new NpcTint("black", 0f, 0f, .4f);
            public static readonly NpcTint BrownShoes = new NpcTint("brown", .07f, .55f, .9f);
            public static readonly NpcTint GreyShoes = new NpcTint("grey", .6f, .05f, 1.4f);

            public static readonly NpcTint BrownEyes = new NpcTint("brown", .07f, .65f);
            public static readonly NpcTint AmberEyes = new NpcTint("amber", .11f, .8f);
            public static readonly NpcTint HazelEyes = new NpcTint("hazel", .17f, .55f);
            public static readonly NpcTint GreenEyes = new NpcTint("green", .33f, .55f);
            public static readonly NpcTint TealEyes = new NpcTint("teal", .48f, .5f);
            public static readonly NpcTint BlueEyes = new NpcTint("blue", .6f, .55f);
            public static readonly NpcTint GreyEyes = new NpcTint("grey", .58f, .2f);
            public static readonly NpcTint VioletEyes = new NpcTint("violet", .76f, .4f);
        }
    }

    /// <summary>외형 표의 NPC 한 명. 배색을 비운 부분은 외형의 원래 색이다.</summary>
    public sealed class NpcCastMember
    {
        // 배색을 바꾸지 않았어도 색을 칠하는 규칙이 바뀌면 이 값을 올려 텍스처를 다시 쓴다.
        private const int RecipeVersion = 1;

        public NpcCastMember(
            string code,
            NpcLook look,
            NpcTint? hair,
            NpcTint? clothes,
            NpcTint? secondClothes,
            NpcTint? shoes,
            NpcTint? eyes
        )
        {
            Code = code;
            Look = look;
            Hair = hair;
            Clothes = clothes;
            SecondClothes = secondClothes;
            Shoes = shoes;
            Eyes = eyes;
        }

        public string Code { get; }
        public NpcLook Look { get; }
        public NpcTint? Hair { get; }
        public NpcTint? Clothes { get; }
        public NpcTint? SecondClothes { get; }
        public NpcTint? Shoes { get; }
        public NpcTint? Eyes { get; }

        public string HairColor => Hair?.Name ?? Look.OriginalHair;

        /// <summary>입은 옷 전체의 색. 함께 입은 옷까지 같아야 같은 옷이다.</summary>
        public string ClothesColor =>
            (Clothes?.Name ?? Look.OriginalClothes)
            + " / "
            + (SecondClothes?.Name ?? Look.OriginalSecondClothes);

        public string ShoesColor => Shoes?.Name ?? Look.OriginalShoes;
        public string EyesColor => Eyes?.Name ?? Look.OriginalEyes;

        // 몸 색 텍스처에 그려진 부분의 배색. 눈 메시를 따로 둔 외형은 눈동자 배색만으로는 몸 텍스처를 바꾸지 않는다.
        private bool RecolorsBody =>
            Hair.HasValue
            || Clothes.HasValue
            || SecondClothes.HasValue
            || Shoes.HasValue
            || (Look.EyesInBody && Eyes.HasValue);

        /// <summary>
        /// 몸 색 텍스처에 칠할 순서. 앞에 놓인 범위가 픽셀을 먼저 가져가므로 좁은 부분(눈동자·신발)부터 두고, 배색하지 않은 부분은 원래 색으로 지킨다.
        /// 몸 텍스처에 바꿀 색이 없으면 비어 있다.
        /// </summary>
        public IReadOnlyList<NpcColorSwap> BodySwaps()
        {
            var swaps = new List<NpcColorSwap>();
            if (!RecolorsBody)
            {
                return swaps;
            }

            if (Look.EyesInBody)
            {
                Add(swaps, Look.Eyes, Eyes);
            }

            Add(swaps, Look.Shoes, Shoes);
            Add(swaps, Look.Hair, Hair);
            Add(swaps, Look.SecondClothes, SecondClothes);
            Add(swaps, Look.Clothes, Clothes);
            return swaps;
        }

        /// <summary>몸 색 텍스처에서 바꾼 머리카락 사이의 가는 틈(가닥 사이 선)도 메울 범위. 머리 배색이 없으면 비어 있다.</summary>
        public IReadOnlyList<NpcColorSwap> HairSwaps()
        {
            var swaps = new List<NpcColorSwap>();
            if (Hair.HasValue)
            {
                foreach (NpcColorRange range in Look.Hair)
                {
                    swaps.Add(range.To(Hair.Value));
                }
            }

            return swaps;
        }

        /// <summary>따로 둔 눈 메시의 눈 색 텍스처에 칠할 범위. 눈동자 배색이 없으면 비어 있다.</summary>
        public IReadOnlyList<NpcColorSwap> EyeSwaps()
        {
            var swaps = new List<NpcColorSwap>();
            if (!Look.EyesInBody && Eyes.HasValue)
            {
                Add(swaps, Look.Eyes, Eyes);
            }

            return swaps;
        }

        /// <summary>텍스처를 다시 써야 하는지 가리는 배색 기록</summary>
        public string Recipe(string textureName)
        {
            var recipe = new StringBuilder();
            recipe.Append("v").Append(RecipeVersion).Append(' ').Append(Look.Name);
            recipe.Append(' ').Append(textureName);
            foreach (NpcTint? tint in new[] { Hair, Clothes, SecondClothes, Shoes, Eyes })
            {
                recipe.Append(" | ");
                if (tint.HasValue)
                {
                    recipe.Append(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0:0.###},{1:0.###},{2:0.###}",
                            tint.Value.Hue,
                            tint.Value.Saturation,
                            tint.Value.ValueScale
                        )
                    );
                }
            }

            return recipe.ToString();
        }

        public override string ToString()
        {
            return $"{Code} ({Look.Name}: 머리 {HairColor}, 옷 {ClothesColor}, 신발 {ShoesColor}, 눈 {EyesColor})";
        }

        private static void Add(
            List<NpcColorSwap> swaps,
            IReadOnlyList<NpcColorRange> ranges,
            NpcTint? tint
        )
        {
            foreach (NpcColorRange range in ranges)
            {
                swaps.Add(tint.HasValue ? range.To(tint.Value) : range.Keep());
            }
        }
    }
}
