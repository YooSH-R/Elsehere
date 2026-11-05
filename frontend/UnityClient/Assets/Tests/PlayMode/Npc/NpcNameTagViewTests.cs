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
    /// 마을에서 가까운 NPC 머리 위에 이름표가 뜬다. 근무 중인 직원은 역할 칩(예: 경찰관)을 함께 달고, 행인과 쉬는 직원은 이름만 보인다.
    /// 멀리 있는 NPC와 플레이어가 대화·창으로 움직이지 못하는 동안에는 이름표를 띄우지 않는다.
    /// 마을 시계는 08:00에 시작하므로 경찰관 Dan(06:00~10:00 근무, 10:00~10:30 쉬는 시간)은 근무 중이다.
    /// </summary>
    public class NpcNameTagViewTests
    {
        private const string StaffCode = "POLICE_OFFICER";
        private const string StaffName = "Dan";
        private const string StaffRole = "경찰관";
        private const string PasserbyCode = "TOWN_RETIREE";
        private const string PasserbyName = "Ruby";

        // 경찰관 Dan의 쉬는 시간(10:00~10:30) 한가운데
        private const double StaffBreakMinutes = 10 * 60 + 15;

        // 카메라가 플레이어를 따라와 자리 잡을 때까지 기다리는 프레임 수
        private const int SettleFrames = 10;

        [UnitySetUp]
        public IEnumerator LoadTownAfterTutorial()
        {
            NpcTestClock.SetDaytime();
            TutorialState.Clear();
            TutorialState.MarkFinishedOnServer();
            yield return SceneManager.LoadSceneAsync("TownScene", LoadSceneMode.Single);
            yield return null;
            yield return NpcTestClock.OpenPoliceStation();
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
            // 시계를 쉬는 시간으로 흘리고, Dan의 생활이 그 시각을 읽은 뒤에 세워 둔다.
            GameClock.Shared.Flow(
                (StaffBreakMinutes - GameClock.Shared.Minutes) / GameClock.GameMinutesPerRealSecond
            );
            yield return null;
            NpcIdentity staff = HoldStill(StaffCode);
            Assert.That(staff.GetComponent<NpcRoutine>().IsOffDuty, Is.True, "준비: Dan이 쉬는 중");
            yield return StandFacing(staff, 3f);

            TagView tag = ShownTags().SingleOrDefault(shown => shown.Name == StaffName);
            Assert.That(tag, Is.Not.Null, "쉬는 직원도 이름표는 보입니다.");
            Assert.That(tag.Role, Is.Null, "쉬는 직원에게는 역할 칩을 달지 않습니다.");
        }

        [UnityTest]
        public IEnumerator NearbyPasserby_ShowsNameOnly()
        {
            NpcIdentity passerby = HoldStill(PasserbyCode);
            yield return StandFacing(passerby, 3f);

            TagView tag = ShownTags().SingleOrDefault(shown => shown.Name == PasserbyName);
            Assert.That(tag, Is.Not.Null, "가까운 행인의 이름표가 보이지 않습니다.");
            Assert.That(tag.Role, Is.Null, "행인에게는 역할 칩을 달지 않습니다.");
        }

        [UnityTest]
        public IEnumerator FarNpc_HasNoTag()
        {
            NpcIdentity staff = HoldStill(StaffCode);
            yield return StandFacing(staff, NpcNameTags.ShowRadiusMeters + 6f);

            Assert.That(ShownTags().Select(shown => shown.Name), Does.Not.Contain(StaffName));
        }

        [UnityTest]
        public IEnumerator PlayerWhoCannotMove_SeesNoTags()
        {
            NpcIdentity staff = HoldStill(StaffCode);
            yield return StandFacing(staff, 3f);
            Assume.That(ShownTags().Select(shown => shown.Name), Does.Contain(StaffName));

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
