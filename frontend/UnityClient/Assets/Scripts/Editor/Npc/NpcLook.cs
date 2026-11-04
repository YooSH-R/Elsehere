using System.Collections.Generic;

namespace A305.EditorTools.Npc
{
    /// <summary>
    /// NPC가 쓰는 리틀 멍 외형 하나와, 그 외형의 색 텍스처에서 머리·옷·신발·눈동자가 차지하는 색 범위.
    /// 같은 외형을 쓰는 NPC는 <see cref="NpcCast"/>의 배색으로 이 범위의 색만 바꿔 다른 사람처럼 보이게 한다.
    /// </summary>
    public sealed class NpcLook
    {
        public NpcLook(
            string name,
            string prefabPath,
            string bodyTexturePath,
            string onlyForCodePrefix = null,
            string baseMaterialPath = null
        )
        {
            Name = name;
            PrefabPath = prefabPath;
            BodyTexturePath = bodyTexturePath;
            OnlyForCodePrefix = onlyForCodePrefix;
            BaseMaterialPath = baseMaterialPath;
        }

        public string Name { get; }
        public string PrefabPath { get; }

        /// <summary>외형 프리팹의 몸 색 텍스처. 이 텍스처를 쓰는 재질만 NPC 색 사본으로 바꾼다.</summary>
        public string BodyTexturePath { get; }

        /// <summary>이 외형을 쓸 수 있는 NPC 키 앞부분(카페 유니폼은 "CAFE_"). 비우면 어느 NPC나 쓴다.</summary>
        public string OnlyForCodePrefix { get; }

        /// <summary>
        /// 색을 바꾸기 전의 몸 재질. 비우면 외형 프리팹의 재질이고, 경찰 제복처럼 외형 재질을 한 번 바꿔 입힌 외형은 그 재질이다.
        /// </summary>
        public string BaseMaterialPath { get; }

        public IReadOnlyList<NpcColorRange> Hair { get; private set; } = new NpcColorRange[0];

        /// <summary>윗옷처럼 가장 넓게 보이는 옷</summary>
        public IReadOnlyList<NpcColorRange> Clothes { get; private set; } = new NpcColorRange[0];

        /// <summary>재킷·조끼·셔츠처럼 함께 입은 다른 옷. 없는 외형도 있다.</summary>
        public IReadOnlyList<NpcColorRange> SecondClothes { get; private set; } =
            new NpcColorRange[0];

        public IReadOnlyList<NpcColorRange> Shoes { get; private set; } = new NpcColorRange[0];
        public IReadOnlyList<NpcColorRange> Eyes { get; private set; } = new NpcColorRange[0];

        /// <summary>
        /// 눈동자가 몸 색 텍스처에 그려져 있는지. false면 눈은 따로 둔 눈 메시의 눈 색 텍스처(*EyeColor)다.
        /// </summary>
        public bool EyesInBody { get; private set; }

        // 배색하지 않은 부분의 원래 색 이름. 같은 외형의 NPC끼리 같은 색인지 가릴 때 쓴다.
        public string OriginalHair { get; private set; }
        public string OriginalClothes { get; private set; }
        public string OriginalSecondClothes { get; private set; }
        public string OriginalShoes { get; private set; }
        public string OriginalEyes { get; private set; }

        public NpcLook Originally(
            string hair,
            string clothes,
            string shoes,
            string eyes,
            string secondClothes = "-"
        )
        {
            OriginalHair = hair;
            OriginalClothes = clothes;
            OriginalSecondClothes = secondClothes;
            OriginalShoes = shoes;
            OriginalEyes = eyes;
            return this;
        }

        public NpcLook WithHair(params NpcColorRange[] ranges)
        {
            Hair = ranges;
            return this;
        }

        public NpcLook WithClothes(params NpcColorRange[] ranges)
        {
            Clothes = ranges;
            return this;
        }

        public NpcLook WithSecondClothes(params NpcColorRange[] ranges)
        {
            SecondClothes = ranges;
            return this;
        }

        public NpcLook WithShoes(params NpcColorRange[] ranges)
        {
            Shoes = ranges;
            return this;
        }

        public NpcLook WithEyes(bool inBody, params NpcColorRange[] ranges)
        {
            EyesInBody = inBody;
            Eyes = ranges;
            return this;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
