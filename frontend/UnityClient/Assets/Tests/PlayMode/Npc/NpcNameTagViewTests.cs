using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using A305.Player;
using A305.Tutorial;
using A305.World;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace A305.Npc.PlayModeTests
{
    /// <summary>
    /// 가까운 NPC 머리 위에 이름표가 뜬다. 근무 중인 직원은 역할 칩(예: 경찰서의 경찰관)을 함께 달고, 마을 행인과 쉬는 직원은 이름만 보인다.
    /// 멀리 있는 NPC와 플레이어가 대화·창으로 움직이지 못하는 동안에는 이름표를 띄우지 않는다.
    /// 기본 시각은 12:15 근무 중으로 고정하고, 휴식 검사는 10:15에 나타나는 출퇴근 캐릭터를 확인한다.
    /// </summary>
    public class NpcNameTagViewTests
    {
        private const string StaffCode = "POLICE_OFFICER";
        private const string StaffName = "Dan";
        private const string StaffRole = "경찰관";
        private const string PasserbyCode = "TOWN_RETIREE";
        private const string PasserbyName = "Ruby";

        // 경찰관 Dan의 쉬는 시간(10:00~10:30) 한가운데
        private const int StaffBreakMinutes = 10 * 60 + 15;

        // 카메라가 플레이어를 따라와 자리 잡을 때까지 기다리는 프레임 수
        private const int SettleFrames = 10;

        [SetUp]
        public void StartAfterTutorial()
        {
            NpcTestClock.SetDaytime();
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
        }

        [UnityTearDown]
        public IEnumerator ClearTutorialAndClock()
        {
            TutorialState.Clear();
            // 마을의 시계가 서버 없이 보낸 신호는 응답을 기다린 채로 남아, 바로 뒤 시험이 시각을 맞추지 못한다.
            // 마을이 아직 열려 있으므로 신호를 멈춘 뒤, Play를 새로 시작할 때처럼 시계를 비운다(World 시험의 TestGameClock과 같은 방법).
            foreach (
                GameClockDriver driver in Object.FindObjectsByType<GameClockDriver>(
                    FindObjectsSortMode.None
                )
            )
                driver.enabled = false;
            typeof(GameClock)
                .GetMethod("ResetShared", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NearbyStaff_ShowsNameWithRoleChipAboveTheHead()
        {
            // 경찰관은 경찰서 장면에서 일한다.
            yield return SceneManager.LoadSceneAsync("PoliceStationScene", LoadSceneMode.Single);
            yield return null;
            NpcIdentity staff = HoldStill(StaffCode);
            yield return StandFacing(staff, 3f);

            TagView tag = ShownTags().SingleOrDefault(shown => shown.Name == StaffName);
            Assert.That(tag, Is.Not.Null, "가까운 직원의 이름표가 보이지 않습니다.");
            Assert.That(tag.Role, Is.EqualTo(StaffRole));
            float headY = Camera.main.WorldToScreenPoint(staff.LookTarget.position).y;
            Assert.That(tag.Rect.position.y, Is.GreaterThan(headY), "이름표는 머리 위에 뜹니다.");
        }

        [UnityTest]
        public IEnumerator NearbyStaffOnBreak_ShowsNameOnly()
        {
            // 고정 시계에 휴식 시각을 주고 근무지 직원이 출퇴근 캐릭터로 전환될 때까지 기다린다.
            NpcTestClock.SetTime(StaffBreakMinutes);
            yield return null;
            yield return null;
            NpcIdentity staff = HoldStill(StaffCode);
            Assert.That(GameClock.Shared.Minutes, Is.EqualTo(StaffBreakMinutes));
            var commuter = staff.GetComponent<NpcTownWalker>();
            Assert.That(commuter, Is.Not.Null, "준비: 쉬는 Dan은 근무지 대신 거리에서 지냅니다.");
            Assert.That(commuter.IsStaff, Is.True);
            yield return StandFacing(staff, 3f);

            TagView tag = ShownTags().SingleOrDefault(shown => shown.Name == StaffName);
            Assert.That(tag, Is.Not.Null, "쉬는 직원도 이름표는 보입니다.");
            Assert.That(tag.Role, Is.Null, "쉬는 직원에게는 역할 칩을 달지 않습니다.");
        }

        [UnityTest]
        public IEnumerator NearbyPasserby_ShowsNameOnly()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            NpcIdentity passerby = HoldStill(PasserbyCode);
            yield return StandFacing(passerby, 3f);

            TagView tag = ShownTags().SingleOrDefault(shown => shown.Name == PasserbyName);
            Assert.That(tag, Is.Not.Null, "가까운 행인의 이름표가 보이지 않습니다.");
            Assert.That(tag.Role, Is.Null, "행인에게는 역할 칩을 달지 않습니다.");
        }

        [UnityTest]
        public IEnumerator FarNpc_HasNoTag()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            NpcIdentity passerby = HoldStill(PasserbyCode);
            yield return StandFacing(passerby, NpcNameTags.ShowRadiusMeters + 6f);

            Assert.That(ShownTags().Select(shown => shown.Name), Does.Not.Contain(PasserbyName));
        }

        [UnityTest]
        public IEnumerator PlayerWhoCannotMove_SeesNoTags()
        {
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            NpcIdentity passerby = HoldStill(PasserbyCode);
            yield return StandFacing(passerby, 3f);
            Assume.That(ShownTags().Select(shown => shown.Name), Does.Contain(PasserbyName));

            Object.FindFirstObjectByType<PlayerMover>().SetMovementLocked(true);
            yield return null;

            Assert.That(ShownTags(), Is.Empty);
        }

        // 다가와 말을 걸거나 일하러 걸어가지 않게 세워 둔다. 이름표는 NPC 키(NpcIdentity)로 그대로 찾는다.
        private static NpcIdentity HoldStill(string code)
        {
            NpcIdentity npc = Object
                .FindObjectsByType<NpcIdentity>(FindObjectsSortMode.None)
                .Single(candidate => candidate.Code == code);
            if (npc.TryGetComponent(out NpcRoutine routine))
                routine.enabled = false;
            if (npc.TryGetComponent(out Passerby passerby))
                passerby.enabled = false;
            return npc;
        }

        private static IEnumerator StandFacing(NpcIdentity npc, float distanceMeters)
        {
            Vector3 forward = npc.transform.forward;
            forward.y = 0f;
            Vector3 position = npc.transform.position + forward.normalized * distanceMeters;
            Object
                .FindFirstObjectByType<PlayerMover>()
                .Teleport(position, Quaternion.LookRotation(-forward.normalized));
            for (int i = 0; i < SettleFrames; i++)
                yield return null;
        }

        private static List<TagView> ShownTags()
        {
            NpcNameTagView view = Object.FindFirstObjectByType<NpcNameTagView>();
            Assert.That(view, Is.Not.Null, "장면에 이름표 층이 없습니다.");
            return view.GetComponentsInChildren<TMP_Text>()
                .Where(text => text.name == "Name")
                .Select(text => new TagView((RectTransform)text.transform.parent))
                .ToList();
        }

        private sealed class TagView
        {
            public TagView(RectTransform rect)
            {
                Rect = rect;
                Name = rect.Find("Name").GetComponent<TMP_Text>().text;
                Transform chip = rect.Find("RoleChip");
                Role =
                    chip != null && chip.gameObject.activeSelf
                        ? chip.Find("Label").GetComponent<TMP_Text>().text
                        : null;
            }

            public RectTransform Rect { get; }

            public string Name { get; }

            public string Role { get; }
        }
    }
}
