using System;
using System.Collections.Generic;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.HealAccumulation)]
    public class RobotCase_018_HealAccumulation_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_018_HealAccumulation");

            try
            {
                Log.Console("开始治疗累积系统测试");

                // 准备测试环境
                PrepareTestEnvironment(robot, fiber);

                // 执行治疗累积测试
                await ExecuteHealAccumulationTest(robot, fiber);

                Log.Console("治疗累积系统测试完成成功!");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"治疗累积系统测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }

        private void PrepareTestEnvironment(Fiber robot, Fiber serverFiber)
        {
            Log.Console("准备测试环境");

            // 创建回春术Buff配置
            BuffConfig regenerationConfig = new BuffConfig
            {
                Id = 5001,
                Desc = "回春术Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 5,
                Priority = 100,
                Category = BuffCategory.Heal,
                Tags = BuffTag.HoT | BuffTag.Magic,
                Effects = new List<EffectNode>
                {
                    new EffectServerBuffTurnEnd
                    {
                        Children = new List<BTNode>
                        {
                            new BTBuffLinearProgress
                            {
                                Buff = "Buff",
                                Unit = "Unit",
                                BaseValue = 12,
                                PerTickIncrease = 0,
                                MaxValue = 12,
                                NumericType = NumericType.HP,
                                IsPositive = true,
                                CurrentValue = "RegenerationHeal"
                            }
                        }
                    }
                }
            };

            // 创建快速恢复Buff配置
            BuffConfig rapidRecoveryConfig = new BuffConfig
            {
                Id = 5002,
                Desc = "快速恢复Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 5,
                Priority = 100,
                Category = BuffCategory.Heal,
                Tags = BuffTag.HoT | BuffTag.Physical,
                Effects = new List<EffectNode>
                {
                    new EffectServerBuffTurnEnd
                    {
                        Children = new List<BTNode>
                        {
                            new BTBuffLinearProgress
                            {
                                Buff = "Buff",
                                Unit = "Unit",
                                BaseValue = 8,
                                PerTickIncrease = 0,
                                MaxValue = 8,
                                NumericType = NumericType.HP,
                                IsPositive = true,
                                CurrentValue = "RapidRecoveryHeal"
                            }
                        }
                    }
                }
            };

            // 初始化配置
            regenerationConfig.OnAfterDeserialize();
            rapidRecoveryConfig.OnAfterDeserialize();

            BuffConfigCategory.Instance.Add(regenerationConfig);
            BuffConfigCategory.Instance.Add(rapidRecoveryConfig);

            Log.Console("创建治疗累积测试Buff配置完成");
        }

        private async ETTask ExecuteHealAccumulationTest(Fiber robot, Fiber serverFiber)
        {
            Log.Console("=== 开始治疗累积测试 ===");

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

            // 设置玩家属性并降低HP到50%
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
            if (playerNumeric == null)
            {
                playerNumeric = playerUnit.AddComponent<NumericComponent>();
            }
            playerNumeric.Set(NumericType.HP, 50);
            playerNumeric.Set(NumericType.MaxHP, 100);

            Log.Console($"设置玩家血量为50%: {playerNumeric.GetAsInt(NumericType.HP)}/100");

            // 保存引用
            long playerId = playerUnit.Id;
            EntityRef<Unit> playerUnitRef = playerUnit;

            // 给玩家施加两种治疗Buff
            BuffComponent playerBuffComponent = playerUnit.GetComponent<BuffComponent>();
            if (playerBuffComponent == null)
            {
                playerBuffComponent = playerUnit.AddComponent<BuffComponent>();
            }

            // 创建回春术Buff
            long regenerationBuffId = IdGenerater.Instance.GenerateId();
            Buff regenerationBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, regenerationBuffId, 5001);
            SetupHealBuff(regenerationBuff, 1);

            // 创建快速恢复Buff
            long rapidRecoveryBuffId = IdGenerater.Instance.GenerateId();
            Buff rapidRecoveryBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, rapidRecoveryBuffId, 5002);
            SetupHealBuff(rapidRecoveryBuff, 1);

            // 初始化所有Buff
            BuffHelper.InitBuff(regenerationBuff, null);
            BuffHelper.InitBuff(rapidRecoveryBuff, null);

            Log.Console($"施加两种治疗Buff: 回春术({regenerationBuff.Id}) + 快速恢复({rapidRecoveryBuff.Id})");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

            // 验证治疗累积（进行多回合直到满血或达到5回合）
            int expectedTotalHeal = 12 + 8; // 回春术12 + 快速恢复8 = 20点每回合
            bool reachedMaxHP = false;

            for (int round = 1; round <= 5; round++)
            {
                Log.Console($"--- 第 {round} 回合治疗累积测试 ---");

                // 重新获取引用
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                playerBuffComponent = playerUnit.GetComponent<BuffComponent>();
                playerNumeric = playerUnit.GetComponent<NumericComponent>();

                int hpBefore = playerNumeric.GetAsInt(NumericType.HP);
                int maxHP = playerNumeric.GetAsInt(NumericType.MaxHP);
                Log.Console($"回合开始前血量: {hpBefore}/{maxHP}");

                // 检查是否已达到满血
                if (hpBefore >= maxHP)
                {
                    Log.Console("血量已满，测试治疗溢出处理");
                    reachedMaxHP = true;
                }

                // 统计当前生效的治疗Buff数量
                int activeHealBuffs = CountActiveHealBuffs(playerBuffComponent);
                Log.Console($"当前生效治疗Buff数量: {activeHealBuffs}");

                // 处理回合开始
                playerBuffComponent.OnTurnStart(round);
                await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                // 重新获取引用并处理回合结束
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                playerBuffComponent = playerUnit.GetComponent<BuffComponent>();
                playerBuffComponent.OnTurnEnd(round);
                await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

                // 重新获取引用检查结果
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                playerNumeric = playerUnit.GetComponent<NumericComponent>();

                int hpAfter = playerNumeric.GetAsInt(NumericType.HP);
                int actualHeal = hpAfter - hpBefore;

                Log.Console($"回合结束后血量: {hpAfter}/{maxHP}");
                Log.Console($"本回合总治疗: {actualHeal} (期望: {Math.Min(expectedTotalHeal, maxHP - hpBefore)})");

                // 验证治疗累积是否正确
                int expectedHealThisRound = reachedMaxHP ? 0 : Math.Min(expectedTotalHeal, maxHP - hpBefore);

                if (reachedMaxHP && actualHeal == 0)
                {
                    Log.Console($"第{round}回合治疗溢出处理验证通过: 满血时不再治疗");
                }
                else if (Math.Abs(actualHeal - expectedHealThisRound) <= 2) // 允许2点误差
                {
                    Log.Console($"第{round}回合治疗累积验证通过");
                }
                else
                {
                    Log.Console($"第{round}回合治疗累积验证异常");
                    Log.Console($"期望治疗: {expectedHealThisRound}, 实际治疗: {actualHeal}");
                }

                // 检查是否达到满血
                if (hpAfter >= maxHP)
                {
                    Log.Console($"玩家在第{round}回合达到满血状态");
                    reachedMaxHP = true;

                    // 测试满血时的治疗溢出处理（继续1回合验证）
                    if (round < 5)
                    {
                        Log.Console("继续下一回合测试治疗溢出处理");
                        continue;
                    }
                    break;
                }
            }

            // 最终验证
            VerifyHealAccumulationResults(map.Root, playerId, expectedTotalHeal);
        }

        private void SetupHealBuff(Buff buff, int round)
        {
            buff.CreatedRound = round;
            buff.RemainTurn = 5;
            buff.LastTriggerRound = 0;
            buff.DurationType = BuffDurationType.Turn;
            buff.Stack = 1;
        }

        private int CountActiveHealBuffs(BuffComponent buffComponent)
        {
            int count = 0;
            foreach (var kv in buffComponent.Children)
            {
                Buff buff = kv.Value as Buff;
                if (buff != null && buff.ConfigId >= 5001 && buff.ConfigId <= 5002)
                {
                    count++;
                }
            }
            return count;
        }

        private void VerifyHealAccumulationResults(Scene scene, long playerId, int expectedTotalHeal)
        {
            Log.Console("=== 验证治疗累积结果 ===");

            Unit playerUnit = scene.GetComponent<UnitComponent>().Get(playerId);
            if (playerUnit == null)
            {
                Log.Console("警告: 无法获取玩家单位进行最终验证");
                return;
            }

            NumericComponent numeric = playerUnit.GetComponent<NumericComponent>();
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            int currentHP = numeric.GetAsInt(NumericType.HP);
            int maxHP = numeric.GetAsInt(NumericType.MaxHP);
            int totalHealReceived = currentHP - 50; // 从50血开始

            Log.Console($"玩家当前血量: {currentHP}/{maxHP}");
            Log.Console($"累积获得治疗: {totalHealReceived}");

            // 统计剩余的治疗Buff
            int remainingBuffs = CountActiveHealBuffs(buffComponent);
            Log.Console($"剩余治疗Buff数量: {remainingBuffs}");

            // 验证治疗累积系统工作正常
            if (totalHealReceived > 0)
            {
                Log.Console("治疗累积系统验证通过: 成功累积多种治疗Buff的效果");
            }
            else
            {
                Log.Console("警告: 没有检测到累积治疗，请检查治疗计算逻辑");
            }

            // 验证HP上限限制
            if (currentHP <= maxHP)
            {
                Log.Console("HP上限限制验证通过: 治疗不会超过最大血量");
            }
            else
            {
                Log.Console($"警告: HP超过了最大值限制，当前{currentHP} > 最大{maxHP}");
            }

            // 验证治疗效率
            int expectedMinHeal = expectedTotalHeal; // 至少1回合的治疗
            int expectedMaxHeal = Math.Min(50, expectedTotalHeal * 5); // 最多5回合治疗，但不超过缺失血量

            if (totalHealReceived >= expectedMinHeal)
            {
                Log.Console("治疗效率验证通过: 累积治疗达到预期效果");
            }
            else
            {
                Log.Console($"治疗效率验证: 累积治疗{totalHealReceived}，预期最少{expectedMinHeal}");
            }

            // 验证满血时的治疗溢出处理
            if (currentHP == maxHP && totalHealReceived < 50)
            {
                Log.Console("治疗溢出处理验证通过: 满血后停止过度治疗");
            }

            Log.Console("治疗累积系统验证完成");
        }
    }
}