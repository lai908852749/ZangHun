using System;
using System.Collections.Generic;

namespace ET.Server
{
    [MessageHandler(SceneType.Map)]
    public class C2M_StartCombatHandler : MessageLocationHandler<Unit, C2M_StartCombat, M2C_StartCombat>
    {
        protected override async ETTask Run(Unit unit, C2M_StartCombat request, M2C_StartCombat response)
        {
            try
            {
                // 获取怪物单位
                var monsters = new List<Unit>();
                var unitComponent = unit.Root().GetComponent<UnitComponent>();

                Log.Debug($"[战斗] 玩家 {unit.Id} 请求开始战斗，怪物数量: {request.MonsterIds.Count}");

                foreach (long monsterId in request.MonsterIds)
                {
                    Unit monster = unitComponent.Get(monsterId);
                    if (monster != null)
                    {
                        monsters.Add(monster);
                        var monsterNum = monster.GetComponent<NumericComponent>();
                        if (monsterNum != null)
                        {
                            Log.Debug($"[战斗] 发现怪物 {monsterId} - 生命值: {monsterNum.GetAsInt(NumericType.HP)}, 攻击力: {monsterNum.GetAsInt(NumericType.Attack)}, 速度: {monsterNum.GetAsInt(NumericType.Speed)}");
                        }
                    }
                    else
                    {
                        Log.Debug($"[战斗] 怪物 {monsterId} 未找到");
                    }
                }

                if (monsters.Count == 0)
                {
                    response.Error = 1; // Combat no monsters error
                    response.Message = "No valid monsters found";
                    return;
                }

                // 检查是否已在战斗中
                var existingCombat = unit.GetComponent<TurnBasedCombatComponent>();
                if (existingCombat != null && existingCombat.InCombat)
                {
                    response.Error = 2; // Combat already in progress error
                    response.Message = "Already in combat";
                    return;
                }

                // 移除旧的战斗组件
                unit.RemoveComponent<TurnBasedCombatComponent>();

                // 创建新的战斗组件
                var combat = unit.AddComponent<TurnBasedCombatComponent>();

                // 输出玩家信息
                var playerNum = unit.GetComponent<NumericComponent>();
                if (playerNum != null)
                {
                    Log.Debug($"[战斗] 玩家 {unit.Id} 属性 - 生命值: {playerNum.GetAsInt(NumericType.HP)}/{playerNum.GetAsInt(NumericType.MaxHP)}, 攻击力: {playerNum.GetAsInt(NumericType.Attack)}, 速度: {playerNum.GetAsInt(NumericType.Speed)}");
                }

                // 创建EntityRef
                EntityRef<TurnBasedCombatComponent> combatRef = combat;
                EntityRef<Unit> unitRef = unit;

                // 开始战斗
                await combat.StartCombatAsync(unit, monsters);

                // 重新获取引用
                combat = combatRef;
                unit = unitRef;

                // 返回行动顺序
                if (combat != null)
                {
                    response.TurnOrder.AddRange(combat.TurnOrder);
                }

                Log.Debug($"[战斗] 战斗成功开始，玩家 {unit.Id}，行动顺序: {string.Join(",", response.TurnOrder)}");
            }
            catch (Exception e)
            {
                Log.Error($"Start combat error: {e}");
                response.Error = 3; // Combat start failed error
                response.Message = e.Message;
            }
        }
    }
}