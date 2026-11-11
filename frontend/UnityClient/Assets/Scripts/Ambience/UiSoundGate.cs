using System;
using A305.World;

namespace A305.Ambience
{
    /// <summary>연속 클릭·중복 보상과 완료 팡파레의 소리가 겹치지 않게 한다.</summary>
    public sealed class UiSoundGate
    {
        private readonly double[] next = new double[6];
        private double coinQuietUntil;

        public bool TryPlay(UiSound sound, bool enabled, double now)
        {
            int index = (int)sound;
            if (!enabled || now < next[index] || (sound == UiSound.Coin && now < coinQuietUntil))
                return false;
            next[index] =
                now
                + (
                    sound == UiSound.Click ? .08
                    : sound == UiSound.Menu ? .12
                    : sound == UiSound.Spend ? .35
                    : .8
                );
            if (sound == UiSound.Quest)
                coinQuietUntil = now + .8;
            return true;
        }
    }

    public static class EntryMusicPlan
    {
        public static bool IsEntryScene(string scene) =>
            string.Equals(scene, "EntryScene", StringComparison.Ordinal);

        // 로딩 페이지(WebGLTemplates/A305Responsive/index.html)도 같은 음량 0.18로 튼다.
        public static float Volume(string scene, bool userGesture, bool enabled) =>
            IsEntryScene(scene) && userGesture && enabled ? .18f : 0f;
    }
}
