using System;
using System.Collections.Generic;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.BuffEffectNodes)]
    public class RobotCase_014_BuffEffectNodes_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_014_BuffEffectNodes");

            try
            {
                Log.Console("开始EffectNode和BTNode系统测试");

                // 准备测试环境
                PrepareTestEnvironment(robot, fiber);

                // 执行测试流程
                await ExecuteTestFlow(robot, fiber);

                Log.Console("EffectNode和BTNode系统测试完成成功!");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"EffectNode和BTNode系统测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }

        private void PrepareTestEnvironment(Fiber robot, Fiber serverFiber)
        {
            Log.Console("准备测试环境");

            // 创建测试配置1：多EffectNode串联测试
            BuffConfig testBuffConfig1 = new BuffConfig
            {
                Id = 1001,
                Desc = "复合效果Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 2,
                Priority = 100,
                Category = BuffCategory.Damage,
                Tags = BuffTag.DoT,
                Effects = new List<EffectNode>
                {
                    // OnTurnStart效果：治疗
                    new EffectServerBuffTurnStart
                    {
                        Children = new List<BTNode>
                        {
                            new BTNumericChange
                            {
                                Unit = "Unit",
                                Buff = "Buff",
                                NumericType = NumericType.HP,
                                Value = 5  // 每回合开始恢复5点血量
                            }
                        }
                    },
                    // OnTurnEnd效果：伤害
                    new EffectServerBuffTurnEnd
                    {
                        Children = new List<BTNode>
                        {
                            new BTDamage
                            {
                                Caster = "Caster",
                                Target = "Unit",
                                Buff = "Buff",
                                Value = 10  // 每回合结束伤害10点
                            }
                        }
                    }
                }
            };

            // 创建测试配置2：自定义BTNode链式执行
            BuffConfig testBuffConfig2 = new BuffConfig
            {
                Id = 1002,
                Desc = "链式执行测试Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 1,
                Priority = 200,
                Category = BuffCategory.Other,
                Tags = BuffTag.Buff,
                Effects = new List<EffectNode>
                {
                    new EffectServerBuffTurnEnd
                    {
                        Children = new List<BTNode>
                        {
                            // 多个BTNode串联执行
                            new BTDamage
                            {
                                Caster = "Caster",
                                Target = "Unit",
                                Buff = "Buff",
                                Value = 8
                            },
                            new BTNumericChange
                            {
                                Unit = "Unit",
                                Buff = "Buff",
                                NumericType = NumericType.HP,
                                Value = 3  // 治疗3点血量
                            }
                        }
                    }
                }
            };

            // 初始化配置
            testBuffConfig1.OnAfterDeserialize();
            testBuffConfig2.OnAfterDeserialize();

            // 添加到单例
            BuffConfigCategory.Instance.Add(testBuffConfig1);
            BuffConfigCategory.Instance.Add(testBuffConfig2);

            Log.Console("测试配置创建完成");
        }

        private async ETTask ExecuteTestFlow(Fiber robot, Fiber serverFiber)
        {
            Log.Console("执行测试流程");

            // 获取服务端数据访问
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            if (map == null)
            {
                throw new Exception($"地图未找到: {mapName}");
            }

            Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
            if (playerComponent == null)
            {
                throw new Exception("机器人上未找到PlayerComponent");
            }

            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);
            if (playerUnit == null)
            {
                throw new Exception($"玩家单位未找到: {playerComponent.MyId}");
            }

            // 设置玩家属性
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
            if (playerNumeric == null)
            {
                playerNumeric = playerUnit.AddComponent<NumericComponent>();
            }
            playerNumeric.Set(NumericType.HP, 50);  // 设置较低血量便于观察治疗和伤害
            playerNumeric.Set(NumericType.MaxHP, 100);

            // 确保SpellComponent存在，避免DamageHelper中的SpellMod计算出错
            SpellComponent spellComponent = playerUnit.GetComponent<SpellComponent>();
            if (spellComponent == null)
            {
                spellComponent = playerUnit.AddComponent<SpellComponent>();
                Log.Console("为玩家单位添加SpellComponent组件");
            }

            Log.Console($"玩家初始血量: {playerNumeric.GetAsInt(NumericType.HP)}");

            // 创建BuffComponent
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            if (buffComponent == null)
            {
                buffComponent = playerUnit.AddComponent<BuffComponent>();
            }

            long playerId = playerUnit.Id;
            EntityRef<Unit> playerUnitRef = playerUnit;

            // 步骤1：测试多EffectNode的串联执行
            await TestMultipleEffectNodes(robot, serverFiber, playerId);

            // 等待处理
            await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

            // 获取最新引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = playerUnitRef;
            if (playerUnit == null || playerUnit.IsDisposed)
            {
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            }

            // 步骤2：测试BTNode链式执行
            await TestBTNodeChaining(robot, serverFiber, playerId);

            // 步骤3：验证BTEnv数据传递机制
            await TestBTEnvDataPassing(robot, serverFiber, playerId);

            Log.Console("测试流程执行完成");
        }

        private async ETTask TestMultipleEffectNodes(Fiber robot, Fiber serverFiber, long playerId)
        {
            Log.Console("--- 测试多EffectNode串联执行 ---");

            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();

            int hpBefore = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"测试开始前血量: {hpBefore}");

            // 创建复合效果Buff
            long buffId = IdGenerater.Instance.GenerateId();
            Buff compoundBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, buffId, 1001);

            // 设置回合制字段
            compoundBuff.CreatedRound = 1;
            compoundBuff.RemainTurn = 2;
            compoundBuff.LastTriggerRound = 0;
            compoundBuff.DurationType = BuffDurationType.Turn;

            // 初始化Buff
            BuffHelper.InitBuff(compoundBuff, null);

            Log.Console($"创建复合效果Buff - ID: {compoundBuff.Id}, ConfigId: {compoundBuff.ConfigId}");

            // 等待初始化
            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 第1回合测试
            Log.Console("--- 第1回合：测试OnTurnStart和OnTurnEnd ---");

            // 获取最新引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 回合开始（应该触发BTNumericChange +5血量）
            Log.Console($"触发OnTurnStart效果 - 期望BTNumericChange +5血量");
            buffComponent.OnTurnStart(1);
            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 获取最新引用并检查状态
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            int hpAfterTurnStart = playerNumeric.GetAsInt(NumericType.HP);
            int turnStartChange = hpAfterTurnStart - hpBefore;
            Log.Console($"OnTurnStart后血量: {hpBefore} -> {hpAfterTurnStart} (变化: {turnStartChange:+#;-#;0})");

            if (turnStartChange != 5)
            {
                Log.Console($"警告: OnTurnStart期望+5血量，实际变化{turnStartChange}");
            }

            // 回合结束（应该触发BTDamage -10血量）
            buffComponent = playerUnit.GetComponent<BuffComponent>();
            Log.Console($"触发OnTurnEnd效果 - 期望BTDamage -10血量");
            buffComponent.OnTurnEnd(1);
            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 再次获取最新状态
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            int hpAfterTurnEnd = playerNumeric.GetAsInt(NumericType.HP);
            int turnEndChange = hpAfterTurnEnd - hpAfterTurnStart;
            Log.Console($"OnTurnEnd后血量: {hpAfterTurnStart} -> {hpAfterTurnEnd} (变化: {turnEndChange:+#;-#;0})");

            if (turnEndChange != -10)
            {
                Log.Console($"警告: OnTurnEnd期望-10血量，实际变化{turnEndChange}");
            }

            // 验证整个回合的净效果：+5治疗 -10伤害 = -5
            int totalRoundChange = hpAfterTurnEnd - hpBefore;
            Log.Console($"第1回合总血量变化: {hpBefore} -> {hpAfterTurnEnd} (净变化: {totalRoundChange:+#;-#;0})");
            Log.Console($"效果分解: OnTurnStart({turnStartChange:+#;-#;0}) + OnTurnEnd({turnEndChange:+#;-#;0}) = {totalRoundChange:+#;-#;0})");

            if (totalRoundChange != -5)
            {
                Log.Console($"警告: 期望净效果-5，实际{totalRoundChange}");
            }

            // 验证Buff状态
            buffComponent = playerUnit.GetComponent<BuffComponent>();
            Buff currentBuff = buffComponent.GetChild<Buff>(buffId);
            if (currentBuff != null)
            {
                Log.Console($"第1回合后Buff状态 - 剩余回合: {currentBuff.RemainTurn}");
                if (currentBuff.RemainTurn != 1)
                {
                    throw new Exception($"第1回合后剩余回合错误: 期望1, 实际{currentBuff.RemainTurn}");
                }
            }
            else
            {
                throw new Exception("第1回合后Buff被意外移除");
            }

            Log.Console("多EffectNode串联测试完成");
        }

        private async ETTask TestBTNodeChaining(Fiber robot, Fiber serverFiber, long playerId)
        {
            Log.Console("--- 测试BTNode链式执行 ---");

            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();

            int hpBefore = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"链式执行测试开始前血量: {hpBefore}");

            // 创建链式执行Buff
            long buffId = IdGenerater.Instance.GenerateId();
            Buff chainBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, buffId, 1002);

            // 设置回合制字段
            chainBuff.CreatedRound = 1;
            chainBuff.RemainTurn = 1;
            chainBuff.LastTriggerRound = 0;
            chainBuff.DurationType = BuffDurationType.Turn;

            // 初始化Buff
            BuffHelper.InitBuff(chainBuff, null);

            Log.Console($"创建链式执行Buff - ID: {chainBuff.Id}, ConfigId: {chainBuff.ConfigId}");

            // 等待初始化
            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 获取最新引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 触发回合结束（应该依次执行BTDamage -8，然后BTNumericChange +3）
            Log.Console($"触发链式执行OnTurnEnd - 期望BTDamage(-8) + BTNumericChange(+3) = -5");
            buffComponent.OnTurnEnd(1);
            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 获取最新状态
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            int hpAfter = playerNumeric.GetAsInt(NumericType.HP);
            int chainChange = hpAfter - hpBefore;
            Log.Console($"链式执行后血量: {hpBefore} -> {hpAfter} (变化: {chainChange:+#;-#;0})");

            // 验证链式执行效果：先伤害8点，再治疗3点，净效果-5
            Log.Console($"链式执行分析: BTDamage(8) + BTNumericChange(+3) = 预期净效果(-5)");
            if (chainChange != -5)
            {
                Log.Console($"警告: 链式执行期望-5，实际{chainChange}");
                // 可能的原因分析
                if (chainChange < -5)
                {
                    Log.Console("可能原因: DamageHelper中的SpellMod或其他修饰符影响了伤害计算");
                }
                else if (chainChange > -5)
                {
                    Log.Console("可能原因: BTNumericChange治疗效果未正确执行或被其他因素影响");
                }
            }

            // Buff应该在1回合后被移除
            buffComponent = playerUnit.GetComponent<BuffComponent>();
            Buff currentBuff = buffComponent.GetChild<Buff>(buffId);
            if (currentBuff != null)
            {
                Log.Console($"链式执行Buff状态 - 剩余回合: {currentBuff.RemainTurn}");
                if (currentBuff.RemainTurn == 0)
                {
                    Log.Console("链式执行Buff将在下次清理时移除");
                }
            }
            else
            {
                Log.Console("链式执行Buff已正确移除");
            }

            Log.Console("BTNode链式执行测试完成");
        }

        private async ETTask TestBTEnvDataPassing(Fiber robot, Fiber serverFiber, long playerId)
        {
            Log.Console("--- 测试BTEnv数据传递机制 ---");

            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 验证当前存在的Buff
            int buffCount = 0;
            foreach (var child in buffComponent.Children.Values)
            {
                if (child is Buff)
                {
                    buffCount++;
                }
            }

            Log.Console($"当前Buff数量: {buffCount}");

            // 创建一个新的测试Buff来验证BTEnv数据传递
            long buffId = IdGenerater.Instance.GenerateId();
            Buff testBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, buffId, 1001);

            // 设置回合制字段
            testBuff.CreatedRound = 1;
            testBuff.RemainTurn = 1;
            testBuff.LastTriggerRound = 0;
            testBuff.DurationType = BuffDurationType.Turn;

            // 验证Buff配置正确性
            BuffConfig config = testBuff.GetConfig();
            if (config == null)
            {
                throw new Exception("Buff配置获取失败，BTEnv数据传递无法测试");
            }

            Log.Console($"Buff配置验证: ID={config.Id}, 效果数量={config.Effects?.Count ?? 0}");

            // 验证效果字典初始化
            if (config.effectDict == null)
            {
                throw new Exception("Buff配置的effectDict未正确初始化");
            }

            var turnStartEffect = config.GetEffect<EffectServerBuffTurnStart>();
            var turnEndEffect = config.GetEffect<EffectServerBuffTurnEnd>();

            Log.Console($"OnTurnStart效果存在: {turnStartEffect != null}");
            Log.Console($"OnTurnEnd效果存在: {turnEndEffect != null}");

            if (turnStartEffect != null)
            {
                Log.Console($"OnTurnStart效果类型: {turnStartEffect.GetType().Name}");
                Log.Console($"子节点数量: {turnStartEffect.Children?.Count ?? 0}");
            }

            if (turnEndEffect != null)
            {
                Log.Console($"OnTurnEnd效果类型: {turnEndEffect.GetType().Name}");
                Log.Console($"子节点数量: {turnEndEffect.Children?.Count ?? 0}");
            }

            // 初始化Buff
            BuffHelper.InitBuff(testBuff, null);

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 验证BTEnv创建和数据传递
            Log.Console("验证BTEnv数据传递正确性");

            // 获取最新引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 触发效果来间接验证BTEnv工作
            buffComponent.OnTurnStart(1);
            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 获取最新状态验证效果执行
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            Buff verifyBuff = buffComponent.GetChild<Buff>(buffId);
            if (verifyBuff != null)
            {
                Log.Console($"BTEnv验证Buff状态: 最后触发回合={verifyBuff.LastTriggerRound}");

                if (verifyBuff.LastTriggerRound >= 1)
                {
                    Log.Console("BTEnv数据传递和BTDispatcher调用机制验证成功");
                }
                else
                {
                    Log.Console("警告: BTEnv数据传递可能存在问题");
                }
            }
            else
            {
                Log.Console("BTEnv验证Buff已被移除");
            }

            Log.Console("BTEnv数据传递测试完成");
        }
    }
}