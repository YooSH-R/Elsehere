namespace A305.Dialogue
{
    /// <summary>지금 대화 중인 NPC를 기억하고, 대화가 겹쳐 시작되지 않게 막는다.</summary>
    public sealed class DialogueSession
    {
        public bool IsActive => ActiveNpcCode != null;

        /// <summary>대화 중인 NPC 키(ERD npcs.code). 대화 중이 아니면 null.</summary>
        public string ActiveNpcCode { get; private set; }

        /// <returns>대화를 시작했으면 true. 이미 대화 중이거나 NPC 키가 없으면 false.</returns>
        public bool TryStart(string npcCode)
        {
            if (IsActive || string.IsNullOrEmpty(npcCode))
            {
                return false;
            }

            ActiveNpcCode = npcCode;
            return true;
        }

        /// <returns>대화를 끝낸 NPC 키. 대화 중이 아니었으면 null.</returns>
        public string End()
        {
            string endedNpcCode = ActiveNpcCode;
            ActiveNpcCode = null;
            return endedNpcCode;
        }
    }
}
