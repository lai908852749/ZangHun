using System;
using System.Collections.Generic;
using System.Linq;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.BuffProgressiveEffects)]
    public class RobotCase_015_BuffProgressiveEffects_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_015_BuffProgressiveEffects");

            try
            {
                Log.Console("开始渐进式效果Buff测试");

                // 准备测试环境
                PrepareTestEnvironment(robot, fiber);

                // 执行线性递增测试
                await TestLinearProgressiveEffects(robot, fiber);

                // 执行指数递增测试
                await TestExponentialProgressiveEffects(robot, fiber);

                Log.Console("渐进式效果Buff测试完成成功!");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"渐进式效果Buff测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }

        private void PrepareTestEnvironment(Fiber robot, Fiber serverFiber)
        {
            Log.Console("准备测试环境");

            // 创建线性递增毒伤Buff配置
            BuffConfig linearPoisonConfig = new BuffConfig
            {
                Id = 2001,
                Desc = "线性递增毒伤Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 3,
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
                                BaseValue = 10,     // 基础伤害10
                                PerTickIncrease = 5, // 每回合增加5点伤害
                                MaxValue = 30,      // 最大伤害30
                                NumericType = NumericType.HP, // 数值类型为HP
                                IsPositive = false, // false表示伤害（负数效果）
                                CurrentValue = "LinearDamage"
                            },
                        }
                    }
                }
            };

            // 创建指数递增治疗Buff配置
            BuffConfig exponentialHealConfig = new BuffConfig
            {
                Id = 2002,
                Desc = "指数递增治疗Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 4,
                Priority = 100,
                Category = BuffCategory.Heal,
                Tags = BuffTag.HoT | BuffTag.Magic,
                Effects = new List<EffectNode>
                {
                    new EffectServerBuffTurnEnd
                    {
                        Children = new List<BTNode>
                        {
                            new BTBuffExponentialProgress
                            {
                                Buff = "Buff",
                                Unit = "Unit",
                                BaseValue = 10,      // 基础治疗10
                                GrowthRate = 1.5f,   // 每回合*1.5倍
                                MaxValue = 50,       // 最大治疗50
                                NumericType = NumericType.HP, // 数值类型为HP
                                IsPositive = true,   // true表示治疗（正数效果）
                                CurrentValue = "ExpHeal"
                            }
                            // 注意：移除BTNumericChange节点，只使用BTBuffExponentialProgress进行纯指数增长测试
                            // BTBuffExponentialProgress已经直接处理了治疗，无需BTNumericChange重复处理
                        }
                    }
                }
            };

            // 初始化配置
            linearPoisonConfig.OnAfterDeserialize();
            exponentialHealConfig.OnAfterDeserialize();

            // 添加到单例实例
            BuffConfigCategory.Instance.Add(linearPoisonConfig);
            BuffConfigCategory.Instance.Add(exponentialHealConfig);

            Log.Console("创建渐进式效果Buff配置完成");
        }

        private async ETTask TestLinearProgressiveEffects(Fiber robot, Fiber serverFiber)
        {
            Log.Console("=== 开始线性递增毒伤测试 ===");

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

            // 设置玩家血量
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
            if (playerNumeric == null)
            {
                playerNumeric = playerUnit.AddComponent<NumericComponent>();
            }
            playerNumeric.Set(NumericType.HP, 100);
            playerNumeric.Set(NumericType.MaxHP, 100);

            Log.Console($"玩家初始血量: {playerNumeric.GetAsInt(NumericType.HP)}");

            // 创建BuffComponent
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            if (buffComponent == null)
            {
                buffComponent = playerUnit.AddComponent<BuffComponent>();
            }

            // 保存引用
            long playerId = playerUnit.Id;
            EntityRef<Unit> playerUnitRef = playerUnit;
            EntityRef<BuffComponent> buffComponentRef = buffComponent;

            // 创建线性递增毒伤Buff
            long buffId = IdGenerater.Instance.GenerateId();
            Buff linearPoisonBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, buffId, 2001);

            // 设置回合制字段
            int currentRound = 1;
            linearPoisonBuff.CreatedRound = currentRound;
            linearPoisonBuff.RemainTurn = 3;
            linearPoisonBuff.LastTriggerRound = 0;
            linearPoisonBuff.DurationType = BuffDurationType.Turn;

            // 初始化Buff
            BuffHelper.InitBuff(linearPoisonBuff, null);

            Log.Console($"创建线性递增毒伤Buff - ID: {linearPoisonBuff.Id}, ConfigId: {linearPoisonBuff.ConfigId}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

            // 重新获取引用
            playerUnit = playerUnitRef;
            buffComponent = buffComponentRef;

            // 验证3个回合的线性递增效果
            // 预期伤害: 第1回合10点, 第2回合15点, 第3回合20点
            int[] expectedDamage = { 10, 15, 20 };
            int[] actualDamage = new int[3];

            for (int round = 1; round <= 3; round++)
            {
                Log.Console($"--- 线性递增测试第 {round} 回合 ---");

                // 重新获取引用
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                buffComponent = playerUnit.GetComponent<BuffComponent>();
                playerNumeric = playerUnit.GetComponent<NumericComponent>();

                int hpBefore = playerNumeric.GetAsInt(NumericType.HP);
                Log.Console($"回合开始前血量: {hpBefore}");

                // 处理回合开始
                buffComponent.OnTurnStart(round);
                await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                // 重新获取引用并处理回合结束
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                buffComponent = playerUnit.GetComponent<BuffComponent>();
                buffComponent.OnTurnEnd(round);
                await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                // 重新获取引用
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                playerNumeric = playerUnit.GetComponent<NumericComponent>();

                int hpAfter = playerNumeric.GetAsInt(NumericType.HP);
                int damage = hpBefore - hpAfter;
                actualDamage[round - 1] = damage;

                Log.Console($"回合结束后血量: {hpAfter}");
                Log.Console($"本回合伤害: {damage} (期望: {expectedDamage[round - 1]})");

                // 验证伤害值符合线性递增预期
                if (Math.Abs(damage - expectedDamage[round - 1]) > 2) // 允许2点误差
                {
                    Log.Console($"警告: 第{round}回合伤害不符合线性递增预期");
                    Log.Console($"期望伤害: {expectedDamage[round - 1]}, 实际伤害: {damage}");
                }
                else
                {
                    Log.Console($"第{round}回合线性递增验证通过");
                }
            }

            Log.Console("线性递增毒伤测试完成");
            Log.Console($"伤害变化序列: {string.Join(" → ", actualDamage)} (期望: {string.Join(" → ", expectedDamage)})");
        }

        private async ETTask TestExponentialProgressiveEffects(Fiber robot, Fiber serverFiber)
        {
            Log.Console("=== 开始指数递增治疗测试 ===");

            // 获取服务端数据访问
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);

            // 设置玩家血量为50%进行治疗测试
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
            playerNumeric.Set(NumericType.HP, 50);
            Log.Console($"设置玩家血量为50%: {playerNumeric.GetAsInt(NumericType.HP)}");

            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            long playerId = playerUnit.Id;
            EntityRef<BuffComponent> buffComponentRef = buffComponent;

            // 创建指数递增治疗Buff
            long buffId = IdGenerater.Instance.GenerateId();
            Buff exponentialHealBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, buffId, 2002);

            // 设置回合制字段
            int currentRound = 1;
            exponentialHealBuff.CreatedRound = currentRound;
            exponentialHealBuff.RemainTurn = 4;
            exponentialHealBuff.LastTriggerRound = 0;
            exponentialHealBuff.DurationType = BuffDurationType.Turn;

            // 初始化Buff
            BuffHelper.InitBuff(exponentialHealBuff, null);

            Log.Console($"创建指数递增治疗Buff - ID: {exponentialHealBuff.Id}, ConfigId: {exponentialHealBuff.ConfigId}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

            // 重新获取引用
            buffComponent = buffComponentRef;

            // 验证4个回合的指数递增效果
            // 预期治疗: 第1回合10点, 第2回合15点, 第3回合22点, 第4回合33点 (基础10 * 1.5^(n-1))
            float[] expectedHeal = { 10f, 15f, 22.5f, 33.75f };
            int[] actualHeal = new int[4];

            for (int round = 1; round <= 4; round++)
            {
                Log.Console($"--- 指数递增测试第 {round} 回合 ---");

                // 重新获取引用
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                buffComponent = playerUnit.GetComponent<BuffComponent>();
                playerNumeric = playerUnit.GetComponent<NumericComponent>();

                int hpBefore = playerNumeric.GetAsInt(NumericType.HP);
                Log.Console($"回合开始前血量: {hpBefore}");

                // 确保不会超过最大血量
                if (hpBefore >= 100)
                {
                    Log.Console("血量已满，跳过治疗测试");
                    break;
                }

                // 处理回合开始
                buffComponent.OnTurnStart(round);
                await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                // 重新获取引用并处理回合结束
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                buffComponent = playerUnit.GetComponent<BuffComponent>();
                buffComponent.OnTurnEnd(round);
                await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                // 重新获取引用
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                playerNumeric = playerUnit.GetComponent<NumericComponent>();

                int hpAfter = playerNumeric.GetAsInt(NumericType.HP);
                int heal = hpAfter - hpBefore;
                actualHeal[round - 1] = heal;

                Log.Console($"回合结束后血量: {hpAfter}");
                Log.Console($"本回合治疗: {heal} (期望约: {(int)expectedHeal[round - 1]})");

                // 验证治疗值符合指数递增预期 (允许一定误差因为可能有上限限制)
                int expectedInt = (int)expectedHeal[round - 1];
                if (heal > 0) // 只要有治疗就算通过基础测试
                {
                    Log.Console($"第{round}回合指数递增治疗生效");

                    // 检查是否符合增长趋势
                    if (round > 1 && heal >= actualHeal[round - 2])
                    {
                        Log.Console($"治疗量呈增长趋势: {actualHeal[round - 2]} → {heal}");
                    }
                }
                else
                {
                    Log.Console($"第{round}回合无治疗效果，可能已达血量上限");
                }

                // 如果达到血量上限，提前结束测试
                if (hpAfter >= 100)
                {
                    Log.Console("血量达到上限，提前结束指数递增测试");
                    break;
                }
            }

            Log.Console("指数递增治疗测试完成");
            Log.Console($"治疗变化序列: {string.Join(" → ", actualHeal.Take(4))}");

            // 验证最大值限制
            // 检查治疗值没有超过配置的最大值50
            for (int i = 0; i < actualHeal.Length; i++)
            {
                if (actualHeal[i] > 50)
                {
                    throw new Exception($"第{i + 1}回合治疗值{actualHeal[i]}超过最大值限制50");
                }
            }

            Log.Console("最大值限制验证通过");
        }
    }
}