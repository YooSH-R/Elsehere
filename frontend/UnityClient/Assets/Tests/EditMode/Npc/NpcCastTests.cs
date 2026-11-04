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
