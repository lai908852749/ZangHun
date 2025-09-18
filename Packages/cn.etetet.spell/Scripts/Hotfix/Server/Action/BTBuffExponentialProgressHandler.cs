using System;

namespace ET.Server
{
    /// <summary>
    /// Buff指数增长节点处理器
    /// </summary>
    public class BTBuffExponentialProgressHandler : ABTHandler<BTBuffExponentialProgress>
    {
        protected override int Run(BTBuffExponentialProgress node, BTEnv env)
        {
            Buff buff = env.GetEntity<Buff>(node.Buff);
            Unit unit = env.GetEntity<Unit>(node.Unit);

            if (buff == null || unit == null)
            {
                Log.Error("BTBuffExponentialProgress: Buff或Unit为空");
                return 1;
            }

            // 计算当前触发次数（基于LastTriggerRound）
            int triggerCount = buff.LastTriggerRound - buff.CreatedRound + 1;
            if (triggerCount < 1)
            {
                triggerCount = 1;
            }

            // 计算指数增长值
            // 公式：BaseValue * (GrowthRate ^ (triggerCount - 1))
            float currentValueFloat = node.BaseValue * (float)Math.Pow(node.GrowthRate, triggerCount - 1);
            int currentValue = (int)Math.Round(currentValueFloat);

            // 应用最大值限制
            if (node.MaxValue > 0 && currentValue > node.MaxValue)
            {
                currentValue = node.MaxValue;
            }

            // 确保最小值为1（避免0值无效果）
            if (currentValue < 1)
            {
                currentValue = 1;
            }

            // 输出当前计算值到环境变量
            if (!string.IsNullOrEmpty(node.CurrentValue))
            {
                env.AddStruct(node.CurrentValue, currentValue);
            }

            // 如果指定了数值类型，直接应用到Unit
            if (node.NumericType != 0)
            {
                NumericComponent numericComponent = unit.GetComponent<NumericComponent>();
                if (numericComponent != null)
                {
                    // 使用IsPositive字段判断是否为伤害
                    bool isDamage = !node.IsPositive;
                    int targetType = node.NumericType;

                    if (targetType == NumericType.HP)
                    {
                        if (isDamage)
                        {
                            // 伤害处理
                            long oldValue = numericComponent.GetAsLong(NumericType.HP);
                            long newValue = oldValue - currentValue; // 减少HP
                            if (newValue < 0) newValue = 0; // 不能低于0
                            numericComponent.Set(NumericType.HP, newValue);
                            Log.Debug($"BTBuffExponentialProgress伤害: {currentValue}点，当前HP: {numericComponent.GetAsInt(NumericType.HP)}");
                        }
                        else
                        {
                            // 治疗处理
                            int currentHp = numericComponent.GetAsInt(NumericType.HP);
                            int maxHp = numericComponent.GetAsInt(NumericType.MaxHP);
                            int healAmount = Math.Min(currentValue, maxHp - currentHp);
                            if (healAmount > 0)
                            {
                                long oldValue = numericComponent.GetAsLong(NumericType.HP);
                                numericComponent.Set(NumericType.HP, oldValue + healAmount);
                                Log.Debug($"BTBuffExponentialProgress治疗: {healAmount}点，当前HP: {numericComponent.GetAsInt(NumericType.HP)}");
                            }
                        }
                    }
                    else
                    {
                        // 其他数值类型直接修改
                        long oldValue = numericComponent.GetAsLong(targetType);
                        long changeValue = isDamage ? -currentValue : currentValue;
                        numericComponent.Set(targetType, oldValue + changeValue);
                        Log.Debug($"BTBuffExponentialProgress修改数值类型{targetType}: {(isDamage ? "-" : "+")}{currentValue}");
                    }
                }
            }

            Log.Debug($"BTBuffExponentialProgress: Buff={buff.Id}, 触发次数={triggerCount}, 增长倍率={node.GrowthRate}, 基础值={node.BaseValue}, 计算值={currentValue}");
            Log.Console($"指数增长调试: 创建回合={buff.CreatedRound}, 最后触发回合={buff.LastTriggerRound}, 触发次数={triggerCount}, 计算公式={node.BaseValue}*({node.GrowthRate}^{triggerCount-1})={currentValueFloat:F2}≈{currentValue}");

            return 0;
        }
    }
}