using System;

namespace ET.Server
{
    [MessageHandler(SceneType.Map)]
    public class C2M_CombatActionHandler : MessageHandler<Unit, C2M_CombatAction>
    {
        protected override async ETTask Run(Unit unit, C2M_CombatAction message)
        {
            try
            {
                var combat = unit.GetComponent<TurnBasedCombatComponent>();
                if (combat == null || !combat.InCombat)
                {
                    Log.Warning($"[战斗] 玩家 {unit.Id} 未在战斗中");
                    return;
                }

                // 检查是否是玩家回合
                if (combat.CurrentActorId != unit.Id)
                {
                    Log.Warning($"[战斗] 不是玩家 {unit.Id} 的回合，当前回合: {combat.CurrentActorId}");
                    return;
                }

                // 通知等待的PlayerTurn方法
                var objectWait = unit.Root().GetComponent<ObjectWait>();
                if (objectWait != null)
                {
                    var action = new Wait_PlayerCombatAction
                    {
                        Error = WaitTypeError.Success,
                        ActionType = message.ActionType,
                        TargetId = message.TargetId
                    };

                    objectWait.Notify(action);
                }

                string actionTypeStr = message.ActionType == 1 ? "攻击" : message.ActionType == 2 ? "跳过" : "未知";
                Log.Debug($"[战斗] 玩家 {unit.Id} 执行动作: {actionTypeStr}，目标: {message.TargetId}");
            }
            catch (Exception e)
            {
                Log.Error($"Combat action error: {e}");
            }

            await ETTask.CompletedTask;
        }
    }
}