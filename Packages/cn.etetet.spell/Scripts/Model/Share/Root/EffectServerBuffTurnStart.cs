namespace ET
{
    /// <summary>
    /// 服务端Buff回合开始效果节点
    /// </summary>
    [System.Serializable]
    public class EffectServerBuffTurnStart : EffectNode
    {
        [BTOutput(typeof(Buff))]
        public string Buff = "Buff";

        [BTOutput(typeof(Unit))]
        public string Unit = "Unit";

        [BTOutput(typeof(Unit))]
        public string Caster = "Caster";

        [BTOutput(typeof(int))]
        public string CurrentRound = "CurrentRound";
    }
}