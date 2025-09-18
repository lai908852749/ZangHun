using System;
using System.Collections.Generic;
using System.Linq;

namespace ET.Server
{
    /// <summary>
    /// Buff回合制处理系统（服务端专用）
    /// </summary>
    public static class BuffTurnBasedSystem
    {
        #region 回合制核心方法

        /// <summary>
        /// 回合开始时的Buff处理（优化版）
        /// </summary>
        public static void OnTurnStart(this BuffComponent self, int currentRound)
        {
            // 第1步：检查免疫状态（确保免疫在回合开始时也生效）
            ImmunityStatus immunity = self.CheckImmunityStatus();

            // 第2步：收集需要处理的Buff
            var buffsToProcess = self.GetTurnStartBuffs();

            // 第3步：处理Buff（传入免疫状态）
            var (_, _) = self.ProcessBuffsByPriorityAndCategory(
                buffsToProcess, currentRound, true, immunity);
        }

        /// <summary>
        /// 回合结束时的Buff处理（优化版）
        /// </summary>
        public static void OnTurnEnd(this BuffComponent self, int currentRound)
        {
            // 第1步：检查免疫状态
            ImmunityStatus immunity = self.CheckImmunityStatus();

            // 第2步：收集需要处理的Buff
            var buffsToProcess = self.GetTurnEndBuffs();

            // 第3步：处理Buff（传入免疫状态）
            var (toRemove, damageAccumulator) = self.ProcessBuffsByPriorityAndCategory(
                buffsToProcess, currentRound, false, immunity);

            // 第4步：应用累积的伤害（考虑护盾）
            self.ApplyAccumulatedDamage(damageAccumulator);

            // 第5步：移除过期Buff
            foreach (var buff in toRemove)
            {
                BuffHelper.RemoveBuff(buff, BuffFlags.TurnExpiredRemove);
            }
        }

        #endregion

        #region 核心处理方法（提取的公共逻辑）

        /// <summary>
        /// 统一的Buff处理方法（减少代码重复）
        /// </summary>
        private static (List<Buff> toRemove, Dictionary<string, int> damageAccumulator)
            ProcessBuffsByPriorityAndCategory(this BuffComponent self,
                List<Buff> buffs, int currentRound, bool isTurnStart,
                ImmunityStatus immunity = default)
        {
            List<Buff> toRemove = new();
            Dictionary<string, int> damageAccumulator = new();

            // 按类别分组并排序
            var buffGroups = buffs
                .GroupBy(b => BuffConfigCategory.Instance.Get(b.ConfigId).Category)
                .OrderBy(g => GetCategoryPriority(g.Key));

            foreach (var group in buffGroups)
            {
                BuffCategory category = group.Key;

                // 免疫检查（回合开始和结束时都检查）
                if (immunity.IsImmuneToCategory(category))
                {
                    Log.Debug($"Category {category} blocked by immunity at turn {(isTurnStart ? "start" : "end")}");
                    continue;
                }

                // 处理互斥（优化后的方法）
                var buffsToProcess = FilterMutuallyExclusiveOptimized(group);

                // 按优先级处理组内Buff
                foreach (var buff in buffsToProcess.OrderByDescending(b =>
                    BuffConfigCategory.Instance.Get(b.ConfigId).Priority))
                {
                    if (!self.ShouldProcessTurnBased(buff)) continue;

                    // 执行Buff效果
                    if (isTurnStart)
                    {
                        ProcessTurnStartEffect(self, buff, currentRound);
                    }
                    else
                    {
                        ProcessBuffEffect(self, buff, currentRound, category,
                            immunity, damageAccumulator, toRemove);
                    }
                }
            }

            return (toRemove, damageAccumulator);
        }

        /// <summary>
        /// 优化后的互斥处理（处理优先级相同的情况）
        /// </summary>
        private static List<Buff> FilterMutuallyExclusiveOptimized(IEnumerable<Buff> buffs)
        {
            var result = new List<Buff>();
            var mutexGroups = new Dictionary<BuffMutexGroup, Buff>();

            foreach (var buff in buffs)
            {
                BuffConfig config = BuffConfigCategory.Instance.Get(buff.ConfigId);

                // 没有互斥组的直接加入
                if (config.MutexGroup == BuffMutexGroup.None)
                {
                    result.Add(buff);
                    continue;
                }

                // 处理互斥组
                if (!mutexGroups.ContainsKey(config.MutexGroup))
                {
                    mutexGroups[config.MutexGroup] = buff;
                }
                else
                {
                    var existing = mutexGroups[config.MutexGroup];
                    BuffConfig existingConfig = BuffConfigCategory.Instance.Get(existing.ConfigId);

                    // 比较优先级
                    if (config.Priority > existingConfig.Priority)
                    {
                        mutexGroups[config.MutexGroup] = buff;
                    }
                    else if (config.Priority == existingConfig.Priority)
                    {
                        // 优先级相同时，保留创建时间最晚的（后创建的生效）
                        if (buff.CreateTime > existing.CreateTime)
                        {
                            mutexGroups[config.MutexGroup] = buff;
                            Log.Debug($"MutexGroup {config.MutexGroup}: Buff {buff.ConfigId} " +
                                     $"replaces {existing.ConfigId} (same priority, newer)");
                        }
                    }
                }
            }

            result.AddRange(mutexGroups.Values);
            return result;
        }

