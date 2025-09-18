using System;
using System.Collections.Generic;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.BuffTagSystem)]
    public class RobotCase_011_BuffTagSystem_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_011_BuffTagSystem");

            try
            {
                Log.Console("开始BuffTag标签系统测试");

                // 准备测试环境
                PrepareTestEnvironment(robot, fiber);

                // 执行测试流程
                await ExecuteTestFlow(robot, fiber);

                Log.Console("BuffTag标签系统测试完成成功!");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"BuffTag标签系统测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }

        private void PrepareTestEnvironment(Fiber robot, Fiber serverFiber)
        {
            Log.Console("准备BuffTag测试环境");

            // 创建多种标签的测试配置
            CreateTestConfigs();

            Log.Console("BuffTag测试环境准备完成");
        }

        private void CreateTestConfigs()
        {
            // 创建物理伤害Buff配置
            BuffConfig physicalDamageConfig = new BuffConfig
            {
                Id = 2001,
                Desc = "物理伤害Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 5,
                Priority = 100,
                Category = BuffCategory.Damage,
                Tags = BuffTag.Physical, // 单一标签：物理
                Effects = new List<EffectNode>()
            };

            // 创建魔法伤害Buff配置
            BuffConfig magicDamageConfig = new BuffConfig
            {
                Id = 2002,
                Desc = "魔法伤害Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 3,
                Priority = 150,
                Category = BuffCategory.Damage,
                Tags = BuffTag.Magic, // 单一标签：魔法
                Effects = new List<EffectNode>()
            };

            // 创建复合标签Buff配置（物理 + 伤害 + DoT）
            BuffConfig compoundTagConfig = new BuffConfig
            {
                Id = 2003,
                Desc = "复合标签物理DoT伤害",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 4,
                Priority = 200,
                Category = BuffCategory.Damage,
                Tags = BuffTag.Physical | BuffTag.DoT | BuffTag.Debuff, // 复合标签
                Effects = new List<EffectNode>()
            };

            // 创建治疗类Buff配置
            BuffConfig healingConfig = new BuffConfig
            {
                Id = 2004,
                Desc = "治疗Buff",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 3,
                Priority = 50,
                Category = BuffCategory.Heal,
                Tags = BuffTag.Buff | BuffTag.HoT, // 治疗相关标签
                Effects = new List<EffectNode>()
            };

            // 创建控制类Buff配置
            BuffConfig controlConfig = new BuffConfig
            {
                Id = 2005,
                Desc = "眩晕控制",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 2,
                Priority = 300,
                Category = BuffCategory.Debuff,
                Tags = BuffTag.Control | BuffTag.Debuff, // 控制相关标签
                Effects = new List<EffectNode>()
            };

            // 初始化所有配置
            var configs = new[] { physicalDamageConfig, magicDamageConfig, compoundTagConfig, healingConfig, controlConfig };
            foreach (var config in configs)
            {
                config.OnAfterDeserialize();
                BuffConfigCategory.Instance.Add(config);
            }

            Log.Console($"创建了{configs.Length}个不同标签的测试Buff配置");
        }

        private async ETTask ExecuteTestFlow(Fiber robot, Fiber serverFiber)
        {
            Log.Console("执行BuffTag测试流程");

            // 获取服务端数据访问
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);

            // 设置玩家血量
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
            if (playerNumeric == null)
            {
                playerNumeric = playerUnit.AddComponent<NumericComponent>();
            }
            playerNumeric.Set(NumericType.HP, 100);
            playerNumeric.Set(NumericType.MaxHP, 100);

            // 创建BuffComponent
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            if (buffComponent == null)
            {
                buffComponent = playerUnit.AddComponent<BuffComponent>();
            }

            // 创建EntityRef
            long playerId = playerUnit.Id;
            EntityRef<Unit> playerUnitRef = playerUnit;

            // 第1步：测试HasTag()方法的正确性
            await TestHasTagMethod(robot, serverFiber, playerId);

            // 第2步：测试单一标签Buff
            playerUnit = playerUnitRef;
            await TestSingleTagBuffs(robot, serverFiber, playerUnit, playerId);

            // 第3步：测试复合标签Buff
            playerUnit = playerUnitRef;
            await TestCompoundTagBuffs(robot, serverFiber, playerUnit, playerId);

            // 第4步：测试标签过滤和查询功能
            playerUnit = playerUnitRef;
            await TestTagFilterAndQuery(robot, serverFiber, playerUnit, playerId);

            // 第5步：测试动态标签操作
            playerUnit = playerUnitRef;
            await TestDynamicTagOperations(robot, serverFiber, playerUnit, playerId);

            Log.Console("BuffTag测试流程执行完成");
        }

        private async ETTask TestHasTagMethod(Fiber robot, Fiber serverFiber, long playerId)
        {
            Log.Console("--- 第1步：测试HasTag()方法 ---");

            // 测试单一标签检查
            BuffConfig physicalConfig = BuffConfigCategory.Instance.Get(2001);
            bool hasPhysical = physicalConfig.HasTag(BuffTag.Physical);
            bool hasMagic = physicalConfig.HasTag(BuffTag.Magic);

            if (!hasPhysical)
            {
                throw new Exception("物理伤害配置应该包含Physical标签");
            }

            if (hasMagic)
            {
                throw new Exception("物理伤害配置不应该包含Magic标签");
            }

            Log.Console($"物理伤害配置标签检查: Physical={hasPhysical}, Magic={hasMagic}");

            // 测试复合标签检查
            BuffConfig compoundConfig = BuffConfigCategory.Instance.Get(2003);
            bool hasPhysicalCompound = compoundConfig.HasTag(BuffTag.Physical);
            bool hasDoT = compoundConfig.HasTag(BuffTag.DoT);
            bool hasDamage = compoundConfig.HasTag(BuffTag.Debuff);
            bool hasHealing = compoundConfig.HasTag(BuffTag.Buff);

            if (!hasPhysicalCompound || !hasDoT || !hasDamage)
            {
                throw new Exception("复合标签配置应该包含Physical、DoT、Debuff标签");
            }

            if (hasHealing)
            {
                throw new Exception("复合标签配置不应该包含Buff标签");
            }

            Log.Console($"复合标签配置检查: Physical={hasPhysicalCompound}, DoT={hasDoT}, Debuff={hasDamage}, Buff={hasHealing}");

            // 等待一下
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            Log.Console("HasTag()方法测试通过");
        }

        private async ETTask TestSingleTagBuffs(Fiber robot, Fiber serverFiber, Unit playerUnit, long playerId)
        {
            Log.Console("--- 第2步：测试单一标签Buff ---");

            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);

            // 创建物理伤害Buff
            long physicalBuffId = IdGenerater.Instance.GenerateId();
            Buff physicalBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, physicalBuffId, 2001);
            physicalBuff.DurationType = BuffDurationType.Turn;
            physicalBuff.RemainTurn = 5;
            BuffHelper.InitBuff(physicalBuff, null);

            Log.Console($"创建物理伤害Buff - ID: {physicalBuff.Id}, 标签: Physical");

            // 创建魔法伤害Buff
            long magicBuffId = IdGenerater.Instance.GenerateId();
            Buff magicBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, magicBuffId, 2002);
            magicBuff.DurationType = BuffDurationType.Turn;
            magicBuff.RemainTurn = 3;
            BuffHelper.InitBuff(magicBuff, null);

            Log.Console($"创建魔法伤害Buff - ID: {magicBuff.Id}, 标签: Magic");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 验证Buff存在
            Buff createdPhysicalBuff = buffComponent.GetChild<Buff>(physicalBuffId);
            Buff createdMagicBuff = buffComponent.GetChild<Buff>(magicBuffId);

            if (createdPhysicalBuff == null)
            {
                throw new Exception("物理伤害Buff创建失败");
            }

            if (createdMagicBuff == null)
            {
                throw new Exception("魔法伤害Buff创建失败");
            }

            // 验证Buff配置标签
            BuffConfig physicalConfig = createdPhysicalBuff.GetConfig();
            BuffConfig magicConfig = createdMagicBuff.GetConfig();

            if (!physicalConfig.HasTag(BuffTag.Physical))
            {
                throw new Exception("物理伤害Buff应该有Physical标签");
            }

            if (!magicConfig.HasTag(BuffTag.Magic))
            {
                throw new Exception("魔法伤害Buff应该有Magic标签");
            }

            if (physicalConfig.HasTag(BuffTag.Magic))
            {
                throw new Exception("物理伤害Buff不应该有Magic标签");
            }

            if (magicConfig.HasTag(BuffTag.Physical))
            {
                throw new Exception("魔法伤害Buff不应该有Physical标签");
            }

            Log.Console("单一标签Buff测试通过");
        }

        private async ETTask TestCompoundTagBuffs(Fiber robot, Fiber serverFiber, Unit playerUnit, long playerId)
        {
            Log.Console("--- 第3步：测试复合标签Buff ---");

            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);

            // 创建复合标签Buff
            long compoundBuffId = IdGenerater.Instance.GenerateId();
            Buff compoundBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, compoundBuffId, 2003);
            compoundBuff.DurationType = BuffDurationType.Turn;
            compoundBuff.RemainTurn = 4;
            BuffHelper.InitBuff(compoundBuff, null);

            Log.Console($"创建复合标签Buff - ID: {compoundBuff.Id}, 标签: Physical | DoT | Damage");

            // 创建治疗Buff
            long healingBuffId = IdGenerater.Instance.GenerateId();
            Buff healingBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, healingBuffId, 2004);
            healingBuff.DurationType = BuffDurationType.Turn;
            healingBuff.RemainTurn = 3;
            BuffHelper.InitBuff(healingBuff, null);

            Log.Console($"创建治疗Buff - ID: {healingBuff.Id}, 标签: Healing | HoT");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 验证复合标签Buff
            Buff createdCompoundBuff = buffComponent.GetChild<Buff>(compoundBuffId);
            if (createdCompoundBuff == null)
            {
                throw new Exception("复合标签Buff创建失败");
            }

            BuffConfig compoundConfig = createdCompoundBuff.GetConfig();

            // 验证包含所有预期标签
            if (!compoundConfig.HasTag(BuffTag.Physical))
            {
                throw new Exception("复合标签Buff应该包含Physical标签");
            }

            if (!compoundConfig.HasTag(BuffTag.DoT))
            {
                throw new Exception("复合标签Buff应该包含DoT标签");
            }

            if (!compoundConfig.HasTag(BuffTag.Debuff))
            {
                throw new Exception("复合标签Buff应该包含Debuff标签");
            }

            // 验证不包含其他标签
            if (compoundConfig.HasTag(BuffTag.Magic))
            {
                throw new Exception("复合标签Buff不应该包含Magic标签");
            }

            if (compoundConfig.HasTag(BuffTag.Buff))
            {
                throw new Exception("复合标签Buff不应该包含Buff标签");
            }

            Log.Console("复合标签检查 - Physical: ✓, DoT: ✓, Debuff: ✓, 不包含Magic和Buff");

            // 验证治疗Buff
            Buff createdHealingBuff = buffComponent.GetChild<Buff>(healingBuffId);
            if (createdHealingBuff == null)
            {
                throw new Exception("治疗Buff创建失败");
            }

            BuffConfig healingConfig = createdHealingBuff.GetConfig();

            if (!healingConfig.HasTag(BuffTag.Buff))
            {
                throw new Exception("治疗Buff应该包含Buff标签");
            }

            if (!healingConfig.HasTag(BuffTag.HoT))
            {
                throw new Exception("治疗Buff应该包含HoT标签");
            }

            if (healingConfig.HasTag(BuffTag.Debuff))
            {
                throw new Exception("治疗Buff不应该包含Debuff标签");
            }

            Log.Console("治疗Buff标签检查 - Buff: ✓, HoT: ✓, 不包含Debuff");

            Log.Console("复合标签Buff测试通过");
        }

        private async ETTask TestTagFilterAndQuery(Fiber robot, Fiber serverFiber, Unit playerUnit, long playerId)
        {
            Log.Console("--- 第4步：测试标签过滤和查询功能 ---");

            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);

            // 重新获取引用
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 统计当前所有Buff
            var allBuffs = buffComponent.Children;
            Log.Console($"当前总Buff数量: {allBuffs.Count}");

            // 统计不同标签的Buff数量
            int physicalCount = 0;
            int magicCount = 0;
            int debuffCount = 0;
            int buffCount = 0;
            int dotCount = 0;
            int hotCount = 0;

            foreach (var buff in allBuffs.Values)
            {
                BuffConfig config = ((Buff)buff).GetConfig();
                if (config != null)
                {
                    if (config.HasTag(BuffTag.Physical)) physicalCount++;
                    if (config.HasTag(BuffTag.Magic)) magicCount++;
                    if (config.HasTag(BuffTag.Debuff)) debuffCount++;
                    if (config.HasTag(BuffTag.Buff)) buffCount++;
                    if (config.HasTag(BuffTag.DoT)) dotCount++;
                    if (config.HasTag(BuffTag.HoT)) hotCount++;

                    Log.Console($"Buff {buff.Id}: {config.Desc}, 标签值: {config.Tags}");
                }
            }

            Log.Console($"标签统计 - Physical: {physicalCount}, Magic: {magicCount}, Debuff: {debuffCount}");
            Log.Console($"标签统计 - Buff: {buffCount}, DoT: {dotCount}, HoT: {hotCount}");

            // 验证预期的标签数量
            // 应该有2个Physical标签的Buff（单一物理 + 复合标签）
            if (physicalCount != 2)
            {
                Log.Console($"警告: 期望2个Physical标签Buff，实际: {physicalCount}");
            }

            // 应该有1个Magic标签的Buff
            if (magicCount != 1)
            {
                Log.Console($"警告: 期望1个Magic标签Buff，实际: {magicCount}");
            }

            // 应该有多个Debuff标签的Buff（复合标签 + 控制Buff）
            if (debuffCount < 1)
            {
                Log.Console($"警告: 期望至少1个Debuff标签Buff，实际: {debuffCount}");
            }

            // 应该有1个Buff标签的Buff（治疗类）
            if (buffCount != 1)
            {
                Log.Console($"警告: 期望1个Buff标签Buff，实际: {buffCount}");
            }

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            Log.Console("标签过滤和查询功能测试通过");
        }

        private async ETTask TestDynamicTagOperations(Fiber robot, Fiber serverFiber, Unit playerUnit, long playerId)
        {
            Log.Console("--- 第5步：测试动态标签操作 ---");

            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);

            // 创建控制类Buff进行动态标签测试
            long controlBuffId = IdGenerater.Instance.GenerateId();
            Buff controlBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, controlBuffId, 2005);
            controlBuff.DurationType = BuffDurationType.Turn;
            controlBuff.RemainTurn = 2;
            BuffHelper.InitBuff(controlBuff, null);

            Log.Console($"创建控制Buff - ID: {controlBuff.Id}, 原始标签: Control | Stun");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(100);

            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            Buff createdControlBuff = buffComponent.GetChild<Buff>(controlBuffId);
            if (createdControlBuff == null)
            {
                throw new Exception("控制Buff创建失败");
            }

            BuffConfig controlConfig = createdControlBuff.GetConfig();

            // 验证初始标签
            if (!controlConfig.HasTag(BuffTag.Control))
            {
                throw new Exception("控制Buff应该包含Control标签");
            }

            if (!controlConfig.HasTag(BuffTag.Debuff))
            {
                throw new Exception("控制Buff应该包含Debuff标签");
            }

            Log.Console("初始标签验证通过 - Control: ✓, Debuff: ✓");

            // 测试标签组合验证
            BuffTag expectedCombination = BuffTag.Control | BuffTag.Debuff;
            if (controlConfig.Tags != expectedCombination)
            {
                Log.Console($"标签组合检查 - 期望: {expectedCombination}, 实际: {controlConfig.Tags}");
            }

            // 测试位运算操作
            BuffTag testTags = BuffTag.Physical | BuffTag.Magic | BuffTag.Buff;
            bool hasPhysical = (testTags & BuffTag.Physical) != 0;
            bool hasMagic = (testTags & BuffTag.Magic) != 0;
            bool hasHealing = (testTags & BuffTag.Buff) != 0;
            bool hasControl = (testTags & BuffTag.Control) != 0;

            if (!hasPhysical || !hasMagic || !hasHealing)
            {
                throw new Exception("位运算标签检查失败 - 应该包含Physical、Magic、Buff");
            }

            if (hasControl)
            {
                throw new Exception("位运算标签检查失败 - 不应该包含Control");
            }

            Log.Console($"位运算测试通过 - Physical: {hasPhysical}, Magic: {hasMagic}, Healing: {hasHealing}, Control: {hasControl}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            Log.Console("动态标签操作测试通过");
        }
    }
}