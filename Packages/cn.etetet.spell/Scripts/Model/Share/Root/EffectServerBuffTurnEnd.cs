namespace ET
{
    /// <summary>
    /// 服务端Buff回合结束效果节点
    /// </summary>
    [System.Serializable]
    public class EffectServerBuffTurnEnd : EffectNode
    {
        [BTOutput(typeof(Buff))]
        public string Buff = "Buff";

        [BTOutput(typeof(Unit))]
        public string Unit = "Unit";

        [BTOutput(typeof(Unit))]
        public string Caster = "Caster";

        [BTOutput(typeof(int))]
        public string CurrentRound = "CurrentRound";

        [BTOutput(typeof(int))]
        public string Damage = "Damage";  // 输出伤害值

        [BTOutput(typeof(int))]
        public string Heal = "Heal";      // 输出治疗值
    }
}