        #endregion

        #region Buff效果处理

        /// <summary>
        /// 处理单个Buff效果（回合结束）
        /// </summary>
        private static void ProcessBuffEffect(BuffComponent self, Buff buff,
            int currentRound, BuffCategory category, ImmunityStatus immunity,
            Dictionary<string, int> damageAccumulator, List<Buff> toRemove)
        {
            switch (category)
            {
                case BuffCategory.Immunity:
                    ProcessImmunityBuff(self, buff, currentRound);
                    break;

                case BuffCategory.Shield:
                    ProcessShieldBuff(self, buff, currentRound);
                    break;

                case BuffCategory.Damage:
                    int damage = ProcessDamageBuff(self, buff, currentRound, immunity);
                    if (damage > 0)
                    {
                        BuffConfig buffConfig = BuffConfigCategory.Instance.Get(buff.ConfigId);
                        string damageType = buffConfig.HasTag(BuffTag.Physical)
                            ? "Physical" : "Magic";
                        if (!damageAccumulator.ContainsKey(damageType))
                            damageAccumulator[damageType] = 0;
                        damageAccumulator[damageType] += damage;
                    }
                    break;

                case BuffCategory.Heal:
                    ProcessHealBuff(self, buff, currentRound);
                    break;

                case BuffCategory.Control:
                    ProcessControlBuff(self, buff, currentRound);
                    break;

                case BuffCategory.Enhance:
                    ProcessEnhanceBuff(self, buff, currentRound);
                    break;

                case BuffCategory.Debuff:
                    ProcessDebuffBuff(self, buff, currentRound);
                    break;

                default:
                    ProcessGenericBuff(self, buff, currentRound);
                    break;
            }

            // 减少回合数
            if (buff.DurationType == BuffDurationType.Turn ||
                buff.DurationType == BuffDurationType.Hybrid)
            {
                if (buff.RemainTurn > 0)
                {
                    buff.RemainTurn--;
                    if (buff.RemainTurn <= 0)
                    {
                        toRemove.Add(buff);
                    }
                }
            }
        }

        /// <summary>
        /// 处理回合开始效果
        /// </summary>
        private static void ProcessTurnStartEffect(BuffComponent self, Buff buff, int currentRound)
        {
            EffectServerBuffTurnStart effect = buff.GetConfig().GetEffect<EffectServerBuffTurnStart>();
            if (effect != null)
            {
                using BTEnv env = BTEnv.Create(buff.Scene());
                env.AddEntity(effect.Buff, buff);
                env.AddEntity(effect.Unit, self.GetParent<Unit>());
                env.AddEntity(effect.Caster, buff.GetCaster());
                env.AddStruct(effect.CurrentRound, currentRound);

                BTDispatcher.Instance.Handle(effect, env);
            }

            buff.LastTriggerRound = currentRound;
        }

        #endregion

        #region 免疫和伤害处理

        /// <summary>
        /// 检查免疫状态（使用枚举）
        /// </summary>
        private static ImmunityStatus CheckImmunityStatus(this BuffComponent self)
        {
            var status = new ImmunityStatus();
            var immunityBuffs = self.GetBuffsByCategory(BuffCategory.Immunity);

            foreach (var buff in immunityBuffs)
            {
                BuffConfig config = BuffConfigCategory.Instance.Get(buff.ConfigId);

                if (config.HasTag(BuffTag.AllDamageImmunity))
                    status.ImmuneToAllDamage = true;
                if (config.HasTag(BuffTag.PhysicalDamageImmunity))
                    status.ImmuneToPhysical = true;
                if (config.HasTag(BuffTag.MagicDamageImmunity))
                    status.ImmuneToMagic = true;
                if (config.HasTag(BuffTag.ControlImmunity))
                    status.ImmuneToControl = true;
                if (config.HasTag(BuffTag.DebuffImmunity))
                    status.ImmuneToDebuff = true;
            }

            return status;
        }

