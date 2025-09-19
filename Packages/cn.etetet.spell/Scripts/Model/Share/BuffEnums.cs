namespace ET
{
    /// <summary>
    /// Buff互斥组枚举
    /// </summary>
    public enum BuffMutexGroup
    {
        None = 0,           // 无互斥
        Burn = 1,           // 燃烧类
        Freeze = 2,         // 冰冻类
        Poison = 3,         // 中毒类
        Bleed = 4,          // 流血类
        Shield = 5,         // 护盾类
        DivineShield = 6,   // 神圣护盾
        Stealth = 7,        // 隐身类
        Taunt = 8,          // 嘲讽类
        Charm = 9,          // 魅惑类
        Fear = 10,          // 恐惧类
        Slow = 11,          // 减速类
        Haste = 12,         // 加速类
        Immunity = 13,      // 免疫类
        Barrier = 14,       // 屏障类
        Regeneration = 15,  // 回复类
        Weakness = 16,      // 虚弱类
        Strengthen = 17,    // 强化类
        Curse = 18,         // 诅咒类
        Blessing = 19,      // 祝福类
        Transform = 20,     // 变形类
    }

    /// <summary>
    /// Buff标签枚举（支持多标签组合）
    /// </summary>
    [System.Flags]
    public enum BuffTag
    {
        None = 0,

        // 伤害类型标签
        Physical = 1 << 0,         // 物理
        Magic = 1 << 1,            // 魔法
        Pure = 1 << 2,             // 真实伤害

        // 元素类型标签
        Fire = 1 << 3,             // 火焰
        Ice = 1 << 4,              // 冰霜
        Lightning = 1 << 5,        // 闪电
        Poison = 1 << 6,           // 毒素
        Holy = 1 << 7,             // 神圣
        Dark = 1 << 8,             // 暗影
        Nature = 1 << 9,           // 自然
        Arcane = 1 << 10,          // 奥术

        // 效果类型标签
        Control = 1 << 11,         // 控制效果
        DoT = 1 << 12,             // 持续伤害
        HoT = 1 << 13,             // 持续治疗
        Dispellable = 1 << 14,     // 可驱散
        Stackable = 1 << 15,       // 可叠加
        Unique = 1 << 16,          // 唯一

        // 免疫类型标签
        AllDamageImmunity = 1 << 17,     // 所有伤害免疫
        PhysicalDamageImmunity = 1 << 18, // 物理伤害免疫
        MagicDamageImmunity = 1 << 19,    // 魔法伤害免疫
        ControlImmunity = 1 << 20,        // 控制免疫
        DebuffImmunity = 1 << 21,         // 减益免疫

        // 特殊标签
        Divine = 1 << 22,          // 神圣（特殊护盾）
        Debuff = 1 << 23,          // 减益效果
        Buff = 1 << 24,            // 增益效果
        Hidden = 1 << 25,          // 隐藏（不显示图标）
        Passive = 1 << 26,         // 被动
        Aura = 1 << 27,            // 光环
        Channeling = 1 << 28,      // 引导
        Shield = 1 << 29,          // 护盾类
        Absorb = 1 << 30,          // 吸收类
    }

    /// <summary>
    /// Buff持续类型
    /// </summary>
    public enum BuffDurationType
    {
        Auto = -1,     // 自动选择（根据战斗状态）
        Time = 0,      // 时间制（默认）
        Turn = 1,      // 回合制
        Permanent = 2, // 永久
        Hybrid = 3,    // 混合模式（同时支持时间和回合）
        CombatOnly = 4 // 仅战斗期间（战斗开始时激活，战斗结束时移除）
    }

    /// <summary>
    /// Buff类别（用于执行优先级）
    /// </summary>
    public enum BuffCategory
    {
        Immunity = 0,    // 免疫类（最高优先级）
        Shield = 1,      // 护盾类
        Defense = 2,     // 防御类
        Control = 3,     // 控制类
        Damage = 4,      // 伤害类
        Heal = 5,        // 治疗类
        Enhance = 6,     // 增益类
        Debuff = 7,      // 减益类
        Other = 99       // 其他
    }
}