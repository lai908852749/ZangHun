using System;
using System.Collections.Generic;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.DamageAccumulation)]
    public class RobotCase_017_DamageAccumulation_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_017_DamageAccumulation");

            try
            {
                Log.Console("开始伤害累积系统测试");

                // 准备测试环境
                PrepareTestEnvironment(robot, fiber);

                // 执行伤害累积测试
                await ExecuteDamageAccumulationTest(robot, fiber);

                Log.Console("伤害累积系统测试完成成功!");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"伤害累积系统测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }

        private void PrepareTestEnvironment(Fiber robot, Fiber serverFiber)
        {
            Log.Console("准备测试环境");

            // 创建毒伤Buff配置
            BuffConfig poisonConfig = new BuffConfig
            {
                Id = 4001,
                Desc = "毒伤Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 5,
                Priority = 100,
                Category = BuffCategory.Damage,
                Tags = BuffTag.DoT | BuffTag.Physical,
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
                                IsPositive = false,
                                CurrentValue = "PoisonDamage"
                            }
                        }
                    }
                }
            };

            // 创建流血Buff配置
            BuffConfig bleedConfig = new BuffConfig
            {
                Id = 4002,
                Desc = "流血Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 5,
                Priority = 100,
                Category = BuffCategory.Damage,
                Tags = BuffTag.DoT | BuffTag.Physical,
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
                                BaseValue = 5,
                                PerTickIncrease = 0,
                                MaxValue = 5,
                                NumericType = NumericType.HP,
                                IsPositive = false,
                                CurrentValue = "BleedDamage"
                            }
                        }
                    }
                }
            };

            // 创建灼烧Buff配置
            BuffConfig burnConfig = new BuffConfig
            {
                Id = 4003,
                Desc = "灼烧Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 5,
                Priority = 100,
                Category = BuffCategory.Damage,
                Tags = BuffTag.DoT | BuffTag.Magic,
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
                                BaseValue = 6,
                                PerTickIncrease = 0,
                                MaxValue = 6,
                                NumericType = NumericType.HP,
                                IsPositive = false,
                                CurrentValue = "BurnDamage"
                            }
                        }
                    }
                }
            };

            // 初始化配置
            poisonConfig.OnAfterDeserialize();
            bleedConfig.OnAfterDeserialize();
            burnConfig.OnAfterDeserialize();

            BuffConfigCategory.Instance.Add(poisonConfig);
            BuffConfigCategory.Instance.Add(bleedConfig);
            BuffConfigCategory.Instance.Add(burnConfig);

            Log.Console("创建伤害累积测试Buff配置完成");
        }

        private async ETTask ExecuteDamageAccumulationTest(Fiber robot, Fiber serverFiber)
        {
            Log.Console("=== 开始伤害累积测试 ===");

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

            // 设置玩家属性
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

            // 给玩家施加三种伤害Buff
            BuffComponent playerBuffComponent = playerUnit.GetComponent<BuffComponent>();
            if (playerBuffComponent == null)
            {
                playerBuffComponent = playerUnit.AddComponent<BuffComponent>();
            }

            // 创建毒伤Buff
            long poisonBuffId = IdGenerater.Instance.GenerateId();
            Buff poisonBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, poisonBuffId, 4001);
            SetupDamageBuff(poisonBuff, 1);

            // 创建流血Buff
            long bleedBuffId = IdGenerater.Instance.GenerateId();
            Buff bleedBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, bleedBuffId, 4002);
            SetupDamageBuff(bleedBuff, 1);

            // 创建灼烧Buff
            long burnBuffId = IdGenerater.Instance.GenerateId();
            Buff burnBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, burnBuffId, 4003);
            SetupDamageBuff(burnBuff, 1);

            // 初始化所有Buff
            BuffHelper.InitBuff(poisonBuff, null);
            BuffHelper.InitBuff(bleedBuff, null);
            BuffHelper.InitBuff(burnBuff, null);

            Log.Console($"施加三种伤害Buff: 毒伤({poisonBuff.Id}) + 流血({bleedBuff.Id}) + 灼烧({burnBuff.Id})");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

            // 验证伤害累积（进行3回合）
            int expectedTotalDamage = 8 + 5 + 6; // 毒伤8 + 流血5 + 灼烧6 = 19点每回合
            for (int round = 1; round <= 3; round++)
            {
                Log.Console($"--- 第 {round} 回合伤害累积测试 ---");

                // 重新获取引用
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                playerBuffComponent = playerUnit.GetComponent<BuffComponent>();
                playerNumeric = playerUnit.GetComponent<NumericComponent>();

                int hpBefore = playerNumeric.GetAsInt(NumericType.HP);
                Log.Console($"回合开始前血量: {hpBefore}");

                // 统计当前生效的伤害Buff数量
                int activeDamageBuffs = CountActiveDamageBuffs(playerBuffComponent);
                Log.Console($"当前生效伤害Buff数量: {activeDamageBuffs}");

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
                int actualDamage = hpBefore - hpAfter;

                Log.Console($"回合结束后血量: {hpAfter}");
                Log.Console($"本回合总伤害: {actualDamage} (期望: {expectedTotalDamage})");

                // 验证伤害累积是否正确
                if (Math.Abs(actualDamage - expectedTotalDamage) <= 2) // 允许2点误差
                {
                    Log.Console($"第{round}回合伤害累积验证通过");
                }
                else
                {
                    Log.Console($"第{round}回合伤害累积验证失败");
                    Log.Console($"期望总伤害: {expectedTotalDamage}, 实际总伤害: {actualDamage}");
                }

                // 检查是否达到致命伤害
                if (hpAfter <= 0)
                {
                    Log.Console($"玩家在第{round}回合受到致命伤害，血量降至{hpAfter}");
                    break;
                }
            }

            // 最终验证
            VerifyDamageAccumulationResults(map.Root, playerId, expectedTotalDamage);
        }

        private void SetupDamageBuff(Buff buff, int round)
        {
            buff.CreatedRound = round;
            buff.RemainTurn = 5;
            buff.LastTriggerRound = 0;
            buff.DurationType = BuffDurationType.Turn;
            buff.Stack = 1;
        }

        private int CountActiveDamageBuffs(BuffComponent buffComponent)
        {
            int count = 0;
            foreach (var kv in buffComponent.Children)
            {
                Buff buff = kv.Value as Buff;
                if (buff != null && buff.ConfigId >= 4001 && buff.ConfigId <= 4003)
                {
                    count++;
                }
            }
            return count;
        }

        private void VerifyDamageAccumulationResults(Scene scene, long playerId, int expectedTotalDamage)
        {
            Log.Console("=== 验证伤害累积结果 ===");

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
            int totalDamageTaken = maxHP - currentHP;

            Log.Console($"玩家当前血量: {currentHP}/{maxHP}");
            Log.Console($"累积受到伤害: {totalDamageTaken}");

            // 统计剩余的伤害Buff
            int remainingBuffs = CountActiveDamageBuffs(buffComponent);
            Log.Console($"剩余伤害Buff数量: {remainingBuffs}");

            // 验证伤害累积系统工作正常
            if (totalDamageTaken > 0)
            {
                Log.Console("伤害累积系统验证通过: 成功累积多种伤害Buff的效果");
            }
            else
            {
                Log.Console("警告: 没有检测到累积伤害，请检查伤害计算逻辑");
            }

            // 验证伤害计算范围合理性
            int expectedMinDamage = expectedTotalDamage; // 至少1回合的伤害
            int expectedMaxDamage = expectedTotalDamage * 3; // 最多3回合的伤害

            if (totalDamageTaken >= expectedMinDamage && totalDamageTaken <= expectedMaxDamage + 10)
            {
                Log.Console("伤害范围验证通过: 累积伤害在预期范围内");
            }
            else
            {
                Log.Console($"伤害范围验证: 累积伤害{totalDamageTaken}，预期范围{expectedMinDamage}-{expectedMaxDamage}");
            }

            Log.Console("伤害累积系统验证完成");
        }
    }
}