        /// <summary>
        /// 处理伤害Buff（考虑免疫）
        /// </summary>
        private static int ProcessDamageBuff(BuffComponent self, Buff buff,
            int currentRound, ImmunityStatus immunity)
        {
            BuffConfig config = BuffConfigCategory.Instance.Get(buff.ConfigId);

            // 检查免疫
            if (immunity.ImmuneToAllDamage)
            {
                Log.Debug($"Damage buff {buff.ConfigId} blocked by all damage immunity");
                return 0;
            }

            if (config.HasTag(BuffTag.Physical) && immunity.ImmuneToPhysical)
            {
                Log.Debug($"Physical damage buff {buff.ConfigId} blocked by physical immunity");
                return 0;
            }

            if (config.HasTag(BuffTag.Magic) && immunity.ImmuneToMagic)
            {
                Log.Debug($"Magic damage buff {buff.ConfigId} blocked by magic immunity");
                return 0;
            }

            // 触发伤害效果
            EffectServerBuffTurnEnd effect = buff.GetConfig().GetEffect<EffectServerBuffTurnEnd>();
            if (effect != null)
            {
                using BTEnv env = BTEnv.Create(buff.Scene());
                env.AddEntity(effect.Buff, buff);
                env.AddEntity(effect.Unit, self.GetParent<Unit>());
                env.AddEntity(effect.Caster, buff.GetCaster());
                env.AddStruct(effect.CurrentRound, currentRound);

                int result = BTDispatcher.Instance.Handle(effect, env);

                // 尝试从环境中获取伤害值，如果BTNode设置了的话
                if (env.ContainKey("Damage"))
                {
                    return env.GetStruct<int>("Damage");
                }
            }

            return 0;
        }

