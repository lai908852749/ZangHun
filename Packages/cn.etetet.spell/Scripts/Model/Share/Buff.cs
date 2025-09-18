namespace ET
{
    [ChildOf(typeof(BuffComponent))]
    public class Buff: Entity, IAwake<int>, IDestroy
    {
        public int ConfigId { get; set; }
        public long Caster { get; set; }
        public long CreateTime { get; set; }
        public int TickTime { get; set; }
        public long ExpireTime { get; set; }
        public int Stack { get; set; }
        public long TimeoutTimer;

        // 回合制相关的运行时字段
        public int CreatedRound { get; set; }      // 创建时的回合数
        public int RemainTurn { get; set; }        // 剩余回合数
        public int LastTriggerRound { get; set; }  // 上次触发的回合
        public BuffDurationType DurationType { get; set; } = BuffDurationType.Time;
    }
}