using System.Collections.Generic;

namespace ET.Server
{
    /// <summary>
    /// 战斗记录组件
    /// </summary>
    [ComponentOf(typeof(TurnBasedCombatComponent))]
    public class CombatRecordComponent : Entity, IAwake
    {
        /// <summary>
        /// 总回合数
        /// </summary>
        public int TotalRounds;

        /// <summary>
        /// 战斗动作记录列表
        /// </summary>
        public List<CombatAction> Actions = new();
    }

    /// <summary>
    /// 战斗动作记录
    /// </summary>
    public struct CombatAction
    {
        public long AttackerId;
        public long TargetId;
        public int Damage;
        public long Timestamp;
        public int ActionType; // 1=攻击 2=跳过
    }
}