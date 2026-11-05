namespace A305.Npc
{
    /// <summary>NPC 머리 위 이름표에 쓸 내용. 직원이 아니면 <see cref="StaffRole"/>은 null이다.</summary>
    public readonly struct NpcNameTag
    {
        public NpcNameTag(string name, string staffRole)
        {
            Name = name;
            StaffRole = staffRole;
        }

        public string Name { get; }

        /// <summary>이름 위 칩에 쓰는 직원 역할(예: 벨보이). 행인이면 null</summary>
        public string StaffRole { get; }
    }

    /// <summary>
    /// NPC 머리 위 이름표의 내용과 보일지 정한다. 플레이어 가까이 있는 NPC만 이름을 보이고,
    /// 일터의 직원은 역할을 칩으로 함께 보여 행인과 한눈에 구분되게 한다. 근무 시간과 상관없이 직원은 직원으로 보인다.
    /// </summary>
    public static class NpcNameTags
    {
        /// <summary>플레이어에서 이 거리 안의 NPC만 이름표를 띄운다. 멀리 있는 NPC까지 띄우면 화면이 이름으로 덮인다.</summary>
        public const float ShowRadiusMeters = 8f;

        /// <param name="name">대본의 NPC 이름. 모르면 이름표를 띄우지 않는다.</param>
        /// <param name="roleLabel">대본의 역할 이름(예: 벨보이)</param>
        /// <param name="lifeRole">생활 데이터의 역할(STAFF·PASSERBY). 없으면 null</param>
        public static NpcNameTag? Describe(string name, string roleLabel, string lifeRole)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            bool showsRole = NpcDuty.IsStaff(lifeRole) && !string.IsNullOrEmpty(roleLabel);
            return new NpcNameTag(name, showsRole ? roleLabel : null);
        }

        /// <param name="isPlayerBusy">대화나 창 때문에 플레이어가 움직이지 못하는지. 그동안에는 이름표가 화면을 가리지 않게 숨긴다.</param>
        public static bool IsShown(float metersFromPlayer, bool isPlayerBusy)
        {
            return !isPlayerBusy && metersFromPlayer <= ShowRadiusMeters;
        }
    }
}
