namespace ET.Client
{
    [MessageHandler(SceneType.Client)]
    public class M2C_CombatActionResultHandler : MessageHandler<Scene, M2C_CombatActionResult>
    {
        protected override async ETTask Run(Scene scene, M2C_CombatActionResult message)
        {
            // 处理战斗动作结果
            Log.Debug($"战斗动作结果 - 攻击者ID: {message.AttackerId}, 目标ID: {message.TargetId}, 伤害: {message.Damage}, 剩余血量: {message.RemainHp}, 是否死亡: {message.IsDead}");

            // 更新单位血量显示或播放战斗动画
            await ETTask.CompletedTask;
        }
    }
}