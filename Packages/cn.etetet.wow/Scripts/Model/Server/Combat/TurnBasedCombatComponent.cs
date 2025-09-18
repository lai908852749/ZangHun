using System.Collections.Generic;

namespace ET.Server
{
    /// <summary>
    /// 回合制战斗主组件
    /// </summary>
    [ComponentOf(typeof(Unit))]
    public class TurnBasedCombatComponent : Entity, IAwake, IDestroy
    {
        /// <summary>
        /// 是否在战斗中
        /// </summary>
        public bool InCombat;

        /// <summary>
        /// 当前回合数
        /// </summary>
        public int RoundNumber;

        /// <summary>
        /// 行动顺序列表（按速度排序）
        /// </summary>
        public List<long> TurnOrder = new();

        /// <summary>
        /// 当前行动者在TurnOrder中的索引
        /// </summary>
        public int CurrentTurnIndex;

        /// <summary>
        /// 玩家单位ID
        /// </summary>
        public long PlayerUnitId;

        /// <summary>
        /// 怪物单位ID列表
        /// </summary>
        public List<long> MonsterUnitIds = new();

        /// <summary>
        /// 当前行动者单位ID
        /// </summary>
        public long CurrentActorId;

        /// <summary>
        /// 战斗开始时间
        /// </summary>
        public long StartTime;
    }
}