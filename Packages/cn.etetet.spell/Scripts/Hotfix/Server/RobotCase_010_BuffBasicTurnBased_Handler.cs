using System;
using System.Collections.Generic;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.BuffBasicTurnBased)]
    public class RobotCase_010_BuffBasicTurnBased_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_010_BuffBasicTurnBased");

            try
            {
                Log.Console("开始基础回合制Buff测试");

                // 1. Create BuffConfig for testing
                BuffConfig testBuffConfig = new BuffConfig
                {
                    Id = 1001,
                    Desc = "测试毒伤Buff",
                    DurationType = BuffDurationType.Turn,
                    TurnDuration = 3,
                    Priority = 100,
                    Category = BuffCategory.Damage,
                    Tags = BuffTag.DoT,
                    Effects = new List<EffectNode>
                    {
                        new EffectServerBuffTurnEnd
                        {
                            Children = new List<BTNode>
                            {
                                new BTDamage
                                {
                                    Caster = "Caster",
                                    Target = "Unit",
                                    Buff = "Buff",
                                    Value = 10  // 每回合造成10点伤害
                                }
                            }
                        }
                    }
                };

                // Initialize effect dictionary
                testBuffConfig.OnAfterDeserialize();

                // Add test config to singleton instance
                BuffConfigCategory.Instance.Add(testBuffConfig);

                // Get server map fiber
                string mapName = robot.Root.CurrentScene().Name;
                Fiber map = fiber.GetFiber("MapManager").GetFiber(mapName);
                if (map == null)
                {
                    Log.Error($"地图未找到: {mapName}");
                    return ErrorCode.ERR_NotFoundUnit;
                }

                // Get player component
                Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
                if (playerComponent == null)
                {
                    Log.Error("机器人上未找到PlayerComponent");
                    return ErrorCode.ERR_NotFoundUnit;
                }

                // Get server player unit
                Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);
                if (playerUnit == null)
                {
                    Log.Error($"玩家单位未找到: {playerComponent.MyId}");
                    return ErrorCode.ERR_NotFoundUnit;
                }

                // 2. Setup player stats
                NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
                if (playerNumeric == null)
                {
                    playerNumeric = playerUnit.AddComponent<NumericComponent>();
                }
                playerNumeric.Set(NumericType.HP, 100);
                playerNumeric.Set(NumericType.MaxHP, 100);

                Log.Console($"玩家初始血量: {playerNumeric.GetAsInt(NumericType.HP)}");

                // 3. Create BuffComponent if not exists
                BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
                if (buffComponent == null)
                {
                    buffComponent = playerUnit.AddComponent<BuffComponent>();
                }

                // Store player ID before await
                long playerId = playerUnit.Id;
                EntityRef<Unit> playerUnitRef = playerUnit;

                // 4. Create turn-based poison buff
                long buffId = IdGenerater.Instance.GenerateId();
                Buff poisonBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, buffId, 1001);

                // Set turn-based fields
                int currentRound = 1;
                poisonBuff.CreatedRound = currentRound;
                poisonBuff.RemainTurn = 3;
                poisonBuff.LastTriggerRound = 0;
                poisonBuff.DurationType = BuffDurationType.Turn;

                // Initialize the buff
                BuffHelper.InitBuff(poisonBuff, null);

                Log.Console($"创建回合制毒伤Buff - ID: {poisonBuff.Id}, ConfigId: {poisonBuff.ConfigId}");
                Log.Console($"Buff字段 - 创建回合: {poisonBuff.CreatedRound}, 剩余回合: {poisonBuff.RemainTurn}, 持续类型: {poisonBuff.DurationType}");

                // Wait a bit for initialization
                await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

                // 5. Verify initial state after await
                // Re-get map reference after await
                map = fiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = playerUnitRef;
                if (playerUnit == null || playerUnit.IsDisposed)
                {
                    Log.Error("Buff创建后玩家单位被销毁");
                    return ErrorCode.ERR_Exception;
                }

                buffComponent = playerUnit.GetComponent<BuffComponent>();
                if (buffComponent == null)
                {
                    Log.Error("Buff创建后未找到BuffComponent");
                    return ErrorCode.ERR_Exception;
                }

                // Verify buff exists
                Buff createdBuff = buffComponent.GetChild<Buff>(buffId);
                if (createdBuff == null)
                {
                    Log.Error("在BuffComponent中未找到创建的Buff");
                    return ErrorCode.ERR_Exception;
                }

                // Verify turn-based fields
                if (createdBuff.CreatedRound != currentRound)
                {
                    Log.Error($"创建回合不匹配: 期望 {currentRound}, 实际 {createdBuff.CreatedRound}");
                    return ErrorCode.ERR_Exception;
                }

                if (createdBuff.RemainTurn != 3)
                {
                    Log.Error($"剩余回合不匹配: 期望 3, 实际 {createdBuff.RemainTurn}");
                    return ErrorCode.ERR_Exception;
                }

                if (createdBuff.DurationType != BuffDurationType.Turn)
                {
                    Log.Error($"持续类型不匹配: 期望 Turn, 实际 {createdBuff.DurationType}");
                    return ErrorCode.ERR_Exception;
                }

                Log.Console("初始Buff状态验证通过");

                // 6. Test turn progression (3 rounds)
                for (int round = 1; round <= 3; round++)
                {
                    Log.Console($"--- 第 {round} 回合 ---");

                    // Get fresh references
                    playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                    if (playerUnit == null)
                    {
                        Log.Error($"第{round}回合中未找到玩家单位");
                        return ErrorCode.ERR_Exception;
                    }

                    buffComponent = playerUnit.GetComponent<BuffComponent>();
                    playerNumeric = playerUnit.GetComponent<NumericComponent>();

                    int hpBefore = playerNumeric.GetAsInt(NumericType.HP);
                    Log.Console($"回合处理前血量: {hpBefore}");

                    // Process turn start
                    buffComponent.OnTurnStart(round);

                    // Wait a bit
                    await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                    // Get fresh references after await
                    map = fiber.GetFiber("MapManager").GetFiber(mapName);
                    playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                    buffComponent = playerUnit.GetComponent<BuffComponent>();

                    // Check buff state after turn start
                    createdBuff = buffComponent.GetChild<Buff>(buffId);
                    if (round <= 3 && createdBuff != null)
                    {
                        Log.Console($"回合开始后Buff状态 - 剩余回合: {createdBuff.RemainTurn}, 最后触发回合: {createdBuff.LastTriggerRound}");
                    }

                    // Process turn end
                    buffComponent.OnTurnEnd(round);

                    // Wait a bit
                    await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                    // Get fresh references after turn processing
                    map = fiber.GetFiber("MapManager").GetFiber(mapName);
                    playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                    buffComponent = playerUnit.GetComponent<BuffComponent>();
                    playerNumeric = playerUnit.GetComponent<NumericComponent>();

                    int hpAfter = playerNumeric.GetAsInt(NumericType.HP);
                    Log.Console($"回合处理后血量: {hpAfter}");

                    // Check if buff still exists
                    createdBuff = buffComponent.GetChild<Buff>(buffId);

                    if (round < 3)
                    {
                        // Buff should still exist for rounds 1 and 2
                        if (createdBuff == null)
                        {
                            Log.Error($"第{round}回合Buff应该仍然存在");
                            return ErrorCode.ERR_Exception;
                        }

                        int expectedRemainTurn = 3 - round;
                        if (createdBuff.RemainTurn != expectedRemainTurn)
                        {
                            Log.Error($"第{round}回合剩余回合数不匹配: 期望 {expectedRemainTurn}, 实际 {createdBuff.RemainTurn}");
                            return ErrorCode.ERR_Exception;
                        }

                        Log.Console($"第{round}回合 - 剩余回合: {createdBuff.RemainTurn}");

                        // Verify damage was applied (HP should decrease)
                        if (hpAfter >= hpBefore)
                        {
                            Log.Console($"警告: 第{round}回合期望血量减少, 处理前: {hpBefore}, 处理后: {hpAfter}");
                        }
                    }
                    else if (round == 3)
                    {
                        // Round 3: Check if buff still exists or was removed
                        if (createdBuff != null)
                        {
                            // If buff still exists, RemainTurn should be 0 or will be removed soon
                            Log.Console($"第{round}回合 - 剩余回合: {createdBuff.RemainTurn}");
                            if (createdBuff.RemainTurn < 0)
                            {
                                Log.Error($"剩余回合数不应该为负数: {createdBuff.RemainTurn}");
                                return ErrorCode.ERR_Exception;
                            }
                        }
                        else
                        {
                            Log.Console("第3回合后Buff正确移除");
                        }

                        // Verify damage was applied (HP should decrease)
                        if (hpAfter >= hpBefore)
                        {
                            Log.Console($"警告: 第{round}回合期望血量减少, 处理前: {hpBefore}, 处理后: {hpAfter}");
                        }
                    }
                }

                // 7. Final verification
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                buffComponent = playerUnit.GetComponent<BuffComponent>();
                playerNumeric = playerUnit.GetComponent<NumericComponent>();

                // Verify buff is completely removed
                createdBuff = buffComponent.GetChild<Buff>(buffId);
                if (createdBuff != null)
                {
                    Log.Error("3回合后Buff应该完全移除");
                    return ErrorCode.ERR_Exception;
                }

                int finalHp = playerNumeric.GetAsInt(NumericType.HP);
                Log.Console($"最终血量: {finalHp}");

                // HP should be less than initial (damage was applied over 3 turns)
                if (finalHp >= 100)
                {
                    Log.Console("警告: 毒伤作用后期望血量少于100");
                }

                Log.Console("基础回合制Buff测试完成成功!");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"基础回合制Buff测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }
    }
}