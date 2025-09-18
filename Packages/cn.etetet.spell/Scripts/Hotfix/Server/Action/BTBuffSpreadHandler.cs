using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ET.Server
{
    /// <summary>
    /// Buff传播节点处理器
    /// </summary>
    public class BTBuffSpreadHandler : ABTHandler<BTBuffSpread>
    {
        protected override int Run(BTBuffSpread node, BTEnv env)
        {
            Buff sourceBuff = env.GetEntity<Buff>(node.SourceBuff);
            Unit sourceUnit = env.GetEntity<Unit>(node.SourceUnit);

            if (sourceBuff == null || sourceUnit == null)
            {
                Log.Error("BTBuffSpread: SourceBuff或SourceUnit为空");
                return 1;
            }

            // 获取地图上的所有单位
            Scene scene = sourceUnit.Scene();
            UnitComponent unitComponent = scene.GetComponent<UnitComponent>();
            if (unitComponent == null)
            {
                Log.Error("BTBuffSpread: 场景没有UnitComponent");
                return 1;
            }

            List<Unit> candidateTargets = new List<Unit>();

            // 遍历所有单位，找到符合条件的传播目标
            foreach (var kv in unitComponent.Children)
            {
                Unit unit = kv.Value as Unit;
                if (unit == null || unit.Id == sourceUnit.Id) continue; // 跳过自己

                // 检查目标类型（敌人/友军/全部）
                if (!IsValidSpreadTarget(sourceUnit, unit, node.SpreadTarget)) continue;

                // 检查目标是否已经有相同的Buff
                BuffComponent targetBuffComponent = unit.GetComponent<BuffComponent>();
                if (targetBuffComponent != null)
                {
                    // 检查是否已经有相同配置ID的Buff
                    bool hasSameBuff = false;
                    foreach (var buffKv in targetBuffComponent.Children)
                    {
                        Buff buff = buffKv.Value as Buff;
                        if (buff != null && buff.ConfigId == sourceBuff.ConfigId)
                        {
                            hasSameBuff = true;
                            break;
                        }
                    }
                    if (hasSameBuff) continue; // 已经有相同Buff，跳过
                }

                candidateTargets.Add(unit);
            }

            // 随机选择目标（最多MaxTargets个）
            List<Unit> selectedTargets = SelectTargets(candidateTargets, node.MaxTargets);

            int successCount = 0;
            foreach (Unit target in selectedTargets)
            {
                // 概率检查（SpreadChance为0-100的整数）
                System.Random random = new System.Random();
                int randomValue = random.Next(1, 101); // 生成1-100的随机数
                if (randomValue > node.SpreadChance) continue;

                // 传播Buff（不减少层数）
                if (SpreadBuffToTarget(sourceBuff, target))
                {
                    successCount++;
                    Log.Debug($"BTBuffSpread: Buff传播到目标 {target.Id}");
                }
            }

            Log.Debug($"BTBuffSpread: 成功传播到 {successCount}/{selectedTargets.Count} 个目标");
            return 0;
        }

        /// <summary>
        /// 检查目标是否符合传播条件
        /// </summary>
        private bool IsValidSpreadTarget(Unit sourceUnit, Unit targetUnit, int spreadTarget)
        {
            if (sourceUnit.Id == targetUnit.Id) return false; // 不能传播给自己

            switch (spreadTarget)
            {
                case 1: // 敌人
                    // 敌人：不是友军的单位
                    return !IsFriendlyUnit(targetUnit);

                case 2: // 友军
                    // 友军：UnitType是玩家或宠物
                    return IsFriendlyUnit(targetUnit);

                case 3: // 全部
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 检查是否为友军单位（玩家或宠物）
        /// </summary>
        private bool IsFriendlyUnit(Unit unit)
        {
            // 检查UnitType，玩家或宠物视为友军
            return unit.UnitType == UnitType.Player || unit.UnitType == UnitType.Pet;
        }

        /// <summary>
        /// 选择传播目标
        /// </summary>
        private List<Unit> SelectTargets(List<Unit> candidates, int maxTargets)
        {
            if (candidates.Count <= maxTargets)
                return candidates;

            // 随机洗牌并选择前maxTargets个
            List<Unit> result = new List<Unit>(candidates);
            System.Random random = new System.Random();
            for (int i = 0; i < result.Count; i++)
            {
                int randomIndex = random.Next(i, result.Count);
                Unit temp = result[i];
                result[i] = result[randomIndex];
                result[randomIndex] = temp;
            }

            result.RemoveRange(maxTargets, result.Count - maxTargets);
            return result;
        }

        /// <summary>
        /// 将Buff传播到目标
        /// </summary>
        private bool SpreadBuffToTarget(Buff sourceBuff, Unit target)
        {
            try
            {
                // 确保目标有BuffComponent
                BuffComponent targetBuffComponent = target.GetComponent<BuffComponent>();
                if (targetBuffComponent == null)
                {
                    targetBuffComponent = target.AddComponent<BuffComponent>();
                }

                // 创建新的Buff（保持相同层数）
                long newBuffId = IdGenerater.Instance.GenerateId();
                long casterId = sourceBuff.Parent?.Id ?? target.Id; // 使用源 Buff 的拥有者作为施法者
                Buff newBuff = BuffHelper.CreateBuffWithoutInit(target, casterId, newBuffId, sourceBuff.ConfigId);

                // 复制关键属性
                newBuff.CreatedRound = sourceBuff.CreatedRound;
                newBuff.RemainTurn = sourceBuff.RemainTurn;
                newBuff.LastTriggerRound = sourceBuff.LastTriggerRound;
                newBuff.DurationType = sourceBuff.DurationType;
                newBuff.Stack = sourceBuff.Stack; // 不减少层数，保持原始层数

                // 初始化Buff
                BuffHelper.InitBuff(newBuff, null);

                return true;
            }
            catch (Exception e)
            {
                Log.Error($"BTBuffSpread传播Buff失败: {e}");
                return false;
            }
        }
    }
}