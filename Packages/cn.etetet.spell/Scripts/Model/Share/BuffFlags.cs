using Sirenix.OdinInspector;

namespace ET
{
    public enum BuffFlags
    {
        [LabelText("无")]
        None = 0,

        [LabelText("超时删除")]
        TimeoutRemove = 1,

        [LabelText("死亡删除")]
        DeadRemove = 2,

        [LabelText("进入战斗删除")]
        InBattleRemove = 3,

        [LabelText("离开战斗删除")]
        OutBattleRemove = 4,

        [LabelText("移动删除")]
        MoveRemove = 5,

        [LabelText("伤害删除")]
        DamageRemove = 6,

        [LabelText("当前技能删除的时候删除")]
        CurrentSpellRemoveRemove = 7,

        [LabelText("骑乘删除")]
        RideRemove = 8,

        [LabelText("堆叠为0删除")]
        StackRemove = 9,

        [LabelText("相同配置ID替换")]
        SameConfigIdReplaceRemove = 10,

        [LabelText("新技能打断")]
        NewSpellInterrupt = 11,

        [LabelText("无持续时间")]
        NoDurationRemove = 12,

        [LabelText("父Buff")]
        ParentRemoveRemove = 13,

        [LabelText("目标不存在")]
        NotFoundTargetRemove = 14,
        
        [LabelText("眩晕删除")]
        StunRemove = 15,
        
        [LabelText("AI删除")]
        AIRemove = 16,

        // 回合制相关的新标记
        [LabelText("回合数耗尽删除")]
        TurnExpiredRemove = 100,

        [LabelText("战斗结束删除")]
        CombatEndRemove = 101,

        [LabelText("达到回合上限删除")]
        RoundLimitRemove = 102,

        [LabelText("被免疫阻挡")]
        ImmunityBlocked = 103,

        [LabelText("被互斥替换")]
        MutexReplaced = 104,

        [LabelText("被高优先级覆盖")]
        PriorityOverridden = 105,

        [LabelText("互斥组替换删除")]
        MutexGroupReplaceRemove = 106,
    }
}