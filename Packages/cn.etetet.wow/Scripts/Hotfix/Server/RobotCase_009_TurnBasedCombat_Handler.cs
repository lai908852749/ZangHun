using System;
using System.Collections.Generic;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.TurnBasedCombat)]
    public class RobotCase_009_TurnBasedCombat_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            // Create robot fiber (same as RobotCase_001)
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_009_TurnBasedCombat");
            
            try
            {
                Log.Console("开始回合制战斗测试");
            
                // Get server map fiber
                string mapName = robot.Root.CurrentScene().Name;
                Fiber map = fiber.GetFiber("MapManager").GetFiber(mapName);
                if (map == null)
                {
                    Log.Error($"未找到机器人地图 {mapName}");
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
                    Log.Error($"未找到玩家单位: {playerComponent.MyId}");
                    return ErrorCode.ERR_NotFoundUnit;
                }
            
                // Set player HP and Attack for testing
                NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
                if (playerNumeric == null)
                {
                    playerNumeric = playerUnit.AddComponent<NumericComponent>();
                }
                playerNumeric.Set(NumericType.HP, 100);
                playerNumeric.Set(NumericType.MaxHP, 100);
                playerNumeric.Set(NumericType.Attack, 20);
                playerNumeric.Set(NumericType.Speed, 10);
            
                Log.Console($"玩家设置 - 生命值: {playerNumeric.GetAsInt(NumericType.HP)}, 攻击力: {playerNumeric.GetAsInt(NumericType.Attack)}, 速度: {playerNumeric.GetAsInt(NumericType.Speed)}");
            
                // Store ID before await
                long playerId = playerUnit.Id;
            
                // Create test monsters
                var monsterIds = CreateTestMonsters(map, playerUnit.Position);
                Log.Console($"创建了 {monsterIds.Count} 个测试怪物");
            
                // Start combat
                C2M_StartCombat startCombatMsg = C2M_StartCombat.Create();
                startCombatMsg.MonsterIds.AddRange(monsterIds);
            
                M2C_StartCombat startCombatResponse = await robot.Root.GetComponent<ClientSenderComponent>().Call(startCombatMsg) as M2C_StartCombat;
            
                if (startCombatResponse.Error != ErrorCode.ERR_Success)
                {
                    Log.Error($"开始战斗失败: {startCombatResponse.Error} - {startCombatResponse.Message}");
                    return startCombatResponse.Error;
                }
            
                Log.Console($"战斗开始，行动顺序: {string.Join(",", startCombatResponse.TurnOrder)}");
            
                // Simulate combat - pass ID instead of Entity
                await SimulateCombat(robot, fiber, playerId);
            
                // Verify combat result after combat - need to get unit again
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                if (playerUnit == null)
                {
                    Log.Error("战斗后未找到玩家单位");
                    return ErrorCode.ERR_NotFoundUnit;
                }
                playerNumeric = playerUnit.GetComponent<NumericComponent>();
                TurnBasedCombatComponent combat = playerUnit.GetComponent<TurnBasedCombatComponent>();
                if (combat != null && !combat.InCombat)
                {
                    CombatRecordComponent record = combat.GetComponent<CombatRecordComponent>();
                    if (record != null)
                    {
                        Log.Console($"战斗完成 - 总回合数: {record.TotalRounds}, 动作数: {record.Actions.Count}");
            
                        // Verify player won
                        int playerHp = playerNumeric.GetAsInt(NumericType.HP);
                        if (playerHp <= 0)
                        {
                            Log.Error("测试失败: 玩家应该赢得战斗");
                            return ErrorCode.ERR_Exception;
                        }
            
                        // Verify rounds > 0
                        if (record.TotalRounds <= 0)
                        {
                            Log.Error("测试失败: 总回合数应该大于0");
                            return ErrorCode.ERR_Exception;
                        }
                    }
                }
                else
                {
                    Log.Error("战斗未正常结束");
                    return ErrorCode.ERR_Exception;
                }
            
                Log.Console("测试成功！回合制战斗测试完成");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"回合制战斗测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }

        private List<long> CreateTestMonsters(Fiber mapFiber, Unity.Mathematics.float3 playerPosition)
        {
            var monsterIds = new List<long>();
            UnitComponent unitComponent = mapFiber.Root.GetComponent<UnitComponent>();

            // Create 2 test monsters
            for (int i = 0; i < 2; i++)
            {
                // Create monster unit
                long monsterId = IdGenerater.Instance.GenerateId();
                Unit monster = UnitFactory.Create(mapFiber.Root, monsterId, 1002 + i); // Use configIds 1002 (TrainingDummy) and 1003 (Boar)
                monster.Position = new Unity.Mathematics.float3(playerPosition.x + (i + 1) * 2, playerPosition.y, playerPosition.z);

                // Set monster stats
                NumericComponent monsterNumeric = monster.GetComponent<NumericComponent>();
                if (monsterNumeric == null)
                {
                    monsterNumeric = monster.AddComponent<NumericComponent>();
                }

                // Set weak monsters so player can win
                monsterNumeric.Set(NumericType.HP, 30);
                monsterNumeric.Set(NumericType.MaxHP, 30);
                monsterNumeric.Set(NumericType.Attack, 5);
                monsterNumeric.Set(NumericType.Speed, 5 + i); // Different speeds for turn order

                unitComponent.Add(monster);
                monsterIds.Add(monster.Id);

                Log.Console($"创建怪物 {i + 1} - ID: {monster.Id}, 生命值: {monsterNumeric.GetAsInt(NumericType.HP)}, 攻击力: {monsterNumeric.GetAsInt(NumericType.Attack)}, 速度: {monsterNumeric.GetAsInt(NumericType.Speed)}");
            }

            return monsterIds;
        }

        private async ETTask SimulateCombat(Fiber robot, Fiber serverFiber, long playerId)
        {
            // Get map fiber
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            EntityRef<Unit> playerUnitRef = playerUnit;
            bool combatEnded = false;
            int actionCount = 0;

            // Combat loop - wait for turn messages and respond
            while (!combatEnded && actionCount < 50) // Max 50 actions to prevent infinite loop
            {
                // Check if combat ended
                playerUnit = playerUnitRef;
                if (playerUnit == null || playerUnit.IsDisposed)
                {
                    break;
                }

                TurnBasedCombatComponent combat = playerUnit.GetComponent<TurnBasedCombatComponent>();
                if (combat == null || !combat.InCombat)
                {
                    combatEnded = true;
                    break;
                }

                // Check if it's player turn
                if (combat.CurrentActorId == playerUnit.Id)
                {
                    Log.Console($"玩家回合 - 第 {combat.RoundNumber} 回合, 回合索引 {combat.CurrentTurnIndex}");

                    // Find a valid target
                    UnitComponent unitComponent = playerUnit.Root().GetComponent<UnitComponent>();
                    long targetId = 0;

                    foreach (long monsterId in combat.MonsterUnitIds)
                    {
                        Unit monster = unitComponent.Get(monsterId);
                        if (monster != null && !monster.IsDisposed)
                        {
                            var monsterNum = monster.GetComponent<NumericComponent>();
                            if (monsterNum != null && monsterNum.GetAsInt(NumericType.HP) > 0)
                            {
                                targetId = monsterId;
                                break;
                            }
                        }
                    }

                    if (targetId > 0)
                    {
                        // Send attack action
                        C2M_CombatAction actionMsg = C2M_CombatAction.Create();
                        actionMsg.ActionType = 1; // Attack
                        actionMsg.TargetId = targetId;

                        robot.Root.GetComponent<ClientSenderComponent>().Send(actionMsg);
                        Log.Console($"玩家攻击怪物 {targetId}");
                        actionCount++;
                    }
                    else
                    {
                        // No valid targets, skip turn
                        C2M_CombatAction actionMsg = C2M_CombatAction.Create();
                        actionMsg.ActionType = 2; // Skip
                        actionMsg.TargetId = 0;

                        robot.Root.GetComponent<ClientSenderComponent>().Send(actionMsg);
                        Log.Console("玩家跳过回合（无有效目标）");
                        actionCount++;
                    }
                }

                // Wait a bit for next turn
                await serverFiber.Root.GetComponent<TimerComponent>().WaitAsync(100);
            }

            Log.Console($"战斗模拟完成 - 总动作数: {actionCount}");
        }
    }
}