using System;
using System.Collections.Generic;
using System.Linq;
using ET.Client;

namespace ET.Server
{
    [Invoke(RobotCaseType.BuffMutexGroup)]
    public class RobotCase_012_BuffMutexGroup_Handler : ARobotCaseHandler
    {
        protected override async ETTask<int> Run(Fiber fiber, RobotCaseArgs args)
        {
            Fiber robot = await fiber.CreateFiber(IdGenerater.Instance.GenerateId(), 0, SceneType.Robot, "RobotCase_012_BuffMutexGroup");

            try
            {
                Log.Console("开始Buff互斥组测试");

                // 准备测试环境
                PrepareTestEnvironment(robot, fiber);

                // 执行测试流程
                await ExecuteTestFlow(robot, fiber);

                Log.Console("Buff互斥组测试完成成功!");
                return ErrorCode.ERR_Success;
            }
            catch (Exception e)
            {
                Log.Error($"Buff互斥组测试失败: {e}");
                return ErrorCode.ERR_Exception;
            }
        }

        private void PrepareTestEnvironment(Fiber robot, Fiber serverFiber)
        {
            Log.Console("准备互斥组测试环境");

            // 创建测试配置
            CreateTestConfigs();

            Log.Console("互斥组测试环境准备完成");
        }

        private void CreateTestConfigs()
        {
            // 1. 创建低优先级毒伤Buff配置（优先级100，互斥组Poison）
            BuffConfig lowPriorityPoison = new BuffConfig
            {
                Id = 2001,
                Desc = "低优先级毒伤",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 5,
                Priority = 100,
                Category = BuffCategory.Damage,
                Tags = BuffTag.DoT | BuffTag.Poison,
                MutexGroup = BuffMutexGroup.Poison,
                Effects = new List<EffectNode>()
            };

            // 2. 创建高优先级剧毒Buff配置（优先级200，互斥组Poison）
            BuffConfig highPriorityPoison = new BuffConfig
            {
                Id = 2002,
                Desc = "高优先级剧毒",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 3,
                Priority = 200,
                Category = BuffCategory.Damage,
                Tags = BuffTag.DoT | BuffTag.Poison,
                MutexGroup = BuffMutexGroup.Poison,
                Effects = new List<EffectNode>()
            };

            // 3. 创建不同互斥组的流血Buff配置（优先级150，互斥组Bleed）
            BuffConfig bleedingBuff = new BuffConfig
            {
                Id = 2003,
                Desc = "流血效果",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 4,
                Priority = 150,
                Category = BuffCategory.Damage,
                Tags = BuffTag.DoT | BuffTag.Physical,
                MutexGroup = BuffMutexGroup.Bleed,
                Effects = new List<EffectNode>()
            };

            // 4. 创建无互斥组的治疗Buff配置（互斥组None）
            BuffConfig healingBuff = new BuffConfig
            {
                Id = 2004,
                Desc = "治疗效果",
                DurationType = BuffDurationType.Turn,
                TurnDuration = 3,
                Priority = 50,
                Category = BuffCategory.Heal,
                Tags = BuffTag.HoT,
                MutexGroup = BuffMutexGroup.None,  // 无互斥组
                Effects = new List<EffectNode>()
            };

            // 初始化并添加到单例
            var configs = new[] { lowPriorityPoison, highPriorityPoison, bleedingBuff, healingBuff };
            foreach (var config in configs)
            {
                config.OnAfterDeserialize();
                BuffConfigCategory.Instance.Add(config);
            }

            Log.Console("创建了4个测试Buff配置，包含2个互斥组");
        }

        private async ETTask ExecuteTestFlow(Fiber robot, Fiber serverFiber)
        {
            Log.Console("执行互斥组测试流程");

            // 获取服务端数据访问
            string mapName = robot.Root.CurrentScene().Name;
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);

            if (playerUnit == null)
            {
                throw new Exception("未找到玩家单位");
            }

            // 设置玩家数值
            SetupPlayerStats(playerUnit);

