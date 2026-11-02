using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace A305.ObjectHover.Tests
{
    /// <summary>
    /// Editor에서 서버에 연결하지 못했을 때 쓰는 확인용 단어 카드. 장소의 Unity 단어로 카드를 만들어 [저장]·[알아요]를 눌러 볼 수 있고,
    /// 같은 Play 동안은 장소를 옮겨도 저장·숨김을 기억한다.
    /// </summary>
    public class LocalObjectWordApiTests
    {
        private static readonly ObjectWordDetails[] SceneWords =
        {
            Word("sofa", "sofa", "소파"),
            Word("tv", "TV", "텔레비전"),
        };

        [SetUp]
        [TearDown]
        public void ForgetSavedWords()
        {
            LocalObjectWordApi.Forget();
        }

        [Test]
        public async Task Load_MakesAnUnsavedCardForEachSceneWord()
        {
            ObjectWordCards cards = await LoadAsync();

            Assert.That(cards.IsLoaded, Is.True);
            Assert.That(cards.TryGet("sofa", out WordCard sofa), Is.True);
            Assert.That(sofa.Saved, Is.False);
            Assert.That(sofa.HoverHidden, Is.False);
            Assert.That(sofa.HasPronunciation, Is.False, "확인용 카드에는 발음 음성이 없습니다.");
            Assert.That(cards.TryGet("tv", out _), Is.True);
        }

        [Test]
        public async Task Save_MarksTheCard_AndIsRememberedInTheNextScene()
        {
            ObjectWordCards cards = await LoadAsync();

            await cards.SaveAsync("sofa");

            Assert.That(cards.TryGet("sofa", out WordCard sofa) && sofa.Saved, Is.True);
            ObjectWordCards nextScene = await LoadAsync();
            Assert.That(nextScene.TryGet("sofa", out WordCard again) && again.Saved, Is.True);
            Assert.That(nextScene.TryGet("tv", out WordCard tv) && tv.Saved, Is.False);
        }

        [Test]
        public async Task HidingFromHover_IsRememberedUntilShownAgain()
        {
            ObjectWordCards cards = await LoadAsync();

            await cards.SetHoverHiddenAsync("tv", true);
            Assert.That((await LoadAsync()).CanHover("tv"), Is.False);

            await cards.SetHoverHiddenAsync("tv", false);
            Assert.That((await LoadAsync()).CanHover("tv"), Is.True);
        }

        [Test]
        public async Task Forget_StartsTheNextPlayWithNothingSaved()
        {
            ObjectWordCards cards = await LoadAsync();
            await cards.SaveAsync("sofa");

            LocalObjectWordApi.Forget();

            Assert.That((await LoadAsync()).TryGet("sofa", out WordCard sofa) && sofa.Saved, Is.False);
        }

        private static async Task<ObjectWordCards> LoadAsync()
        {
            var cards = new ObjectWordCards(new LocalObjectWordApi(SceneWords));
            await cards.LoadAsync("HOME");
            return cards;
        }

        private static ObjectWordDetails Word(string key, string text, string meaningKo)
        {
            return JsonUtility.FromJson<ObjectWordDetails>(
                $"{{\"key\":\"{key}\",\"text\":\"{text}\",\"meaningKo\":\"{meaningKo}\"}}"
            );
        }
    }
}
