namespace A305.Npc
{
    /// <summary>NPC 머리 위 이름표에 쓸 내용. 근무 중인 직원이 아니면 <see cref="StaffRole"/>은 null이다.</summary>
    public readonly struct NpcNameTag
    {
        public NpcNameTag(string name, string staffRole)
        {
            Name = name;
            StaffRole = staffRole;
        }

        public string Name { get; }

        /// <summary>이름 위 칩에 쓰는 직원 역할(예: 벨보이). 행인이나 쉬는 직원이면 null</summary>
        public string StaffRole { get; }
    }

    /// <summary>
    /// NPC 머리 위 이름표의 내용과 보일지 정한다. 플레이어 가까이 있는 NPC만 이름을 보이고,
    /// 근무 중인 직원은 역할을 칩으로 함께 보여 행인과 한눈에 구분되게 한다. 쉬는 시간이나 퇴근한 직원은 이름만 보인다.
    /// </summary>
    public static class NpcNameTags
    {
        /// <summary>플레이어에서 이 거리 안의 NPC만 이름표를 띄운다. 멀리 있는 NPC까지 띄우면 화면이 이름으로 덮인다.</summary>
        public const float ShowRadiusMeters = 8f;

        /// <summary>[이웃]에서 찾는 NPC 이름 위에 다는 코랄 표시 글자</summary>
        public const string TrackedLabel = "찾는 이웃";

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

        /// <summary>쉬는 시간이거나 퇴근한 직원은 역할 칩을 떼고 이름만 보인다. 근무 중인 직원과 행인은 그대로다.</summary>
        /// <param name="tag">근무 중일 때의 이름표(<see cref="Describe"/>)</param>
        /// <param name="isOffDuty">교대 밖이라 일하지 않는지(<see cref="NpcRoutine.IsOffDuty"/>). 첫날 미션이 찾아가라는 직원은 근무 중으로 본다.</param>
        public static NpcNameTag ForDuty(NpcNameTag tag, bool isOffDuty)
        {
            return isOffDuty ? new NpcNameTag(tag.Name, null) : tag;
        }

        /// <param name="isPlayerBusy">대화나 창 때문에 플레이어가 움직이지 못하는지. 그동안에는 이름표가 화면을 가리지 않게 숨긴다.</param>
        /// <param name="isTracked">[이웃]에서 찾는 NPC인지(<see cref="NpcTracking"/>). 멀리서도 알아보게 거리와 상관없이 띄운다.</param>
        public static bool IsShown(
            float metersFromPlayer,
            bool isPlayerBusy,
            bool isTracked = false
        )
        {
            return !isPlayerBusy && (isTracked || metersFromPlayer <= ShowRadiusMeters);
        }
    }
}
