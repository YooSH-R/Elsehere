using System;

namespace A305.World
{
    public enum UiSound
    {
        Click,
        Menu,
        Coin,
        Quest,
        Saved,
        Spend,
        PrimaryClick,
    }

    /// <summary>화면 기능은 소리의 의미만 알리고 오디오 모듈이 재생·음량을 맡는다.</summary>
    public static class UiSoundEvents
    {
        public static event Action<UiSound> Requested;

        public static void Play(UiSound sound) => Requested?.Invoke(sound);
    }
}
