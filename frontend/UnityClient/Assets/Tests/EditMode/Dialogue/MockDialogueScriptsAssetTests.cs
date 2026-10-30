using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Dialogue.Tests
{
    /// <summary>씬에 둔 NPC가 기본 대본("Neighbor")으로 빠지지 않고 자기 이름과 역할로 소개되는지.</summary>
    public class MockDialogueScriptsAssetTests
    {
        private const string ScriptsPath = "Assets/Data/Dialogue/MockDialogueScripts.asset";
        private const string SeedRelativePath = "backend/src/main/resources/db/seed/world.sql";

        // 이름과 역할은 서버 시드(world.sql npcs)와 같아야 서버 대화가 시작돼도 바뀌지 않는다.
        [Test]
        public void NpcsInTheSeed_AreIntroducedByTheirSeedNameAndRole()
        {
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);
            List<SeedNpc> seedNpcs = ReadSeedNpcs();

            var mismatches = new List<string>();
            foreach (SeedNpc npc in seedNpcs)
            {
                if (!scripts.TryGetScript(npc.Code, out DialogueScript script))
                {
                    mismatches.Add($"{npc.Code}: 대본 없음");
                }
                else if (script.NpcName != npc.Name || script.RoleLabel != npc.RoleLabel)
                {
                    mismatches.Add(
                        $"{npc.Code}: 대본 {script.NpcName}/{script.RoleLabel}, 시드 {npc.Name}/{npc.RoleLabel}"
                    );
                }
            }

            Assert.That(seedNpcs, Is.Not.Empty);
            Assert.That(mismatches, Is.Empty);
        }

        [Test]
        public void TutorialPasserby_GainsItsRole()
        {
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            scripts.TryGetScript("TUTORIAL_PASSERBY", out DialogueScript script);

            Assert.That(script.RoleLabel, Is.EqualTo("이웃"));
        }

        private static List<SeedNpc> ReadSeedNpcs()
        {
            string seedPath = Path.Combine(Application.dataPath, "../../..", SeedRelativePath);
            string seed = File.ReadAllText(seedPath);
            string npcRows = Regex.Match(seed, @"(?s)INSERT INTO npcs .*?\) AS seed").Value;
            var npcs = new List<SeedNpc>();
            // 행 앞부분: (장소 코드, NPC 코드, 이름, NPC 종류, 역할 이름, ...
            foreach (
                Match row in Regex.Matches(
                    npcRows,
                    @"\('[A-Z_]+', '([A-Z_]+)', '([^']+)', '[A-Z_]+', '([^']+)',"
                )
            )
            {
                npcs.Add(
                    new SeedNpc(row.Groups[1].Value, row.Groups[2].Value, row.Groups[3].Value)
                );
            }

            return npcs;
        }

        private readonly struct SeedNpc
        {
            public SeedNpc(string code, string name, string roleLabel)
            {
                Code = code;
                Name = name;
                RoleLabel = roleLabel;
            }

            public string Code { get; }
            public string Name { get; }
            public string RoleLabel { get; }
        }
    }
}
