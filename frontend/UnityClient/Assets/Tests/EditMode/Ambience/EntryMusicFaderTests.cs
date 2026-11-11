using NUnit.Framework;

namespace A305.Ambience.Tests
{
    public sealed class EntryMusicFaderTests
    {
        private const float EntryVolume = .18f;

        private static void Run(EntryMusicFader fader, string scene, bool enabled, float seconds)
        {
            for (float elapsed = 0; elapsed < seconds - .0001f; elapsed += .05f)
                fader.Update(scene, enabled, .05f);
        }

        // 로딩 페이지가 틀던 음악을 입구 화면에서 줄이거나 멈추지 않고 그대로 잇는다(S15P31A305-786).
        [Test]
        public void LoadingPageMusic_KeepsPlayingOnTheEntryScreenWithoutAnotherClick()
        {
            var fader = new EntryMusicFader();
            fader.ContinueLoadingPageMusic(EntryVolume);

            Run(fader, "EntryScene", enabled: true, seconds: 1);

            Assert.That(fader.Playing, Is.True);
            Assert.That(fader.Volume, Is.EqualTo(EntryVolume).Within(.0001f));
        }

        // 로그인돼 있어 입구 화면을 지나 바로 마을에 들어가도 기존 규칙대로 약 0.5초에 걸쳐 끈다.
        [Test]
        public void LoadingPageMusic_FadesOutOverHalfASecondInTheTown()
        {
            var fader = new EntryMusicFader();
            fader.ContinueLoadingPageMusic(EntryVolume);

            Run(fader, "TownScene", enabled: true, seconds: .25f);
            Assert.That(fader.Playing, Is.True);
            Assert.That(fader.Volume, Is.EqualTo(EntryVolume / 2).Within(.001f));

            Run(fader, "TownScene", enabled: true, seconds: .25f);
            Assert.That(fader.Playing, Is.False);
            Assert.That(fader.Volume, Is.EqualTo(0).Within(.0001f));
        }

        // 스피커를 꺼 두어 로딩 페이지가 틀지 않았으면 입구 화면에서도 조용하고, 스피커를 켜면 클릭을 더 기다리지 않고 튼다.
        [Test]
        public void SilentLoadingPage_StaysSilentUntilTheSpeakerIsTurnedOn()
        {
            var fader = new EntryMusicFader();
            fader.ContinueLoadingPageMusic(0);

            Run(fader, "EntryScene", enabled: false, seconds: 1);
            Assert.That(fader.Playing, Is.False);
            Assert.That(fader.Volume, Is.Zero);

            Run(fader, "EntryScene", enabled: true, seconds: 1);
            Assert.That(fader.Playing, Is.True);
            Assert.That(fader.Volume, Is.EqualTo(EntryVolume).Within(.0001f));
        }

        // 로딩 페이지 음악이 없으면(Editor 등) 지금처럼 첫 클릭·키·터치 뒤에 작게 시작해 키운다.
        [Test]
        public void WithoutLoadingPageMusic_StartsAfterTheFirstClick()
        {
            var fader = new EntryMusicFader();

            Run(fader, "EntryScene", enabled: true, seconds: 1);
            Assert.That(fader.Playing, Is.False);
            Assert.That(fader.Volume, Is.Zero);

            fader.AllowSound();
            Run(fader, "EntryScene", enabled: true, seconds: .25f);
            Assert.That(fader.Playing, Is.True);
            Assert.That(fader.Volume, Is.EqualTo(EntryVolume / 2).Within(.001f));
        }
    }
}
