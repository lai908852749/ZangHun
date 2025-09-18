namespace ET
{
    /// <summary>
    /// 免疫状态结构
    /// </summary>
    public struct ImmunityStatus
    {
        public bool ImmuneToAllDamage { get; set; }
        public bool ImmuneToPhysical { get; set; }
        public bool ImmuneToMagic { get; set; }
        public bool ImmuneToControl { get; set; }
        public bool ImmuneToDebuff { get; set; }

        public bool IsImmuneToCategory(BuffCategory category)
        {
            return category switch
            {
                BuffCategory.Damage => ImmuneToAllDamage,
                BuffCategory.Control => ImmuneToControl,
                BuffCategory.Debuff => ImmuneToDebuff,
                _ => false
            };
        }
    }
}