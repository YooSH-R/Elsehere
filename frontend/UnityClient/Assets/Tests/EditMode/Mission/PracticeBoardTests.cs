using NUnit.Framework;

namespace A305.Mission.Tests
{
    /// <summary>
    /// 퀘스트 창의 [장소 연습] 탭. 왼쪽 장소 줄은 끝낸 상황 수를 보이고 모두 끝내면 초록으로 바뀐다.
    /// 장소를 고르면 오른쪽만 바뀌며, 목록을 다시 받아도 고른 장소를 그대로 둔다.
    /// </summary>
    public sealed class PracticeBoardTests
    {
        [TestCase(0, 3, "0 / 3", false)]
        [TestCase(1, 3, "1 / 3", false)]
        [TestCase(3, 3, "3 / 3", true)]
        public void DoneCount_ShowsDoneOverTotal_AndIsGreenOnlyWhenEveryScenarioIsDone(
            int done,
            int total,
            string text,
            bool green
        )
        {
            var place = new PracticePlace { DoneCount = done, TotalCount = total };

            Assert.That(PracticeBoardText.DoneCount(place), Is.EqualTo(text));
            Assert.That(PracticeBoardText.IsAllDone(place), Is.EqualTo(green));
        }

        [TestCase("식당 직원", "Rosa · 식당 직원")]
        [TestCase("", "Rosa")]
        public void NpcChip_ShowsTheStaffNameAndRole(string role, string chip)
        {
            var place = new PracticePlace { NpcName = "Rosa", RoleLabel = role };

            Assert.That(PracticeBoardText.NpcChip(place), Is.EqualTo(chip));
        }

        [Test]
        public void FirstPlaceIsChosen_AndANewListKeepsThePlaceThatWasChosen()
        {
            var board = new PracticeBoard();

            board.Show(Places("HOTEL", "CAFE", "DINER"));
            Assert.That(board.Selected.PlaceCode, Is.EqualTo("HOTEL"));

            board.Select(2);
            board.Show(Places("HOTEL", "CAFE", "DINER"));
            Assert.That(board.SelectedIndex, Is.EqualTo(2));
            Assert.That(board.Selected.PlaceCode, Is.EqualTo("DINER"));

            board.Show(Places("HOTEL", "CAFE"));
            Assert.That(board.Selected.PlaceCode, Is.EqualTo("HOTEL"), "사라진 장소면 첫 장소");
        }

        [Test]
        public void Select_IgnoresPlacesOutsideTheList()
        {
            var board = new PracticeBoard();
            board.Show(Places("HOTEL", "CAFE"));
            board.Select(1);

            board.Select(5);
            board.Select(-1);

            Assert.That(board.Selected.PlaceCode, Is.EqualTo("CAFE"));
        }

        private static PracticeBoardState Places(params string[] codes)
        {
            var state = new PracticeBoardState();
            foreach (string code in codes)
                state.Places.Add(new PracticePlace { PlaceCode = code, Name = code });
            return state;
        }
    }
}
