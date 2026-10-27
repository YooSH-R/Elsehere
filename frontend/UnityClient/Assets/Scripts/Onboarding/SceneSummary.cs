namespace A305.Onboarding
{
    /// <summary>WORLD-01 장소 한 곳. 입구에서는 재접속할 장소의 Unity 씬 이름(<see cref="UnityKey"/>)을 찾을 때 쓴다.</summary>
    public sealed class SceneSummary
    {
        public string SceneCode { get; set; }
        public string Name { get; set; }

        /// <summary>불러올 Unity 씬 이름.</summary>
        public string UnityKey { get; set; }

        public bool IsOpen { get; set; }
    }
}
