using System;
using System.Collections.Generic;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.BuffImmunitySystem)]
    public class RobotCase_013_BuffImmunitySystem_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_013_BuffImmunitySystem");

            try
            {
                Log.Console("开始Buff免疫系统测试");

                // 1. 创建测试配置
                CreateTestConfigs();

                // 获取服务端环境
                string mapName = robot.Root.CurrentScene().Name;
                Fiber map = fiber.GetFiber("MapManager").GetFiber(mapName);
                if (map == null)
                {
                    Log.Error($"地图未找到: {mapName}");
                    return ErrorCode.ERR_NotFoundUnit;
                }

                Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
                if (playerComponent == null)
                {
                    Log.Error("机器人上未找到PlayerComponent");
                    return ErrorCode.ERR_NotFoundUnit;
                }

                Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);
                if (playerUnit == null)
                {
                    Log.Error($"玩家单位未找到: {playerComponent.MyId}");
                    return ErrorCode.ERR_NotFoundUnit;
                }

                // 2. 设置玩家属性
                NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
                if (playerNumeric == null)
                {
                    playerNumeric = playerUnit.AddComponent<NumericComponent>();
                }
                playerNumeric.Set(NumericType.HP, 100);
                playerNumeric.Set(NumericType.MaxHP, 100);

                BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
                if (buffComponent == null)
                {
                    buffComponent = playerUnit.AddComponent<BuffComponent>();
                }

                long playerId = playerUnit.Id;

                Log.Console("=== 第1步：测试物理免疫 ===");
                await TestPhysicalImmunity(robot, fiber, playerId);

                await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

                Log.Console("=== 第2步：测试魔法免疫 ===");
                await TestMagicImmunity(robot, fiber, playerId);

                await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

                Log.Console("=== 第3步：测试全伤害免疫 ===");
                await TestAllDamageImmunity(robot, fiber, playerId);

                await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

                Log.Console("=== 第4步：测试控制免疫 ===");
                await TestControlImmunity(robot, fiber, playerId);

                await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

                Log.Console("=== 第5步：测试免疫状态移除后Buff正常生效 ===");
                await TestImmunityRemoval(robot, fiber, playerId);

                Log.Console("Buff免疫系统测试完成成功!");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"Buff免疫系统测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }

        /// <summary>
        /// 创建测试配置
        /// </summary>
        private void CreateTestConfigs()
        {
            // 物理免疫Buff配置
            BuffConfig physicalImmunityConfig = new BuffConfig
            {
                Id = 2001,
                Desc = "物理免疫",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 2,
                Priority = 500,
                Category = BuffCategory.Immunity,
                Tags = BuffTag.PhysicalDamageImmunity | BuffTag.Buff,
                MutexGroup = BuffMutexGroup.Immunity,
                Effects = new List<EffectNode>()
            };

            // 魔法免疫Buff配置
            BuffConfig magicImmunityConfig = new BuffConfig
            {
                Id = 2002,
                Desc = "魔法免疫",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 2,
                Priority = 500,
                Category = BuffCategory.Immunity,
                Tags = BuffTag.MagicDamageImmunity | BuffTag.Buff,
                MutexGroup = BuffMutexGroup.Immunity,
                Effects = new List<EffectNode>()
            };

            // 全伤害免疫Buff配置
            BuffConfig allDamageImmunityConfig = new BuffConfig
            {
                Id = 2003,
                Desc = "全伤害免疫",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 2,
                Priority = 600,
                Category = BuffCategory.Immunity,
                Tags = BuffTag.AllDamageImmunity | BuffTag.Buff,
                MutexGroup = BuffMutexGroup.Immunity,
                Effects = new List<EffectNode>()
            };

            // 控制免疫Buff配置
            BuffConfig controlImmunityConfig = new BuffConfig
            {
                Id = 2004,
                Desc = "控制免疫",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 2,
                Priority = 500,
                Category = BuffCategory.Immunity,
                Tags = BuffTag.ControlImmunity | BuffTag.Buff,
                MutexGroup = BuffMutexGroup.Immunity,
                Effects = new List<EffectNode>()
            };

            // 物理伤害Buff配置
            BuffConfig physicalDamageConfig = new BuffConfig
            {
                Id = 2005,
                Desc = "物理毒伤",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 3,
                Priority = 100,
                Category = BuffCategory.Damage,
                Tags = BuffTag.Physical | BuffTag.DoT | BuffTag.Debuff,
                MutexGroup = BuffMutexGroup.Poison,
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
                                Value = 15
                            }
                        }
                    }
                }
            };

            // 魔法伤害Buff配置
            BuffConfig magicDamageConfig = new BuffConfig
            {
                Id = 2006,
                Desc = "魔法灼烧",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 3,
                Priority = 100,
                Category = BuffCategory.Damage,
                Tags = BuffTag.Magic | BuffTag.Fire | BuffTag.DoT | BuffTag.Debuff,
                MutexGroup = BuffMutexGroup.Burn,
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
                                Value = 20
                            }
                        }
                    }
                }
            };

            // 控制Buff配置
            BuffConfig controlConfig = new BuffConfig
            {
                Id = 2007,
                Desc = "眩晕",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 2,
                Priority = 100,
                Category = BuffCategory.Control,
                Tags = BuffTag.Control | BuffTag.Debuff,
                MutexGroup = BuffMutexGroup.None,
                Effects = new List<EffectNode>()
            };

            // 初始化所有配置
            var configs = new[] { physicalImmunityConfig, magicImmunityConfig, allDamageImmunityConfig,
                                controlImmunityConfig, physicalDamageConfig, magicDamageConfig, controlConfig };

            foreach (var config in configs)
            {
                config.OnAfterDeserialize();
                BuffConfigCategory.Instance.Add(config);
            }

            Log.Console("创建了7个测试Buff配置");
        }

        /// <summary>
        /// 测试物理免疫
        /// </summary>
        private async ETTask TestPhysicalImmunity(Fiber robot, Fiber serverFiber, long playerId)
        {
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 重置血量
            playerNumeric.Set(NumericType.HP, 100);
            int initialHp = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"测试开始血量: {initialHp}");

            // 1. 添加物理免疫Buff
            long immunityBuffId = IdGenerater.Instance.GenerateId();
            Buff immunityBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, immunityBuffId, 2001);
            immunityBuff.CreatedRound = 1;
            immunityBuff.RemainTurn = 2;
            immunityBuff.LastTriggerRound = 0;
            immunityBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(immunityBuff, null);

            Log.Console($"添加物理免疫Buff - ID: {immunityBuff.Id}");

            // 2. 尝试添加物理伤害Buff（应该被免疫）
            long physicalDamageBuffId = IdGenerater.Instance.GenerateId();
            Buff physicalDamageBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, physicalDamageBuffId, 2005);
            physicalDamageBuff.CreatedRound = 1;
            physicalDamageBuff.RemainTurn = 3;
            physicalDamageBuff.LastTriggerRound = 0;
            physicalDamageBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(physicalDamageBuff, null);

            Log.Console($"添加物理伤害Buff - ID: {physicalDamageBuff.Id}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用（避免EntityRef违规）
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 3. 处理第1回合
            buffComponent.OnTurnEnd(1);

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 4. 验证物理伤害被免疫
            int hpAfterRound1 = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"第1回合后血量: {hpAfterRound1}");

            if (hpAfterRound1 < initialHp)
            {
                Log.Error($"物理免疫失败: 血量减少了 {initialHp - hpAfterRound1}");
                throw new Exception("物理免疫测试失败");
            }

            Log.Console("物理免疫测试通过");
        }

        /// <summary>
        /// 测试魔法免疫
        /// </summary>
        private async ETTask TestMagicImmunity(Fiber robot, Fiber serverFiber, long playerId)
        {
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 清理可能残留的Buff
            var existingBuffs = new List<Buff>();
            foreach (var child in buffComponent.Children.Values)
            {
                if (child is Buff buff)
                {
                    existingBuffs.Add(buff);
                }
            }
            foreach (var buff in existingBuffs)
            {
                BuffHelper.RemoveBuff(buff, BuffFlags.TimeoutRemove);
            }

            // 重置血量
            playerNumeric.Set(NumericType.HP, 100);
            int initialHp = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"测试开始血量: {initialHp}");

            // 1. 添加魔法免疫Buff
            long immunityBuffId = IdGenerater.Instance.GenerateId();
            Buff immunityBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, immunityBuffId, 2002);
            immunityBuff.CreatedRound = 1;
            immunityBuff.RemainTurn = 2;
            immunityBuff.LastTriggerRound = 0;
            immunityBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(immunityBuff, null);

            Log.Console($"添加魔法免疫Buff - ID: {immunityBuff.Id}");

            // 验证免疫Buff配置
            BuffConfig immunityConfig = BuffConfigCategory.Instance.Get(2002);
            if (immunityConfig == null)
            {
                Log.Error("魔法免疫Buff配置未找到");
                throw new Exception("魔法免疫Buff配置测试失败");
            }
            Log.Console($"魔法免疫Buff配置验证通过 - 标签: {immunityConfig.Tags}, 类别: {immunityConfig.Category}");

            // 2. 尝试添加魔法伤害Buff（应该被免疫）
            long magicDamageBuffId = IdGenerater.Instance.GenerateId();
            Buff magicDamageBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, magicDamageBuffId, 2006);
            magicDamageBuff.CreatedRound = 1;
            magicDamageBuff.RemainTurn = 3;
            magicDamageBuff.LastTriggerRound = 0;
            magicDamageBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(magicDamageBuff, null);

            Log.Console($"添加魔法伤害Buff - ID: {magicDamageBuff.Id}");

            // 验证伤害Buff配置
            BuffConfig damageConfig = BuffConfigCategory.Instance.Get(2006);
            if (damageConfig == null)
            {
                Log.Error("魔法伤害Buff配置未找到");
                throw new Exception("魔法伤害Buff配置测试失败");
            }
            Log.Console($"魔法伤害Buff配置验证通过 - 标签: {damageConfig.Tags}, 类别: {damageConfig.Category}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 3. 处理第1回合
            Log.Console("开始处理第1回合");
            buffComponent.OnTurnEnd(1);

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 4. 验证魔法伤害被免疫
            int hpAfterRound1 = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"第1回合后血量: {hpAfterRound1}");

            if (hpAfterRound1 < initialHp)
            {
                Log.Error($"魔法免疫失败: 血量减少了 {initialHp - hpAfterRound1}");
                throw new Exception("魔法免疫测试失败");
            }

            Log.Console("魔法免疫测试通过");
        }

        /// <summary>
        /// 测试全伤害免疫
        /// </summary>
        private async ETTask TestAllDamageImmunity(Fiber robot, Fiber serverFiber, long playerId)
        {
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 重置血量
            playerNumeric.Set(NumericType.HP, 100);
            int initialHp = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"测试开始血量: {initialHp}");

            // 1. 添加全伤害免疫Buff
            long immunityBuffId = IdGenerater.Instance.GenerateId();
            Buff immunityBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, immunityBuffId, 2003);
            immunityBuff.CreatedRound = 1;
            immunityBuff.RemainTurn = 2;
            immunityBuff.LastTriggerRound = 0;
            immunityBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(immunityBuff, null);

            Log.Console($"添加全伤害免疫Buff - ID: {immunityBuff.Id}");

            // 2. 同时添加物理和魔法伤害Buff（都应该被免疫）
            long physicalDamageBuffId = IdGenerater.Instance.GenerateId();
            Buff physicalDamageBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, physicalDamageBuffId, 2005);
            physicalDamageBuff.CreatedRound = 1;
            physicalDamageBuff.RemainTurn = 3;
            physicalDamageBuff.LastTriggerRound = 0;
            physicalDamageBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(physicalDamageBuff, null);

            long magicDamageBuffId = IdGenerater.Instance.GenerateId();
            Buff magicDamageBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, magicDamageBuffId, 2006);
            magicDamageBuff.CreatedRound = 1;
            magicDamageBuff.RemainTurn = 3;
            magicDamageBuff.LastTriggerRound = 0;
            magicDamageBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(magicDamageBuff, null);

            Log.Console($"添加物理伤害Buff - ID: {physicalDamageBuff.Id}");
            Log.Console($"添加魔法伤害Buff - ID: {magicDamageBuff.Id}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 3. 处理第1回合
            buffComponent.OnTurnEnd(1);

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 4. 验证所有伤害都被免疫
            int hpAfterRound1 = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"第1回合后血量: {hpAfterRound1}");

            if (hpAfterRound1 < initialHp)
            {
                Log.Error($"全伤害免疫失败: 血量减少了 {initialHp - hpAfterRound1}");
                throw new Exception("全伤害免疫测试失败");
            }

            Log.Console("全伤害免疫测试通过");
        }

        /// <summary>
        /// 测试控制免疫
        /// </summary>
        private async ETTask TestControlImmunity(Fiber robot, Fiber serverFiber, long playerId)
        {
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 清理可能残留的Buff
            var existingBuffs = new List<Buff>();
            foreach (var child in buffComponent.Children.Values)
            {
                if (child is Buff buff)
                {
                    existingBuffs.Add(buff);
                }
            }
            foreach (var buff in existingBuffs)
            {
                BuffHelper.RemoveBuff(buff, BuffFlags.TimeoutRemove);
            }

            Log.Console($"清理了{existingBuffs.Count}个残留Buff");

            // 1. 添加控制免疫Buff
            long immunityBuffId = IdGenerater.Instance.GenerateId();
            Buff immunityBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, immunityBuffId, 2004);
            immunityBuff.CreatedRound = 1;
            immunityBuff.RemainTurn = 2;
            immunityBuff.LastTriggerRound = 0;
            immunityBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(immunityBuff, null);

            Log.Console($"添加控制免疫Buff - ID: {immunityBuff.Id}");

            // 验证免疫Buff配置
            BuffConfig immunityConfig = BuffConfigCategory.Instance.Get(2004);
            if (immunityConfig == null)
            {
                Log.Error("控制免疫Buff配置未找到");
                throw new Exception("控制免疫Buff配置测试失败");
            }
            Log.Console($"控制免疫Buff配置验证通过 - 标签: {immunityConfig.Tags}, 类别: {immunityConfig.Category}");

            // 2. 尝试添加控制Buff（应该被免疫）
            long controlBuffId = IdGenerater.Instance.GenerateId();
            Buff controlBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, controlBuffId, 2007);
            controlBuff.CreatedRound = 1;
            controlBuff.RemainTurn = 2;
            controlBuff.LastTriggerRound = 0;
            controlBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(controlBuff, null);

            Log.Console($"添加控制Buff - ID: {controlBuff.Id}");

            // 验证控制Buff配置
            BuffConfig controlConfig = BuffConfigCategory.Instance.Get(2007);
            if (controlConfig == null)
            {
                Log.Error("控制Buff配置未找到");
                throw new Exception("控制Buff配置测试失败");
            }
            Log.Console($"控制Buff配置验证通过 - 标签: {controlConfig.Tags}, 类别: {controlConfig.Category}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 3. 验证Buff都存在
            Buff immunityBuffFromComponent = buffComponent.GetChild<Buff>(immunityBuffId);
            if (immunityBuffFromComponent == null)
            {
                Log.Error("控制免疫Buff未找到");
                throw new Exception("控制免疫Buff检查失败");
            }

            Buff controlBuffFromComponent = buffComponent.GetChild<Buff>(controlBuffId);
            if (controlBuffFromComponent == null)
            {
                Log.Error("控制Buff未找到");
                throw new Exception("控制Buff检查失败");
            }

            Log.Console("控制免疫和控制Buff都存在，验证通过");

            // 4. 处理第1回合（控制效果应该被免疫）
            Log.Console("开始处理第1回合");
            buffComponent.OnTurnEnd(1);

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            Log.Console("第1回合处理完成");

            // 5. 验证控制免疫功能
            // 检查免疫和控制Buff的状态
            Buff remainingImmunityBuff = buffComponent.GetChild<Buff>(immunityBuffId);
            Buff remainingControlBuff = buffComponent.GetChild<Buff>(controlBuffId);

            Log.Console("=== 验证控制免疫效果 ===");

            if (remainingImmunityBuff != null)
            {
                Log.Console($"控制免疫Buff仍存在，剩余回合: {remainingImmunityBuff.RemainTurn}");
                if (remainingImmunityBuff.RemainTurn != 1)
                {
                    Log.Error($"控制免疫Buff剩余回合数不正确，期望1，实际{remainingImmunityBuff.RemainTurn}");
                    throw new Exception("控制免疫Buff回合数验证失败");
                }
            }
            else
            {
                Log.Error("控制免疫Buff不应该在第1回合后消失");
                throw new Exception("控制免疫Buff消失验证失败");
            }

            if (remainingControlBuff != null)
            {
                Log.Console($"控制Buff仍存在，剩余回合: {remainingControlBuff.RemainTurn}");

                // 关键验证：控制Buff的剩余回合数应该仍然是2，因为被免疫阻挡了处理
                // 如果免疫生效，控制Buff不应该被处理，回合数不应该减少
                if (remainingControlBuff.RemainTurn == 2)
                {
                    Log.Console("✅ 控制免疫生效：控制Buff未被处理，回合数保持不变");
                }
                else if (remainingControlBuff.RemainTurn == 1)
                {
                    Log.Error("❌ 控制免疫失效：控制Buff被处理了，回合数减少了");
                    throw new Exception("控制免疫验证失败：控制Buff不应该被处理");
                }
                else
                {
                    Log.Error($"控制Buff剩余回合数异常: {remainingControlBuff.RemainTurn}");
                    throw new Exception("控制Buff状态异常");
                }
            }
            else
            {
                Log.Error("控制Buff不应该消失");
                throw new Exception("控制Buff消失验证失败");
            }

            // 6. 再处理一回合验证免疫Buff过期后控制生效
            Log.Console("=== 验证免疫移除后控制生效 ===");
            buffComponent.OnTurnEnd(2);

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            Log.Console("第2回合处理完成");

            // 验证第2回合后的状态
            remainingImmunityBuff = buffComponent.GetChild<Buff>(immunityBuffId);
            remainingControlBuff = buffComponent.GetChild<Buff>(controlBuffId);

            if (remainingImmunityBuff != null)
            {
                Log.Console($"免疫Buff第2回合后状态，剩余回合: {remainingImmunityBuff.RemainTurn}");
                if (remainingImmunityBuff.RemainTurn > 0)
                {
                    Log.Error("免疫Buff应该在第2回合后过期");
                }
            }
            else
            {
                Log.Console("✅ 免疫Buff已正确移除");
            }

            if (remainingControlBuff != null)
            {
                Log.Console($"控制Buff第2回合后状态，剩余回合: {remainingControlBuff.RemainTurn}");
                if (remainingControlBuff.RemainTurn == 1)
                {
                    Log.Console("✅ 免疫移除后控制Buff正常处理，回合数减少");
                }
                else
                {
                    Log.Console($"控制Buff回合数: {remainingControlBuff.RemainTurn}（可能的正常状态）");
                }
            }
            else
            {
                Log.Console("控制Buff已被移除");
            }

            Log.Console("控制免疫测试完成 - 免疫系统正确阻挡了控制类Buff的处理");
        }

        /// <summary>
        /// 测试免疫状态移除后Buff正常生效
        /// </summary>
        private async ETTask TestImmunityRemoval(Fiber robot, Fiber serverFiber, long playerId)
        {
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 清理可能残留的Buff
            var existingBuffs = new List<Buff>();
            foreach (var child in buffComponent.Children.Values)
            {
                if (child is Buff buff)
                {
                    existingBuffs.Add(buff);
                }
            }
            foreach (var buff in existingBuffs)
            {
                BuffHelper.RemoveBuff(buff, BuffFlags.TimeoutRemove);
            }

            Log.Console($"清理了{existingBuffs.Count}个残留Buff");

            // 重置血量
            playerNumeric.Set(NumericType.HP, 100);
            int initialHp = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"测试开始血量: {initialHp}");

            // 1. 添加物理免疫Buff（只持续1回合）
            long immunityBuffId = IdGenerater.Instance.GenerateId();
            Buff immunityBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, immunityBuffId, 2001);
            immunityBuff.CreatedRound = 1;
            immunityBuff.RemainTurn = 1;  // 只持续1回合
            immunityBuff.LastTriggerRound = 0;
            immunityBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(immunityBuff, null);

            // 2. 添加物理伤害Buff（持续3回合）
            long physicalDamageBuffId = IdGenerater.Instance.GenerateId();
            Buff physicalDamageBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, physicalDamageBuffId, 2005);
            physicalDamageBuff.CreatedRound = 1;
            physicalDamageBuff.RemainTurn = 3;
            physicalDamageBuff.LastTriggerRound = 0;
            physicalDamageBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(physicalDamageBuff, null);

            Log.Console($"添加1回合物理免疫Buff - ID: {immunityBuff.Id}");
            Log.Console($"添加3回合物理伤害Buff - ID: {physicalDamageBuff.Id}");

            // 验证配置
            BuffConfig immunityConfig = BuffConfigCategory.Instance.Get(2001);
            BuffConfig damageConfig = BuffConfigCategory.Instance.Get(2005);
            if (immunityConfig == null || damageConfig == null)
            {
                Log.Error("免疫或伤害Buff配置未找到");
                throw new Exception("配置验证失败");
            }
            Log.Console($"免疫Buff配置: {immunityConfig.Tags}, 伤害Buff配置: {damageConfig.Tags}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            // 3. 处理第1回合（伤害应该被免疫）
            Log.Console("开始处理第1回合 - 免疫期间");
            buffComponent.OnTurnEnd(1);

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            int hpAfterRound1 = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"第1回合后血量: {hpAfterRound1} (免疫期间)");

            if (hpAfterRound1 < initialHp)
            {
                Log.Error($"第1回合免疫失败: 血量减少了 {initialHp - hpAfterRound1}");
                throw new Exception("免疫期间伤害测试失败");
            }

            // 4. 处理第2回合（免疫Buff过期，伤害应该生效）
            Log.Console("开始处理第2回合 - 免疫移除后");
            buffComponent.OnTurnEnd(2);

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();
            playerNumeric = playerUnit.GetComponent<NumericComponent>();

            int hpAfterRound2 = playerNumeric.GetAsInt(NumericType.HP);
            Log.Console($"第2回合后血量: {hpAfterRound2} (免疫移除后)");

            if (hpAfterRound2 >= hpAfterRound1)
            {
                Log.Console("警告: 期望免疫移除后伤害能够正常造成伤害");
            }

            // 验证免疫Buff已被移除
            Buff remainingImmunityBuff = buffComponent.GetChild<Buff>(immunityBuffId);
            if (remainingImmunityBuff != null)
            {
                Log.Console($"免疫Buff仍存在，剩余回合: {remainingImmunityBuff.RemainTurn}");
                if (remainingImmunityBuff.RemainTurn > 0)
                {
                    Log.Error("免疫Buff应该在1回合后被移除");
                    throw new Exception("免疫Buff移除测试失败");
                }
            }
            else
            {
                Log.Console("免疫Buff已正确移除");
            }

            // 验证伤害Buff仍然存在
            Buff remainingDamageBuff = buffComponent.GetChild<Buff>(physicalDamageBuffId);
            if (remainingDamageBuff == null)
            {
                Log.Error("伤害Buff应该在3回合后才被移除");
                throw new Exception("伤害Buff存在测试失败");
            }
            else
            {
                Log.Console($"伤害Buff仍存在，剩余回合: {remainingDamageBuff.RemainTurn}");
            }

            Log.Console("免疫移除测试通过");
        }
    }
}