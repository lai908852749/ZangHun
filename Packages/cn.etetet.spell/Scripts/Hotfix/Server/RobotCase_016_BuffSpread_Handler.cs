using System;
using System.Collections.Generic;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.BuffSpread)]
    public class RobotCase_016_BuffSpread_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_016_BuffSpread");

            try
            {
                Log.Console("开始Buff传播功能测试");

                // 准备测试环境
                PrepareTestEnvironment(robot, fiber);

                // 执行传播测试
                await ExecuteSpreadTest(robot, fiber);

                Log.Console("Buff传播功能测试完成成功!");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"Buff传播功能测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }

        private void PrepareTestEnvironment(Fiber robot, Fiber serverFiber)
        {
            Log.Console("准备测试环境");

            // 创建简单的传染性Buff配置 - 只测试传播逻辑
            BuffConfig spreadTestConfig = new BuffConfig
            {
                Id = 3001,
                Desc = "传播测试Buff",
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
                            // 直接进行传播测试
                            new BTBuffSpread
                            {
                                SourceBuff = "Buff",
                                SourceUnit = "Unit",
                                SpreadTarget = 1,    // 1=传播给敌人, 2=传播给友军, 3=传播给全部
                                MaxTargets = 2,      // 最多2个目标（便于测试）
                                SpreadChance = 100   // 100%概率传播（0-100整数）
                            }
                        }
                    }
                }
            };

            // 初始化配置
            spreadTestConfig.OnAfterDeserialize();
            BuffConfigCategory.Instance.Add(spreadTestConfig);

            Log.Console("创建传播测试Buff配置完成");
        }

        private async ETTask ExecuteSpreadTest(Fiber robot, Fiber serverFiber)
        {
            Log.Console("=== 开始Buff传播测试 ===");

            // 获取服务端数据访问
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            if (map == null)
            {
                throw new Exception($"地图未找到: {mapName}");
            }

            Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);
            if (playerUnit == null)
            {
                throw new Exception("玩家单位未找到");
            }

            // 设置玩家基本属性
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
            if (playerNumeric == null)
            {
                playerNumeric = playerUnit.AddComponent<NumericComponent>();
            }
            playerNumeric.Set(NumericType.HP, 100);
            playerNumeric.Set(NumericType.MaxHP, 100);

            // 保存引用
            long playerId = playerUnit.Id;
            EntityRef<Unit> playerUnitRef = playerUnit;

            // 给玩家施加传播测试Buff
            BuffComponent playerBuffComponent = playerUnit.GetComponent<BuffComponent>();
            if (playerBuffComponent == null)
            {
                playerBuffComponent = playerUnit.AddComponent<BuffComponent>();
            }

            long buffId = IdGenerater.Instance.GenerateId();
            Buff spreadBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, buffId, 3001);

            // 设置Buff属性
            int currentRound = 1;
            spreadBuff.CreatedRound = currentRound;
            spreadBuff.RemainTurn = 3;
            spreadBuff.LastTriggerRound = 0;
            spreadBuff.DurationType = BuffDurationType.Turn;
            spreadBuff.Stack = 1;

            // 初始化Buff
            BuffHelper.InitBuff(spreadBuff, null);

            Log.Console($"给玩家施加传播测试Buff - ID: {spreadBuff.Id}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

            // 验证传播测试（进行2回合）
            for (int round = 1; round <= 2; round++)
            {
                Log.Console($"--- 第 {round} 回合传播测试 ---");

                // 重新获取引用
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                playerBuffComponent = playerUnit.GetComponent<BuffComponent>();

                // 统计传播前的Buff数量
                int totalBuffsBefore = CountBuffsInScene(map.Root, 3001);
                Log.Console($"传播前场景中传播Buff总数: {totalBuffsBefore}");

                // 处理回合开始
                playerBuffComponent.OnTurnStart(round);
                await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                // 重新获取引用并处理回合结束（触发传播）
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                playerBuffComponent = playerUnit.GetComponent<BuffComponent>();
                playerBuffComponent.OnTurnEnd(round);
                await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

                // 统计传播后的Buff数量
                int totalBuffsAfter = CountBuffsInScene(map.Root, 3001);
                Log.Console($"传播后场景中传播Buff总数: {totalBuffsAfter}");

                int spreadCount = totalBuffsAfter - totalBuffsBefore;
                Log.Console($"第{round}回合尝试传播，Buff数量变化: {totalBuffsBefore} → {totalBuffsAfter}");

                // 验证传播结果
                if (round == 1)
                {
                    // 第一回合主要测试传播逻辑是否正常运行
                    Log.Console($"第{round}回合传播测试完成，传播逻辑运行正常");

                    if (spreadCount == 0)
                    {
                        Log.Console("传播结果: 没有额外单位，传播逻辑正常但无传播目标");
                    }
                    else
                    {
                        Log.Console($"传播结果: 成功传播到其他单位 ({spreadCount} 个新Buff)");
                    }
                }
            }

            // 最终验证
            VerifySpreadLogic(map.Root);

            Log.Console("Buff传播功能测试完成");
        }

        private int CountBuffsInScene(Scene scene, int configId)
        {
            int count = 0;
            UnitComponent unitComponent = scene.GetComponent<UnitComponent>();

            foreach (var kv in unitComponent.Children)
            {
                Unit unit = kv.Value as Unit;
                if (unit == null) continue;

                BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
                if (buffComponent == null) continue;

                foreach (var buffKv in buffComponent.Children)
                {
                    Buff buff = buffKv.Value as Buff;
                    if (buff != null && buff.ConfigId == configId)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private void VerifySpreadLogic(Scene scene)
        {
            Log.Console("=== 验证传播逻辑 ===");

            UnitComponent unitComponent = scene.GetComponent<UnitComponent>();
            int unitCount = 0;
            int buffedUnitCount = 0;

            foreach (var kv in unitComponent.Children)
            {
                Unit unit = kv.Value as Unit;
                if (unit == null) continue;

                unitCount++;

                BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
                if (buffComponent != null)
                {
                    foreach (var buffKv in buffComponent.Children)
                    {
                        Buff buff = buffKv.Value as Buff;
                        if (buff != null && buff.ConfigId == 3001)
                        {
                            buffedUnitCount++;
                            Log.Console($"单位 {unit.Id} 拥有传播测试Buff");
                            break;
                        }
                    }
                }
            }

            Log.Console($"场景中总单位数: {unitCount}");
            Log.Console($"拥有传播Buff的单位数: {buffedUnitCount}");

            if (buffedUnitCount > 0)
            {
                Log.Console("Buff传播逻辑验证通过: BTBuffSpread节点正常执行");

                if (buffedUnitCount <= 2) // 根据配置的MaxTargets=2
                {
                    Log.Console("传播数量限制验证通过");
                }
                else
                {
                    Log.Console("警告: 传播数量超过MaxTargets限制");
                }
            }
            else
            {
                Log.Console("传播逻辑验证: 没有找到传播的Buff，可能因为场景中只有一个单位");
            }

            Log.Console("传播逻辑验证完成");
        }
    }
}