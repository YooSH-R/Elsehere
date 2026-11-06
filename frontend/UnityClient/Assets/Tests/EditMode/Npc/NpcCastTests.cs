using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using A305.EditorTools.Npc;
using NUnit.Framework;
using UnityEngine;

namespace A305.Npc.Tests
{
    /// <summary>모든 NPC가 서로 다른 사람처럼 보이고, 유니폼은 그 일터의 직원만 입는지 외형 표(NpcCast)로 확인한다.</summary>
    public class NpcCastTests
    {
        private const string SeedRelativePath = "backend/src/main/resources/db/seed/world.sql";

        [TestCase("Barista")]
        [TestCase("HotelStaff")]
        public void SeparateBlinkEyes_ChangeIrisColorWithoutRecoloringTheBody(string lookName)
        {
            var look = NpcLooks.All.Single(item => item.Name == lookName);
            var member = new NpcCastMember(
                "EYE_CHECK",
                look,
                null,
                null,
                null,
                null,
                new NpcTint("blue", .6f, .6f)
            );
            Color32 iris = new Color32(40, 70, 35, 255);
            Color32 white = new Color32(240, 240, 240, 255);
            var result = NpcColorSwap.Recolor(new[] { iris, white }, member.EyeSwaps());
            Assert.That(
                result[0],
                Is.Not.EqualTo(iris),
                "별도 눈 메시의 홍채에 지정한 색을 적용합니다."
            );
            Assert.That(result[1], Is.EqualTo(white), "흰자는 보존합니다.");
            Assert.That(
                member.BodySwaps(),
                Is.Empty,
                "눈 색만 바꿀 때 몸 텍스처를 다시 칠하지 않습니다."
            );
        }

        [Test]
        public void PoliceHairRecolorIncludesTheNapeAndKeepsSkinAndUniform()
        {
            Color32 hair = new Color32(80, 46, 30, 255);
            Color32 skin = new Color32(232, 164, 115, 255);
            Color32 uniform = new Color32(24, 35, 60, 255);
            var result = NpcColorSwap.Recolor(
                new[] { hair, hair, skin, uniform },
                NpcCast.Find("POLICE_SERGEANT").BodySwaps(),
                new[] { .46f, .75f, .5f, .7f }
            );
            Assert.That(result[0], Is.Not.EqualTo(hair), "목덜미에도 같은 머리 배색을 적용합니다.");
            Assert.That(result[0], Is.EqualTo(result[1]));
            Assert.That(result[2], Is.EqualTo(skin));
            Assert.That(result[3], Is.EqualTo(uniform));
        }

        [Test]
        public void NpcsOnTheSameLook_WithTheSameEyes_DifferInHairAndClothes()
        {
            var lookAlikes = new List<string>();
            foreach ((NpcCastMember first, NpcCastMember second) in PairsOnTheSameLook())
            {
                bool sameEyes = first.EyesColor == second.EyesColor;
                if (
                    sameEyes
                    && (
                        first.HairColor == second.HairColor
                        || first.ClothesColor == second.ClothesColor
                    )
                )
                {
                    lookAlikes.Add($"{first} ↔ {second}");
                }
            }

            Assert.That(lookAlikes, Is.Empty);
        }

        [Test]
        public void NpcsOnTheSameLook_InTheSameClothes_DifferInEyesHairAndShoes()
        {
            var lookAlikes = new List<string>();
            foreach ((NpcCastMember first, NpcCastMember second) in PairsOnTheSameLook())
            {
                bool sameClothes = first.ClothesColor == second.ClothesColor;
                if (
                    sameClothes
                    && (
                        first.EyesColor == second.EyesColor
                        || first.HairColor == second.HairColor
                        || first.ShoesColor == second.ShoesColor
                    )
                )
                {
                    lookAlikes.Add($"{first} ↔ {second}");
                }
            }

            Assert.That(lookAlikes, Is.Empty);
        }

        [Test]
        public void EveryNpcInTheSeed_HasOneLook()
        {
            List<string> seedCodes = ReadSeedCodes();
            List<string> castCodes = NpcCast.Members.Select(member => member.Code).ToList();

            Assert.That(seedCodes, Is.Not.Empty);
            Assert.That(castCodes, Is.Unique);
            Assert.That(castCodes, Is.EquivalentTo(seedCodes));
        }

        [Test]
        public void Uniforms_AreWornOnlyByTheStaffOfThatWorkplace()
        {
            // 카페 유니폼은 카페, 호텔 유니폼은 호텔, 경찰 제복은 경찰서 직원만 입는다.
            List<string> outOfPlace = NpcCast
                .Members.Where(member =>
                    member.Look.OnlyForCodePrefix != null
                    && !member.Code.StartsWith(member.Look.OnlyForCodePrefix)
                )
                .Select(member => member.ToString())
                .ToList();

            Assert.That(outOfPlace, Is.Empty);
        }

        [Test]
        public void NoNpc_WearsThePlayerCharacter()
        {
            foreach (NpcLook look in NpcLooks.All)
            {
                Assert.That(
                    look.PrefabPath,
                    Does.Not.StartWith("Assets/Prefabs/Player/"),
                    look.Name
                );
                Assert.That(look.PrefabPath, Does.Not.Contain("Base_"), look.Name);
            }
        }

        private static IEnumerable<(NpcCastMember, NpcCastMember)> PairsOnTheSameLook()
        {
            IReadOnlyList<NpcCastMember> members = NpcCast.Members;
            for (int first = 0; first < members.Count; first++)
            {
                for (int second = first + 1; second < members.Count; second++)
                {
                    if (members[first].Look == members[second].Look)
                    {
                        yield return (members[first], members[second]);
                    }
                }
            }
        }

        private static List<string> ReadSeedCodes()
        {
            string seedPath = Path.Combine(Application.dataPath, "../../..", SeedRelativePath);
            string seed = File.ReadAllText(seedPath);
            string npcRows = Regex.Match(seed, @"(?s)INSERT INTO npcs .*?\) AS seed").Value;
            return Regex
                .Matches(npcRows, @"\((?:\d+, )?'[A-Z_]+', '([A-Z_]+)', '[^']+', '[A-Z_]+',")
                .Cast<Match>()
                .Select(row => row.Groups[1].Value)
                .ToList();
        }
    }
}