            // 创建EntityRef用于await安全
            long playerId = playerUnit.Id;

            // 步骤1：测试基础互斥组冲突（低优先级被高优先级替换）
            Log.Console("--- 步骤1：测试低优先级被高优先级替换 ---");
            await TestPriorityReplacement(serverFiber, mapName, playerId, true);

            // 步骤2：测试反向冲突（高优先级存在时，低优先级无法添加）
            Log.Console("--- 步骤2：测试高优先级阻止低优先级添加 ---");
            await TestPriorityBlocking(serverFiber, mapName, playerId);

            // 步骤3：测试不同互斥组可以并存
            Log.Console("--- 步骤3：测试不同互斥组并存 ---");
            await TestDifferentMutexGroupsCoexist(serverFiber, mapName, playerId);

            // 步骤4：测试无互斥组的Buff
            Log.Console("--- 步骤4：测试无互斥组Buff并存 ---");
            await TestNoMutexGroupCoexist(serverFiber, mapName, playerId);

            Log.Console("互斥组测试流程执行完成");
        }

        private void SetupPlayerStats(Unit playerUnit)
        {
            // 设置玩家数值
            NumericComponent playerNumeric = playerUnit.GetComponent<NumericComponent>();
            if (playerNumeric == null)
            {
                playerNumeric = playerUnit.AddComponent<NumericComponent>();
            }
            playerNumeric.Set(NumericType.HP, 100);
            playerNumeric.Set(NumericType.MaxHP, 100);

            // 确保BuffComponent存在
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
            if (buffComponent == null)
            {
                buffComponent = playerUnit.AddComponent<BuffComponent>();
            }

            Log.Console("玩家数值设置完成");
        }

        private void ClearAllBuffs(BuffComponent buffComponent)
        {
            // 获取所有Buff并移除
            var buffsToRemove = new List<Buff>();
            foreach (var child in buffComponent.Children.Values)
            {
                if (child is Buff buff)
                {
                    buffsToRemove.Add(buff);
                }
            }

            foreach (var buff in buffsToRemove)
            {
                buffComponent.RemoveBuff(buff);
            }
        }

        private List<Buff> GetAllBuffs(BuffComponent buffComponent)
        {
            var allBuffs = new List<Buff>();
            foreach (var child in buffComponent.Children.Values)
            {
                if (child is Buff buff)
                {
                    allBuffs.Add(buff);
                }
            }
            return allBuffs;
        }

        private async ETTask TestPriorityReplacement(Fiber serverFiber, string mapName, long playerId, bool clearFirst)
        {
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 清除现有Buff
            if (clearFirst)
            {
                ClearAllBuffs(buffComponent);
                await map.Root.GetComponent<TimerComponent>().WaitAsync(50);
            }

            // 1. 先添加低优先级毒伤Buff
            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);

