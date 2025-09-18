namespace ET.Server
{
    /// <summary>
    /// 等待玩家战斗动作
    /// </summary>
    [EnableClass]
    public struct Wait_PlayerCombatAction : IWaitType
    {
        public int Error { get; set; }
        public int ActionType { get; set; } // 1=攻击 2=跳过
        public long TargetId { get; set; }
    }
}