        /// <summary>
        /// 应用累积的伤害
        /// </summary>
        private static void ApplyAccumulatedDamage(this BuffComponent self,
            Dictionary<string, int> damageAccumulator)
        {
            if (damageAccumulator.Count == 0) return;

            Unit unit = self.GetParent<Unit>();
            NumericComponent numeric = unit.GetComponent<NumericComponent>();

            int totalDamage = 0;
            foreach (var kvp in damageAccumulator)
            {
                int damage = kvp.Value;

                // 应用护盾 (这里简化处理，实际项目中应该有专门的护盾字段)
                // 暂时注释掉护盾逻辑，等项目添加护盾相关数值类型后再启用
                // int shield = numeric.GetAsInt(NumericType.Shield);
                // if (shield > 0)
                // {
                //     int absorbed = Math.Min(shield, damage);
                //     damage -= absorbed;
                //     numeric.Set(NumericType.Shield, shield - absorbed);
                //     Log.Debug($"Shield absorbed {absorbed} {kvp.Key} damage");
                // }

                totalDamage += damage;
            }

            // 应用最终伤害
            if (totalDamage > 0)
            {
                int currentHp = numeric.GetAsInt(NumericType.HP);
                numeric.Set(NumericType.HP, Math.Max(0, currentHp - totalDamage));
                Log.Debug($"Applied {totalDamage} total damage, HP: {currentHp} -> {numeric.GetAsInt(NumericType.HP)}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取类别优先级（数值越小越先执行）
        /// </summary>
        private static int GetCategoryPriority(BuffCategory category)
        {
            return category switch
            {
                BuffCategory.Immunity => 0,
                BuffCategory.Shield => 1,
                BuffCategory.Defense => 2,
                BuffCategory.Control => 3,
                BuffCategory.Damage => 4,
                BuffCategory.Heal => 5,
                BuffCategory.Enhance => 6,
                BuffCategory.Debuff => 7,
                _ => 99
            };
        }

        /// <summary>
        /// 检查是否应该处理回合制Buff
        /// </summary>
        private static bool ShouldProcessTurnBased(this BuffComponent self, Buff buff)
        {
            return buff != null && !buff.IsDisposed &&
                   (buff.DurationType == BuffDurationType.Turn ||
                    buff.DurationType == BuffDurationType.Hybrid);
        }

        /// <summary>
        /// 获取需要在回合开始处理的Buff
        /// </summary>
        private static List<Buff> GetTurnStartBuffs(this BuffComponent self)
        {
            List<Buff> result = new();
            foreach (var child in self.Children.Values)
            {
                if (child is Buff buff && self.ShouldProcessTurnBased(buff))
                {
                    result.Add(buff);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取需要在回合结束处理的Buff
        /// </summary>
        private static List<Buff> GetTurnEndBuffs(this BuffComponent self)
        {
            List<Buff> result = new();
            foreach (var child in self.Children.Values)
            {
                if (child is Buff buff && self.ShouldProcessTurnBased(buff))
                {
                    result.Add(buff);
                }
            }
            return result;
        }

        /// <summary>
        /// 根据类别获取Buff
        /// </summary>
        private static List<Buff> GetBuffsByCategory(this BuffComponent self, BuffCategory category)
        {
            List<Buff> result = new();
            foreach (var child in self.Children.Values)
            {
                if (child is Buff buff &&
                    BuffConfigCategory.Instance.Get(buff.ConfigId).Category == category)
                {
                    result.Add(buff);
                }
            }
            return result;
        }

        #endregion

        #region 具体效果处理方法

        private static void ProcessImmunityBuff(BuffComponent self, Buff buff, int currentRound)
        {
            // 免疫类Buff通常只需要维持状态，不需要特殊处理
            Log.Debug($"Immunity buff {buff.ConfigId} active at round {currentRound}");
        }

        private static void ProcessShieldBuff(BuffComponent self, Buff buff, int currentRound)
        {
            // 执行护盾效果
            EffectServerBuffTurnEnd effect = buff.GetConfig().GetEffect<EffectServerBuffTurnEnd>();
            if (effect != null)
            {
                using BTEnv env = BTEnv.Create(buff.Scene());
                env.AddEntity(effect.Buff, buff);
                env.AddEntity(effect.Unit, self.GetParent<Unit>());
                env.AddEntity(effect.Caster, buff.GetCaster());
                env.AddStruct(effect.CurrentRound, currentRound);

                BTDispatcher.Instance.Handle(effect, env);
            }
        }

        private static void ProcessHealBuff(BuffComponent self, Buff buff, int currentRound)
        {
            // 执行治疗效果
            EffectServerBuffTurnEnd effect = buff.GetConfig().GetEffect<EffectServerBuffTurnEnd>();
            if (effect != null)
            {
                using BTEnv env = BTEnv.Create(buff.Scene());
                env.AddEntity(effect.Buff, buff);
                env.AddEntity(effect.Unit, self.GetParent<Unit>());
                env.AddEntity(effect.Caster, buff.GetCaster());
                env.AddStruct(effect.CurrentRound, currentRound);

                int result = BTDispatcher.Instance.Handle(effect, env);

                // 尝试从环境中获取治疗值，如果BTNode设置了的话
                if (env.ContainKey("Heal"))
                {
                    int heal = env.GetStruct<int>("Heal");
                    Unit unit = self.GetParent<Unit>();
                    NumericComponent numeric = unit.GetComponent<NumericComponent>();
                    int currentHp = numeric.GetAsInt(NumericType.HP);
                    int maxHp = numeric.GetAsInt(NumericType.MaxHP);
                    int newHp = Math.Min(currentHp + heal, maxHp);
                    numeric.Set(NumericType.HP, newHp);
                    Log.Debug($"Healed {heal} HP, {currentHp} -> {newHp}");
                }
            }
        }

        private static void ProcessControlBuff(BuffComponent self, Buff buff, int currentRound)
        {
            // 处理控制效果
            Log.Debug($"Control buff {buff.ConfigId} active at round {currentRound}");
            // 具体控制效果通过EffectNode实现
        }

        private static void ProcessEnhanceBuff(BuffComponent self, Buff buff, int currentRound)
        {
            // 处理增益效果
            Log.Debug($"Enhance buff {buff.ConfigId} active at round {currentRound}");
            // 具体增益效果通过EffectNode实现
        }

        private static void ProcessDebuffBuff(BuffComponent self, Buff buff, int currentRound)
        {
            // 处理减益效果
            Log.Debug($"Debuff buff {buff.ConfigId} active at round {currentRound}");
            // 具体减益效果通过EffectNode实现
        }

        private static void ProcessGenericBuff(BuffComponent self, Buff buff, int currentRound)
        {
            // 处理其他类型Buff
            EffectServerBuffTurnEnd effect = buff.GetConfig().GetEffect<EffectServerBuffTurnEnd>();
            if (effect != null)
            {
                using BTEnv env = BTEnv.Create(buff.Scene());
                env.AddEntity(effect.Buff, buff);
                env.AddEntity(effect.Unit, self.GetParent<Unit>());
                env.AddEntity(effect.Caster, buff.GetCaster());
                env.AddStruct(effect.CurrentRound, currentRound);

                BTDispatcher.Instance.Handle(effect, env);
            }
        }

        #endregion
    }

}