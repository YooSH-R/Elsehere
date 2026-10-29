namespace A305.Onboarding
{
    /// <summary>ONB-08 대화 난이도 단계(초급·중급·고급). 고를 수 없는 단계는 "추후 제공"으로 보인다.</summary>
    public sealed class ConversationLevel
    {
        public int Level { get; set; }
        public string Name { get; set; }
        public bool Available { get; set; }
        public string Description { get; set; }
    }
}
