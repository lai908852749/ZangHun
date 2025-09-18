namespace ET.Client
{
    [MessageHandler(SceneType.Client)]
    public class M2C_CombatTurnStartHandler : MessageHandler<Scene, M2C_CombatTurnStart>
    {
        protected override async ETTask Run(Scene scene, M2C_CombatTurnStart message)
        {
            // 处理战斗回合开始
            Log.Debug($"战斗回合开始 - 行动者ID: {message.ActorId}, 是否玩家回合: {message.IsPlayerTurn}, 可攻击目标数: {message.ValidTargets.Count}");

            // 通知客户端UI或其他系统
            await ETTask.CompletedTask;
        }
    }
}