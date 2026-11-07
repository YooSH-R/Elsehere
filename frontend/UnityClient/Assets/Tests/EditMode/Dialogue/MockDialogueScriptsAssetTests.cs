using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using A305.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace A305.Dialogue.Tests
{
    /// <summary>씬에 둔 NPC가 기본 대본("Neighbor")으로 빠지지 않고 자기 이름과 역할로 소개되는지.</summary>
    public class MockDialogueScriptsAssetTests
    {
        private const string ScriptsPath = "Assets/Data/Dialogue/MockDialogueScripts.asset";

        [Test]
        public void TutorialBen_GreetsThenBuildsAffinityAndFinishesWithinThreeTurns()
        {
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);
            scripts.TryGetScript("TUTORIAL_PASSERBY", out DialogueScript script);
            Assert.That(script.OpeningLine.English, Does.Contain("I'm Ben"));
            Assert.That(script.GuidedTurns, Has.Count.EqualTo(3));
            var conversation = new DialogueConversation("TOWN_PASSERBY", script);
            Assert.That(conversation.Standing, Is.Not.Null);
            Assert.That(conversation.Standing.Affinity, Is.Zero);
            Assert.That(conversation.SuggestedEnglish, Does.Contain("Nice to meet you"));
            for (int turn = 1; turn <= 3; turn++)
            {
                Assert.That(conversation.Send(conversation.SuggestedEnglish), Is.True);
                Assert.That(conversation.Standing.Affinity, Is.EqualTo(turn * 2));
            }
            Assert.That(conversation.IsGuidedCompleted, Is.True);
            Assert.That(conversation.ToRecord().UserUtterances, Has.Count.EqualTo(3));
            Assert.That(conversation.Send("One more thing"), Is.False);
        }

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

        // 튜토리얼 Ben 길 안내는 서버 대화가 아닌 정해진 대본이라 서버가 만드는 NPC 음성을 받지 않는다.
        // 그래서 첫 말과 대답마다 Ben 목소리로 미리 합성한 음성을 싣고, 입 모양 타이밍까지 읽혀야 한다.
        [Test]
        public void TutorialBen_EveryLineCarriesBensRecordedVoiceWithLipSync()
        {
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);
            scripts.TryGetScript("TUTORIAL_PASSERBY", out DialogueScript script);

            var lines = new List<DialogueLine> { script.OpeningLine };
            lines.AddRange(script.GuidedTurns.Select(turn => turn.Reply));

            foreach (DialogueLine line in lines)
            {
                Assert.That(line.Speech, Is.Not.Null, $"음성이 없습니다: {line.English}");
                DialogueSpeechWav wav = DialogueSpeechWav.Read(line.Speech);
                Assert.That(wav.TimingError, Is.Null, $"입 모양 타이밍이 없습니다: {line.English}");
            }
        }

        [Test]
        public void ShopStaff_WelcomesVisitorsToTheDrugstore()
        {
            // 미국에서 비 올 때 우산을 사러 들르는 드럭스토어로 상점을 정했다. 간판·지도와 같은 이름으로 맞이한다.
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            scripts.TryGetScript("SHOP_STAFF", out DialogueScript script);

            Assert.That(script.OpeningLine.English, Does.Contain("drugstore"));
            Assert.That(script.OpeningLine.Korean, Does.Contain("드럭스토어"));
        }

        [Test]
        public void TaxiDriver_AsksWhereToGo()
        {
            // 마을 본길에서 택시를 세우면 기사에게 가고 싶은 곳을 영어로 말해 본다.
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            bool found = scripts.TryGetScript("TAXI_DRIVER", out DialogueScript script);

            Assert.That(found, Is.True);
            Assert.That(script.RoleLabel, Is.EqualTo("택시 기사"));
            Assert.That(script.OpeningLine.English, Does.Contain("Where"));
        }

        [Test]
        public void PoliceOfficer_AsksWhatHappened()
        {
            // 경찰서 창구에서 경찰관에게 무슨 일이 있었는지 영어로 신고해 본다.
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            bool found = scripts.TryGetScript("POLICE_OFFICER", out DialogueScript script);

            Assert.That(found, Is.True);
            Assert.That(script.RoleLabel, Is.EqualTo("경찰관"));
            Assert.That(script.OpeningLine.English, Does.Contain("happened"));
        }

        [Test]
        public void TutorialScript_IsSpokenByBenTheTownPasserby()
        {
            // 집 앞에서 길을 묻는 튜토리얼 대본은 마을 행인 Ben이 말한다(같은 사람).
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            scripts.TryGetScript("TUTORIAL_PASSERBY", out DialogueScript tutorial);
            scripts.TryGetScript("TOWN_PASSERBY", out DialogueScript town);

            Assert.That(tutorial.NpcName, Is.EqualTo("Ben"));
            Assert.That(tutorial.NpcName, Is.EqualTo(town.NpcName));
            Assert.That(tutorial.GuidedTurns, Has.Count.EqualTo(3), "대본 흐름은 그대로입니다.");
        }

        [Test]
        public void TutorialScript_SuggestsWordsForEveryLine()
        {
            // 대화 화면 왼쪽 [단어]에 지금 할 대사에 쓸 만한 단어를 보이고, 튜토리얼이 이를 설명한다.
            var scripts = AssetDatabase.LoadAssetAtPath<MockDialogueScriptSource>(ScriptsPath);

            scripts.TryGetScript("TUTORIAL_PASSERBY", out DialogueScript tutorial);

            foreach (GuidedTurn turn in tutorial.GuidedTurns)
            {
                Assert.That(turn.SuggestWords, Is.Not.Empty, turn.SuggestedEnglish);
            }
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
            // 행 앞부분: (목록 순서, 장소 코드, NPC 코드, 이름, NPC 종류, 역할 이름, ... 목록 순서는 없을 수도 있다.
            foreach (
                Match row in Regex.Matches(
                    npcRows,
                    @"\((?:\d+, )?'[A-Z_]+', '([A-Z_]+)', '([^']+)', '[A-Z_]+', '([^']+)',"
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
