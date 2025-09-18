namespace ET.Server
{
    [EntitySystemOf(typeof(CombatRecordComponent))]
    public static partial class CombatRecordComponentSystem
    {
        [EntitySystem]
        private static void Awake(this CombatRecordComponent self)
        {
            self.TotalRounds = 0;
            self.Actions.Clear();
            Log.Debug($"[战斗记录] 战斗记录组件初始化");
        }

        /// <summary>
        /// 获取战报数据
        /// </summary>
        public static string GetCombatReport(this CombatRecordComponent self)
        {
            Log.Debug($"[战斗记录] 生成战报 - 总回合数: {self.TotalRounds}, 总动作数: {self.Actions.Count}");
            return $"总回合数: {self.TotalRounds}, 总动作数: {self.Actions.Count}";
        }
    }
}