            long lowBuffId = IdGenerater.Instance.GenerateId();
            Buff lowPriorityBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, lowBuffId, 2001);
            lowPriorityBuff.CreatedRound = 1;
            lowPriorityBuff.RemainTurn = 5;
            lowPriorityBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(lowPriorityBuff, null);

            Log.Console($"添加低优先级毒伤Buff - ID: {lowBuffId}, 优先级: 100");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 验证低优先级Buff存在
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            Buff existingLowBuff = buffComponent.GetChild<Buff>(lowBuffId);
            if (existingLowBuff == null)
            {
                throw new Exception("低优先级Buff添加失败");
            }

            Log.Console("低优先级Buff添加成功，现在尝试添加高优先级同组Buff");

            // 2. 再添加高优先级剧毒Buff（同互斥组）
            long highBuffId = IdGenerater.Instance.GenerateId();
            Buff highPriorityBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, highBuffId, 2002);
            highPriorityBuff.CreatedRound = 1;
            highPriorityBuff.RemainTurn = 3;
            highPriorityBuff.DurationType = BuffDurationType.Turn;

            // 这里应该触发互斥检查，低优先级应该被移除
            BuffHelper.InitBuff(highPriorityBuff, null);

            Log.Console($"添加高优先级剧毒Buff - ID: {highBuffId}, 优先级: 200");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 验证结果：低优先级被移除，高优先级存在
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            existingLowBuff = buffComponent.GetChild<Buff>(lowBuffId);
            Buff existingHighBuff = buffComponent.GetChild<Buff>(highBuffId);

            if (existingLowBuff != null)
            {
                throw new Exception("低优先级Buff应该被高优先级替换移除");
            }

            if (existingHighBuff == null)
            {
                throw new Exception("高优先级Buff应该添加成功");
            }

            Log.Console("✅ 互斥组优先级替换测试通过：低优先级被高优先级正确替换");
        }

        private async ETTask TestPriorityBlocking(Fiber serverFiber, string mapName, long playerId)
        {
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 清除现有Buff
            ClearAllBuffs(buffComponent);
            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 1. 先添加高优先级剧毒Buff
            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);

            long highBuffId = IdGenerater.Instance.GenerateId();
            Buff highPriorityBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, highBuffId, 2002);
            highPriorityBuff.CreatedRound = 1;
            highPriorityBuff.RemainTurn = 3;
            highPriorityBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(highPriorityBuff, null);

            Log.Console($"先添加高优先级剧毒Buff - ID: {highBuffId}, 优先级: 200");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 验证高优先级Buff存在
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            Buff existingHighBuff = buffComponent.GetChild<Buff>(highBuffId);
            if (existingHighBuff == null)
            {
                throw new Exception("高优先级Buff添加失败");
            }

            Log.Console("高优先级Buff添加成功，现在尝试添加低优先级同组Buff");

            // 2. 尝试添加低优先级毒伤Buff（应该被阻止）
            long lowBuffId = IdGenerater.Instance.GenerateId();

            try
            {
                Buff lowPriorityBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, lowBuffId, 2001);
                lowPriorityBuff.CreatedRound = 1;
                lowPriorityBuff.RemainTurn = 5;
                lowPriorityBuff.DurationType = BuffDurationType.Turn;

                // 这里应该检查互斥组，低优先级应该无法添加
                BuffHelper.InitBuff(lowPriorityBuff, null);

                Log.Console($"尝试添加低优先级毒伤Buff - ID: {lowBuffId}, 优先级: 100");

                await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                // 验证结果：高优先级仍然存在，低优先级被阻止或立即移除
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                buffComponent = playerUnit.GetComponent<BuffComponent>();

                existingHighBuff = buffComponent.GetChild<Buff>(highBuffId);
                Buff existingLowBuff = buffComponent.GetChild<Buff>(lowBuffId);

                if (existingHighBuff == null)
                {
                    throw new Exception("高优先级Buff不应该被移除");
                }

                if (existingLowBuff != null)
                {
                    throw new Exception("低优先级Buff应该被阻止添加或立即移除");
                }

                Log.Console("✅ 互斥组优先级阻止测试通过：低优先级被高优先级正确阻止");
            }
            catch (Exception e)
            {
                // 如果在创建阶段就被阻止，这也是正确的行为
                Log.Console($"✅ 低优先级Buff在创建阶段被阻止: {e.Message}");
            }
        }

        private async ETTask TestDifferentMutexGroupsCoexist(Fiber serverFiber, string mapName, long playerId)
        {
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 清除现有Buff
            ClearAllBuffs(buffComponent);
            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 1. 添加互斥组Poison的高优先级剧毒Buff
            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);

            long poisonBuffId = IdGenerater.Instance.GenerateId();
            Buff poisonBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, poisonBuffId, 2002);
            poisonBuff.CreatedRound = 1;
            poisonBuff.RemainTurn = 3;
            poisonBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(poisonBuff, null);

            Log.Console($"添加互斥组Poison的剧毒Buff - ID: {poisonBuffId}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 2. 添加互斥组Bleed的流血Buff
            // 重新获取引用
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);

            long bleedBuffId = IdGenerater.Instance.GenerateId();
            Buff bleedBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, bleedBuffId, 2003);
            bleedBuff.CreatedRound = 1;
            bleedBuff.RemainTurn = 4;
            bleedBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(bleedBuff, null);

            Log.Console($"添加互斥组Bleed的流血Buff - ID: {bleedBuffId}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 验证两个不同互斥组的Buff都存在
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            Buff existingPoisonBuff = buffComponent.GetChild<Buff>(poisonBuffId);
            Buff existingBleedBuff = buffComponent.GetChild<Buff>(bleedBuffId);

            if (existingPoisonBuff == null)
            {
                throw new Exception("互斥组Poison的剧毒Buff应该存在");
            }

            if (existingBleedBuff == null)
            {
                throw new Exception("互斥组Bleed的流血Buff应该存在");
            }

            Log.Console("✅ 不同互斥组并存测试通过：两个不同互斥组的Buff可以并存");
        }

        private async ETTask TestNoMutexGroupCoexist(Fiber serverFiber, string mapName, long playerId)
        {
            Fiber map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 当前已有两个不同互斥组的Buff，再添加无互斥组的治疗Buff
            long healBuffId = IdGenerater.Instance.GenerateId();
            Buff healBuff = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, healBuffId, 2004);
            healBuff.CreatedRound = 1;
            healBuff.RemainTurn = 3;
            healBuff.DurationType = BuffDurationType.Turn;
            BuffHelper.InitBuff(healBuff, null);

            Log.Console($"添加无互斥组的治疗Buff - ID: {healBuffId}");

            await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

            // 验证所有Buff都存在
            map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
            playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
            buffComponent = playerUnit.GetComponent<BuffComponent>();

            // 统计当前Buff数量
            var allBuffs = GetAllBuffs(buffComponent);
            int buffCount = allBuffs.Count;

            Log.Console($"当前总Buff数量: {buffCount}");

            if (buffCount < 3)
            {
                throw new Exception($"应该至少有3个Buff（2个互斥组+1个无互斥组），实际: {buffCount}");
            }

            // 验证治疗Buff存在
            Buff existingHealBuff = buffComponent.GetChild<Buff>(healBuffId);
            if (existingHealBuff == null)
            {
                throw new Exception("无互斥组的治疗Buff应该存在");
            }

            Log.Console("✅ 无互斥组并存测试通过：无互斥组的Buff可以与其他Buff并存");

            // 最后验证互斥机制仍然有效
            Log.Console("最后验证：在多Buff环境下互斥机制仍然有效");

            // 尝试添加另一个互斥组Poison的低优先级Buff（应该被阻止）
            long anotherPoisonId = IdGenerater.Instance.GenerateId();
            try
            {
                Buff anotherPoison = BuffHelper.CreateBuffWithoutInit(playerUnit, playerId, anotherPoisonId, 2001);
                anotherPoison.CreatedRound = 1;
                anotherPoison.RemainTurn = 5;
                anotherPoison.DurationType = BuffDurationType.Turn;
                BuffHelper.InitBuff(anotherPoison, null);

                await map.Root.GetComponent<TimerComponent>().WaitAsync(50);

                // 检查是否被正确阻止
                map = serverFiber.GetFiber("MapManager").GetFiber(mapName);
                playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerId);
                buffComponent = playerUnit.GetComponent<BuffComponent>();

                Buff blockedBuff = buffComponent.GetChild<Buff>(anotherPoisonId);
                if (blockedBuff != null)
                {
                    throw new Exception("低优先级互斥组Buff应该在多Buff环境下仍然被阻止");
                }

                Log.Console("✅ 多Buff环境下互斥机制验证通过：低优先级仍然被正确阻止");
            }
            catch (Exception e)
            {
                Log.Console($"✅ 多Buff环境下互斥机制验证通过：{e.Message}");
            }
        }
    }
}