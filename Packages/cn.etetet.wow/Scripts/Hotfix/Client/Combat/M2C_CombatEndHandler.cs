namespace ET.Client
{
    [MessageHandler(SceneType.Client)]
    public class M2C_CombatEndHandler : MessageHandler<Scene, M2C_CombatEnd>
    {
        protected override async ETTask Run(Scene scene, M2C_CombatEnd message)
        {
            // 处理战斗结束
            Log.Debug($"战斗结束 - 是否胜利: {message.IsWin}, 总回合数: {message.TotalRounds}, 持续时间: {message.Duration}毫秒");

            // 清理战斗UI，显示结算界面等
            await ETTask.CompletedTask;
        }
    }
}