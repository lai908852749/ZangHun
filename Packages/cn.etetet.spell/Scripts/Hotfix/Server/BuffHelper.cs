using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ET.Server
{
    public static class BuffHelper
    {
        [Invoke(TimerInvokeType.BuffTimeoutTimer)]
        public class BuffTimeoutTimer: ATimer<Buff>
        {
            protected override void Run(Buff self)
            {
                BuffHelper.RemoveBuff(self, BuffFlags.TimeoutRemove);
            }
        }

        public static Buff CreateBuffWithoutInit(Unit unit, long casterId, long buffId, int buffConfigId)
        {
            Buff buff = unit.GetComponent<BuffComponent>().CreateBuff(buffId, buffConfigId, casterId);
            return buff;
        }

        public static Buff CreateBuff(Unit unit, long casterId, long buffId, int buffConfigId, Buff parent)
        {
            Buff buff = unit.GetComponent<BuffComponent>().CreateBuff(buffId, buffConfigId, casterId);
            return InitBuff(buff, parent);
        }
        
        public static Buff InitBuff(Buff buff, Buff parent)
        {
            Unit unit = buff.Parent.GetParent<Unit>();
            BuffComponent buffComponent = buff.GetParent<BuffComponent>();
            BuffConfig buffConfig = buff.GetConfig();

            // 处理互斥组逻辑
            if (buffConfig.MutexGroup != BuffMutexGroup.None)
            {
                // 查找同互斥组的Buff
                var conflictingBuffs = new List<Buff>();
                foreach (var child in buffComponent.Children.Values)
                {
                    if (child is Buff existingBuff && existingBuff != buff)
                    {
                        BuffConfig existingConfig = existingBuff.GetConfig();
                        if (existingConfig.MutexGroup == buffConfig.MutexGroup)
                        {
                            conflictingBuffs.Add(existingBuff);
                        }
                    }
                }

                // 如果有冲突的Buff，进行优先级比较
                if (conflictingBuffs.Count > 0)
                {
                    // 查找最高优先级的冲突Buff
                    Buff highestPriorityBuff = conflictingBuffs
                        .OrderByDescending(b => b.GetConfig().Priority)
                        .First();

                    int highestPriority = highestPriorityBuff.GetConfig().Priority;
                    int newBuffPriority = buffConfig.Priority;

                    if (newBuffPriority > highestPriority)
                    {
                        // 新Buff优先级更高，移除所有同组的低优先级Buff
                        foreach (var conflictingBuff in conflictingBuffs)
                        {
                            RemoveBuff(conflictingBuff, BuffFlags.MutexGroupReplaceRemove);
                        }
                    }
                    else if (newBuffPriority <= highestPriority)
                    {
                        // 新Buff优先级不够高，拒绝添加（移除新Buff）
                        buffComponent.RemoveBuff(buff);
                        return null; // 返回null表示添加失败
                    }
                }
            }

            // 处理叠加规则
            if (buffConfig.OverLayRuleType != OverLayRuleType.None)
            {
                buffComponent.configIdBuffs.Remove(buff.ConfigId, buff);
                var oldBuffs = buffComponent.GetByConfigId(buffConfig.Id);
                if (oldBuffs != null && oldBuffs.Count > 0)
                {
                    switch (buffConfig.OverLayRuleType)
                    {
                        case OverLayRuleType.AddStack:
                        {
                            Buff oldBuff = oldBuffs.First();
                            UpdateStack(oldBuff, oldBuff.Stack + buffConfig.Stack);
                            return oldBuff;
                        }
                        case OverLayRuleType.AddTime:
                        {
                            Buff oldBuff = oldBuffs.First();
                            long expireTime = TimeInfo.Instance.ServerNow() + buffConfig.Duration;
                            if (expireTime < oldBuff.ExpireTime)
                            {
                                return oldBuff;
                            }
                            UpdateExpireTime(oldBuff, expireTime);
                            return oldBuff;
                        }
                        case OverLayRuleType.Replace:
                        {
                            Buff oldBuff = oldBuffs.First();
                            RemoveBuff(oldBuff, BuffFlags.SameConfigIdReplaceRemove);
                            buffComponent.configIdBuffs.Add(buff.ConfigId, buff);
                            break;
                        }
                        case OverLayRuleType.None:
                        {
                            buffComponent.configIdBuffs.Add(buff.ConfigId, buff);
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            
            if (parent != null)
            {
                BuffParentComponent buffParentComponent = parent.GetComponent<BuffParentComponent>();
                Buff rootBuff = buffParentComponent.RootBuff;
                BuffParentComponent c = buff.AddComponent<BuffParentComponent>();
                c.ParentBuff = parent;
                if (rootBuff != null)
                {
                    c.RootBuff = rootBuff;
                }
            }
            else
            {
                BuffParentComponent c = buff.AddComponent<BuffParentComponent>();
                c.ParentBuff = buff;
                c.RootBuff = buff;
            }
            
            TimerComponent timerComponent = buff.Root().GetComponent<TimerComponent>();
            
            if (buff.TickTime > 0)
            {
                WaitTick(buff, timerComponent).NoContext();
            }
            
            if (buff.ExpireTime > 0 && buff.ExpireTime < long.MaxValue)
            {
                if (buff.TickTime > 0)
                {
                    buff.ExpireTime += 1;
                }
                buff.TimeoutTimer = timerComponent.NewOnceTimer(buff.ExpireTime, TimerInvokeType.BuffTimeoutTimer, buff);
            }
            
            if (buffConfig.Flags.Contains(BuffFlags.ParentRemoveRemove))
            {
                BuffChildrenComponent buffChildrenComponent =
                        parent.GetComponent<BuffChildrenComponent>() ?? parent.AddComponent<BuffChildrenComponent>();
                buffChildrenComponent.Buffs.Add(buff);
            }
            
            
            M2C_BuffAdd m2CBuffAdd = M2C_BuffAdd.Create();
            m2CBuffAdd.BuffId = buff.Id;
            m2CBuffAdd.BuffConfigId = buffConfig.Id;
            m2CBuffAdd.UnitId = unit.Id;
            m2CBuffAdd.CreateTime = buff.CreateTime;
            m2CBuffAdd.TickTime = buff.TickTime;
            m2CBuffAdd.ExpireTime = buff.ExpireTime;
            m2CBuffAdd.Stack = buff.Stack;
            m2CBuffAdd.CasterId = buff.Caster;
            SpellTargetComponent spellTargetComponent = buff.GetComponent<SpellTargetComponent>();
            if (spellTargetComponent != null)
            {
                m2CBuffAdd.SpellTarget = SpellTarget.Create();
                foreach (long targetId in spellTargetComponent.Units)
                {
                    m2CBuffAdd.SpellTarget.TargetUnitId.Add(targetId);
                }
                m2CBuffAdd.SpellTarget.TargetPosition = spellTargetComponent.Position;
            }
            
            MapMessageHelper.NoticeClient(unit, m2CBuffAdd, buffConfig.NoticeType);

            EffectServerBuffAdd effect = buff.GetConfig().GetEffect<EffectServerBuffAdd>();
            if (effect != null)
            {
                using BTEnv env = BTEnv.Create(buff.Scene());
                env.AddEntity(effect.Buff, buff);
                env.AddEntity(effect.Unit, buff.Parent.GetParent<Unit>());
                env.AddEntity(effect.Caster, buff.GetCaster());
                BTDispatcher.Instance.Handle(effect, env);
            }

            return buff;
        }

        private static async ETTask WaitTick(Buff buff, TimerComponent timerComponent)
        {
            BuffConfig buffConfig = buff.GetConfig();

            EntityRef<Buff> buffRef = buff;
            int i = 0;
            while (true)
            {
                ++i;
                // 新版本的buff tick会立刻受到急速属性影响，这里每次tick完成都要重新计算
                long nextTick = buff.CreateTime + buffConfig.TickTime * i;
                await timerComponent.WaitTillAsync(nextTick);
                buff = buffRef;
                if (buff == null)
                {
                    return;
                }
                
                EffectServerBuffTick effect = buff.GetConfig().GetEffect<EffectServerBuffTick>();
                if (effect != null)
                {
                    using BTEnv env = BTEnv.Create(buff.Scene());
                    env.AddEntity(effect.Buff, buff);
                    env.AddEntity(effect.Unit, buff.Parent.GetParent<Unit>());
                    env.AddEntity(effect.Caster, buff.GetCaster());
                    BTDispatcher.Instance.Handle(effect, env);
                }
            }
        }
        
        public static void UpdateExpireTime(Buff buff, long expireTime)
        {
            buff.ExpireTime = expireTime;
            
            if (buff.ExpireTime < TimeInfo.Instance.ServerFrameTime())
            {
                BuffHelper.RemoveBuff(buff, BuffFlags.TimeoutRemove);
                return;
            }
            
            
            long timeoutTimer = buff.TimeoutTimer;
            TimerComponent timerComponent = buff.Root().GetComponent<TimerComponent>();
            timerComponent.Remove(ref timeoutTimer);
            
            if (buff.ExpireTime > 0)
            {
                buff.TimeoutTimer = timerComponent.NewOnceTimer(buff.ExpireTime, TimerInvokeType.BuffTimeoutTimer, buff);
            }

            Unit unit = buff.Parent.GetParent<Unit>();
            M2C_BuffUpdate m2CBuffUpdate = M2C_BuffUpdate.Create();
            m2CBuffUpdate.BuffId = buff.Id;
            m2CBuffUpdate.UnitId = unit.Id;
            m2CBuffUpdate.Stack = -1;
            m2CBuffUpdate.ExpireTime = expireTime;
            m2CBuffUpdate.TickTime = -1;
            MapMessageHelper.NoticeClient(unit, m2CBuffUpdate, buff.GetConfig().NoticeType);
        }
        
        public static void UpdateTickTime(Buff buff, int tickTime)
        {
            buff.TickTime = tickTime;
            
            Unit unit = buff.Parent.GetParent<Unit>();
            M2C_BuffUpdate m2CBuffUpdate = M2C_BuffUpdate.Create();
            m2CBuffUpdate.BuffId = buff.Id;
            m2CBuffUpdate.UnitId = unit.Id;
            m2CBuffUpdate.Stack = -1;
            m2CBuffUpdate.ExpireTime = -1;
            m2CBuffUpdate.TickTime = tickTime;
            MapMessageHelper.NoticeClient(unit, m2CBuffUpdate, buff.GetConfig().NoticeType);
        }
        
        public static void UpdateStack(Buff buff, int stack)
        {
            if (buff.Stack == stack)
            {
                return;
            }

            BuffConfig buffConfig = buff.GetConfig();
            int maxStack = buffConfig.MaxStack;
            if (stack > maxStack)
            {
                stack = maxStack;
            }
            
            if (stack < 0)
            {
                stack = 0;
            }
            
            buff.Stack = stack;
            
            if (buff.Stack == 0)
            {
                RemoveBuff(buff, BuffFlags.StackRemove);
                return;
            }
            Unit unit = buff.Parent.GetParent<Unit>();
            M2C_BuffUpdate m2CBuffUpdate = M2C_BuffUpdate.Create();
            m2CBuffUpdate.BuffId = buff.Id;
            m2CBuffUpdate.UnitId = unit.Id;
            m2CBuffUpdate.Stack = buff.Stack;
            m2CBuffUpdate.ExpireTime = -1;
            m2CBuffUpdate.TickTime = -1;
            MapMessageHelper.NoticeClient(unit, m2CBuffUpdate, buffConfig.NoticeType);
        }

        public static void RemoveBuff(Buff buff, BuffFlags removeType)
        {
            Unit unit = buff.Parent?.GetParent<Unit>();
            if (unit == null || unit.IsDisposed)
            {
                return;
            }
            
            BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
            
            M2C_BuffRemove m2CBuffRemove = M2C_BuffRemove.Create();
            m2CBuffRemove.UnitId = unit.Id;
            m2CBuffRemove.BuffId = buff.Id;
            m2CBuffRemove.BuffConfigId = buff.ConfigId;
            m2CBuffRemove.RemoveType = (int)removeType;

            BuffRemoveTypeComponent buffRemoveTypeComponent = buff.GetComponent<BuffRemoveTypeComponent>() ?? buff.AddComponent<BuffRemoveTypeComponent>();
            buffRemoveTypeComponent.BuffRemoveType = removeType;
            
            EffectServerBuffRemove effect = buff.GetConfig().GetEffect<EffectServerBuffRemove>();
            if (effect != null)
            {
                using BTEnv env = BTEnv.Create(buff.Scene());
                env.AddEntity(effect.Buff, buff);
                env.AddEntity(effect.Unit, buff.Parent.GetParent<Unit>());
                env.AddEntity(effect.Caster, buff.GetCaster());
                BTDispatcher.Instance.Handle(effect, env);
            }

            buffComponent.RemoveBuff(buff);
            
            MapMessageHelper.NoticeClient(unit, m2CBuffRemove, buff.GetConfig().NoticeType);
        }
        
        public static void RemoveBuff(Unit unit, long id, BuffFlags removeType)
        {
            BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
            Buff buff = buffComponent.GetChild<Buff>(id);
            
            if (buff == null)
            {
                return;
            }
            
            RemoveBuff(buff, removeType);
        }

        public static void RemoveBuffByConfigId(Unit unit, int configId, BuffFlags removeType)
        {
            BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
            HashSet<EntityRef<Buff>> buffs = buffComponent.GetByConfigId(configId);
            if (buffs == null)
            {
                return;
            }
            foreach (EntityRef<Buff> buff in buffs.ToArray())
            {
                RemoveBuff(buff, removeType);
            }
        }
        
        public static void RemoveBuffFlag(Unit unit, BuffFlags flag)
        {
            BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
            if (buffComponent == null)
            {
                return;
            }
            HashSet<EntityRef<Buff>> buffs = buffComponent.GetByFlag(flag);
            if (buffs == null)
            {
                return;
            }
            foreach (EntityRef<Buff> buff in buffs.ToArray())
            {
                RemoveBuff(buff, flag);
            }
        }
    }
}