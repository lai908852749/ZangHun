# ET框架战斗系统、Spell与Buff集成方案（完全修正版）

## 一、核心概念理解

### 1.1 技能即Buff原理

**核心理念**：
- ✅ **所有技能的本质都是Buff**
- ✅ **技能系统只负责创建Buff，所有效果都通过Buff的EffectNode实现**
- ✅ **被动技能是预先添加的长期Buff**
- ✅ **战斗时机触发的是Buff的EffectNode，不是技能施放**

### 1.2 技能系统完整流程分析

#### 主动技能施放流程
```
InputSystemComponentSystem.CastSpell()
    ↓ 发布事件
EventSystem.Instance.Publish(OnSpellTrigger)
    ↓ 事件处理
OnSpellTriggerEvent.Run() → 目标选择 → C2M_SpellCast
    ↓ 服务端处理
C2M_SpellCastHandler.Run() → SpellHelper.Cast()
    ↓ Buff创建
BuffHelper.CreateBuff() → BuffHelper.InitBuff()
    ↓ 效果执行
EffectServerBuffAdd.Handle() → 产生实际效果
```

#### 被动技能（被动Buff）触发流程
```
游戏开始/获得能力
    ↓ 添加被动Buff
BuffHelper.CreateBuff(被动技能对应的BuffId)
    ↓ 战斗时机到达
BuffTurnBasedSystem.OnTurnStart/OnTurnEnd()
    ↓ 执行Buff的EffectNode
EffectServerBuffTurnStart/TurnEnd.Handle()
    ↓ 产生效果（可能创建新Buff）
```

### 1.3 关键技术要点

1. **技能与Buff关系**：每个SpellConfig对应一个BuffId，技能通过创建Buff实现所有效果
2. **被动能力实现**：被动技能是长期存在的Buff，在特定时机执行其EffectNode
3. **EffectNode驱动**：所有实际效果都通过EffectNode实现，包括伤害、治疗、状态变化等
4. **系统职责**：技能系统负责Buff创建，Buff系统负责效果执行

## 二、被动能力实现方案

### 2.1 被动能力的本质

被动能力不是"技能"，而是预先添加给单位的长期Buff：

```csharp
// 给单位添加被动能力（吸血）
int vampireBuffId = 9001; // 吸血被动Buff
Buff vampireBuff = BuffHelper.CreateBuff(unit, unit.Id, vampireBuffId, vampireBuffId, null);

// 这个Buff配置了EffectServerBuffCombat，在攻击时触发
```

### 2.2 被动Buff配置示例

```csharp
// 被动技能对应的Buff配置
BuffConfig vampireConfig = new BuffConfig
{
    Id = 9001,
    Name = "吸血被动",
    DurationType = BuffDurationType.Permanent, // 永久存在
    Category = BuffCategory.Enhance,
    // 配置攻击时触发的效果
    Effects = new List<EffectNode>
    {
        new EffectServerBuffCombatTrigger
        {
            TriggerType = CombatTriggerType.OnAttack,
            // 子节点：创建吸血治疗Buff
            Children = new List<BTNode>
            {
                new EffectServerCreateBuff { BuffId = 9002 } // 创建治疗Buff
            }
        }
    }
};
```

### 2.3 Buff持续类型说明

不同的BuffDurationType适用于不同的场景：

| 类型 | 持续方式 | 适用场景 | 示例 |
|------|----------|----------|------|
| Time | 按时间持续 | 实时战斗、临时效果 | 3秒护盾、10秒加速 |
| Turn | 按回合持续 | 回合制战斗 | 持续3回合的中毒 |
| Permanent | 永久存在 | 被动技能、装备属性 | 吸血被动、装备加成 |
| Hybrid | 时间+回合混合 | 复杂战斗系统 | 最多5回合或30秒 |
| CombatOnly | 仅战斗期间 | 战斗临时效果 | 战斗药剂、临时光环 |

### 2.4 EffectNode时机对应关系

不同的战斗时机对应不同的EffectNode：

| 时机 | EffectNode | 说明 |
|------|------------|------|
| 回合开始 | EffectServerBuffTurnStart | 回合开始时执行 |
| 回合结束 | EffectServerBuffTurnEnd | 回合结束时执行 |
| Buff添加时 | EffectServerBuffAdd | Buff创建时立即执行 |
| Buff移除时 | EffectServerBuffRemove | Buff销毁时执行 |
| 攻击时 | EffectServerBuffCombat (OnAttack) | 攻击时执行 |
| 被攻击时 | EffectServerBuffCombat (OnAttacked) | 被攻击时执行 |
| 暴击时 | EffectServerBuffCombat (OnCritical) | 暴击时执行 |
| 战斗开始 | EffectServerBuffCombatStart | 战斗开始时执行（CombatOnly类型） |
| 战斗结束 | EffectServerBuffCombatEnd | 战斗结束时执行（CombatOnly类型） |

## 三、战斗系统集成方案

### 3.1 回合制战斗中的Buff处理

战斗系统不发布事件触发技能，而是直接调用Buff系统的方法：

```csharp
// 在回合循环中触发Buff效果
private static async ETTask RunCombatLoop(this TurnBasedCombatComponent self)
{
    while (self.InCombat)
    {
        for (int i = 0; i < self.TurnOrder.Count; i++)
        {
            long actorId = self.TurnOrder[i];
            Unit actor = self.Root.GetComponent<UnitComponent>().Get(actorId);

            // 触发回合开始时的Buff效果
            BuffComponent buffComponent = actor.GetComponent<BuffComponent>();
            buffComponent.OnTurnStart(self.RoundNumber);

            // 处理单位行动
            if (actorId == self.PlayerUnitId)
            {
                await self.ProcessPlayerTurn(actor);
            }
            else
            {
                await self.ProcessMonsterTurn(actor);
            }

            // 触发回合结束时的Buff效果
            buffComponent.OnTurnEnd(self.RoundNumber);

            // 检查战斗结束
            if (self.CheckCombatEnd())
            {
                await self.EndCombat();
                return;
            }
        }

        self.RoundNumber++;
    }
}
```

### 3.2 攻击流程中的Buff触发

```csharp
private static async ETTask ExecuteAttack(this TurnBasedCombatComponent self,
    Unit attacker, Unit target)
{
    var attackerNum = attacker.GetComponent<NumericComponent>();
    var targetNum = target.GetComponent<NumericComponent>();

    // 1. 计算基础伤害
    int baseDamage = attackerNum.GetAsInt(NumericType.Attack);

    // 2. 触发攻击者的攻击时Buff效果（如吸血、附加伤害等）
    BuffComponent attackerBuff = attacker.GetComponent<BuffComponent>();
    attackerBuff.TriggerOnAttackEffect(target, baseDamage);

    // 3. 判定命中/闪避/格挡/暴击
    CombatResult result = self.CalculateCombatResult(attacker, target, baseDamage);

    // 4. 根据结果触发对应的Buff效果
    BuffComponent targetBuff = target.GetComponent<BuffComponent>();

    switch (result.Type)
    {
        case ResultType.Hit:
            // 普通命中，触发被攻击时效果
            self.ApplyDamage(target, result.FinalDamage);
            targetBuff.TriggerOnAttackedEffect(attacker, result.FinalDamage);
            break;

        case ResultType.Critical:
            // 暴击
            self.ApplyDamage(target, result.FinalDamage);
            attackerBuff.TriggerOnCriticalEffect(target, result.FinalDamage);
            targetBuff.TriggerOnAttackedEffect(attacker, result.FinalDamage);
            break;

        case ResultType.Block:
            // 被格挡
            targetBuff.TriggerOnBlockEffect(attacker, result.BlockedDamage);
            break;

        case ResultType.Dodge:
            // 被闪避
            targetBuff.TriggerOnDodgeEffect(attacker);
            break;
    }

    // 5. 检查生命值阈值触发
    self.CheckHealthThresholds(target);

    await ETTask.CompletedTask;
}
```

### 3.3 BuffComponent扩展方法（分离架构）

为BuffComponent添加分离的战斗触发方法：

```csharp
public static class BuffCombatExtension
{
    /// <summary>
    /// 触发攻击时的Buff效果
    /// </summary>
    public static void TriggerOnAttackEffect(this BuffComponent self, Unit target, int damage)
    {
        foreach (var child in self.Children.Values)
        {
            if (child is Buff buff)
            {
                // 直接检查是否有攻击时触发效果
                EffectServerBuffOnAttack effect = buff.GetConfig().GetEffect<EffectServerBuffOnAttack>();
                if (effect != null)
                {
                    using BTEnv env = BTEnv.Create(buff.Scene());
                    env.AddEntity(effect.Buff, buff);
                    env.AddEntity(effect.Unit, self.GetParent<Unit>());
                    env.AddEntity(effect.Target, target);
                    env.AddEntity(effect.Caster, buff.GetCaster());
                    env.AddStruct(effect.Damage, damage);

                    BTDispatcher.Instance.Handle(effect, env);
                    Log.Debug($"OnAttack trigger: Buff {buff.ConfigId}, Damage: {damage}");
                }
            }
        }
    }

    /// <summary>
    /// 触发被攻击时的Buff效果
    /// </summary>
    public static void TriggerOnAttackedEffect(this BuffComponent self, Unit attacker, int damage)
    {
        foreach (var child in self.Children.Values)
        {
            if (child is Buff buff)
            {
                // 直接检查是否有被攻击时触发效果
                EffectServerBuffOnAttacked effect = buff.GetConfig().GetEffect<EffectServerBuffOnAttacked>();
                if (effect != null)
                {
                    using BTEnv env = BTEnv.Create(buff.Scene());
                    env.AddEntity(effect.Buff, buff);
                    env.AddEntity(effect.Unit, self.GetParent<Unit>());
                    env.AddEntity(effect.Attacker, attacker);
                    env.AddEntity(effect.Caster, buff.GetCaster());
                    env.AddStruct(effect.Damage, damage);

                    BTDispatcher.Instance.Handle(effect, env);
                    Log.Debug($"OnAttacked trigger: Buff {buff.ConfigId}, Damage: {damage}");
                }
            }
        }
    }

    /// <summary>
    /// 触发暴击时的Buff效果
    /// </summary>
    public static void TriggerOnCriticalEffect(this BuffComponent self, Unit target, int criticalDamage)
    {
        foreach (var child in self.Children.Values)
        {
            if (child is Buff buff)
            {
                EffectServerBuffOnCritical effect = buff.GetConfig().GetEffect<EffectServerBuffOnCritical>();
                if (effect != null)
                {
                    using BTEnv env = BTEnv.Create(buff.Scene());
                    env.AddEntity(effect.Buff, buff);
                    env.AddEntity(effect.Unit, self.GetParent<Unit>());
                    env.AddEntity(effect.Target, target);
                    env.AddEntity(effect.Caster, buff.GetCaster());
                    env.AddStruct(effect.CriticalDamage, criticalDamage);

                    BTDispatcher.Instance.Handle(effect, env);
                    Log.Debug($"OnCritical trigger: Buff {buff.ConfigId}, Critical Damage: {criticalDamage}");
                }
            }
        }
    }

    /// <summary>
    /// 触发格挡时的Buff效果
    /// </summary>
    public static void TriggerOnBlockEffect(this BuffComponent self, Unit attacker, int blockedDamage)
    {
        foreach (var child in self.Children.Values)
        {
            if (child is Buff buff)
            {
                EffectServerBuffOnBlock effect = buff.GetConfig().GetEffect<EffectServerBuffOnBlock>();
                if (effect != null)
                {
                    using BTEnv env = BTEnv.Create(buff.Scene());
                    env.AddEntity(effect.Buff, buff);
                    env.AddEntity(effect.Unit, self.GetParent<Unit>());
                    env.AddEntity(effect.Attacker, attacker);
                    env.AddEntity(effect.Caster, buff.GetCaster());
                    env.AddStruct(effect.BlockedDamage, blockedDamage);

                    BTDispatcher.Instance.Handle(effect, env);
                    Log.Debug($"OnBlock trigger: Buff {buff.ConfigId}, Blocked: {blockedDamage}");
                }
            }
        }
    }

    /// <summary>
    /// 触发闪避时的Buff效果
    /// </summary>
    public static void TriggerOnDodgeEffect(this BuffComponent self, Unit attacker)
    {
        foreach (var child in self.Children.Values)
        {
            if (child is Buff buff)
            {
                EffectServerBuffOnDodge effect = buff.GetConfig().GetEffect<EffectServerBuffOnDodge>();
                if (effect != null)
                {
                    using BTEnv env = BTEnv.Create(buff.Scene());
                    env.AddEntity(effect.Buff, buff);
                    env.AddEntity(effect.Unit, self.GetParent<Unit>());
                    env.AddEntity(effect.Attacker, attacker);
                    env.AddEntity(effect.Caster, buff.GetCaster());

                    BTDispatcher.Instance.Handle(effect, env);
                    Log.Debug($"OnDodge trigger: Buff {buff.ConfigId}");
                }
            }
        }
    }

    /// <summary>
    /// 触发生命值阈值时的Buff效果
    /// </summary>
    public static void TriggerOnHealthThresholdEffect(this BuffComponent self, float currentHealthPercent)
    {
        foreach (var child in self.Children.Values)
        {
            if (child is Buff buff)
            {
                EffectServerBuffOnHealthThreshold effect = buff.GetConfig().GetEffect<EffectServerBuffOnHealthThreshold>();
                if (effect != null && currentHealthPercent <= effect.ThresholdPercent)
                {
                    using BTEnv env = BTEnv.Create(buff.Scene());
                    env.AddEntity(effect.Buff, buff);
                    env.AddEntity(effect.Unit, self.GetParent<Unit>());
                    env.AddEntity(effect.Caster, buff.GetCaster());
                    env.AddStruct(effect.CurrentHealthPercent, currentHealthPercent);

                    BTDispatcher.Instance.Handle(effect, env);
                    Log.Debug($"OnHealthThreshold trigger: Buff {buff.ConfigId}, Health: {currentHealthPercent:P1}");
                }
            }
        }
    }
}
```

## 四、EffectNode定义（分离架构）

### 4.1 设计原则

**核心思想**：将原本的单一 `EffectServerBuffCombat` 分离为多个独立的EffectNode，每个EffectNode对应一种战斗触发类型。

**优势**：
- ✅ **按需配置**：技能配置时需要什么触发就挂载什么EffectNode
- ✅ **性能优化**：无需foreach循环，直接精确触发
- ✅ **代码清晰**：每个EffectNode功能单一，职责明确
- ✅ **易于扩展**：新增触发类型只需添加新的EffectNode

### 4.2 分离的服务端EffectNode定义

```csharp
// 位置：cn.etetet.spell/Scripts/Model/Share/Root/

// 攻击时触发
public class EffectServerBuffOnAttack : EffectNode
{
    [BTOutput(typeof(Buff))]
    [BoxGroup("输出参数")]
    public string Buff = "Buff";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Target = "Target";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Caster = "Caster";

    [BTOutput(typeof(int))]
    [BoxGroup("输出参数")]
    public string Damage = "Damage";
}

// 被攻击时触发
public class EffectServerBuffOnAttacked : EffectNode
{
    [BTOutput(typeof(Buff))]
    [BoxGroup("输出参数")]
    public string Buff = "Buff";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Attacker = "Attacker";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Caster = "Caster";

    [BTOutput(typeof(int))]
    [BoxGroup("输出参数")]
    public string Damage = "Damage";
}

// 暴击时触发
public class EffectServerBuffOnCritical : EffectNode
{
    [BTOutput(typeof(Buff))]
    [BoxGroup("输出参数")]
    public string Buff = "Buff";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Target = "Target";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Caster = "Caster";

    [BTOutput(typeof(int))]
    [BoxGroup("输出参数")]
    public string CriticalDamage = "CriticalDamage";
}

// 格挡时触发
public class EffectServerBuffOnBlock : EffectNode
{
    [BTOutput(typeof(Buff))]
    [BoxGroup("输出参数")]
    public string Buff = "Buff";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Attacker = "Attacker";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Caster = "Caster";

    [BTOutput(typeof(int))]
    [BoxGroup("输出参数")]
    public string BlockedDamage = "BlockedDamage";
}

// 闪避时触发
public class EffectServerBuffOnDodge : EffectNode
{
    [BTOutput(typeof(Buff))]
    [BoxGroup("输出参数")]
    public string Buff = "Buff";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Attacker = "Attacker";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Caster = "Caster";
}

// 生命值阈值触发
public class EffectServerBuffOnHealthThreshold : EffectNode
{
    [BTInput]
    [BoxGroup("输入参数")]
    public float ThresholdPercent = 0.25f; // 默认25%

    [BTOutput(typeof(Buff))]
    [BoxGroup("输出参数")]
    public string Buff = "Buff";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Caster = "Caster";

    [BTOutput(typeof(float))]
    [BoxGroup("输出参数")]
    public string CurrentHealthPercent = "CurrentHealthPercent";
}
```

### 4.3 对应的处理器

```csharp
// 位置：cn.etetet.spell/Scripts/Hotfix/Server/

// 攻击时处理器
public class BTEffectServerBuffOnAttackHandler : ABTHandler<EffectServerBuffOnAttack>
{
    protected override int Run(EffectServerBuffOnAttack node, BTEnv env)
    {
        Buff buff = env.GetEntity<Buff>(node.Buff);
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit target = env.GetEntity<Unit>(node.Target);
        int damage = env.GetStruct<int>(node.Damage);

        Log.Debug($"[Server] OnAttack effect - Unit: {unit?.Id}, Target: {target?.Id}, Damage: {damage}");

        // 执行子节点
        return RunChildren(node, env);
    }
}

// 被攻击时处理器
public class BTEffectServerBuffOnAttackedHandler : ABTHandler<EffectServerBuffOnAttacked>
{
    protected override int Run(EffectServerBuffOnAttacked node, BTEnv env)
    {
        Buff buff = env.GetEntity<Buff>(node.Buff);
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit attacker = env.GetEntity<Unit>(node.Attacker);
        int damage = env.GetStruct<int>(node.Damage);

        Log.Debug($"[Server] OnAttacked effect - Unit: {unit?.Id}, Attacker: {attacker?.Id}, Damage: {damage}");

        return RunChildren(node, env);
    }
}

// 暴击时处理器
public class BTEffectServerBuffOnCriticalHandler : ABTHandler<EffectServerBuffOnCritical>
{
    protected override int Run(EffectServerBuffOnCritical node, BTEnv env)
    {
        Buff buff = env.GetEntity<Buff>(node.Buff);
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit target = env.GetEntity<Unit>(node.Target);
        int criticalDamage = env.GetStruct<int>(node.CriticalDamage);

        Log.Debug($"[Server] OnCritical effect - Unit: {unit?.Id}, Target: {target?.Id}, Critical Damage: {criticalDamage}");

        return RunChildren(node, env);
    }
}

// 格挡时处理器
public class BTEffectServerBuffOnBlockHandler : ABTHandler<EffectServerBuffOnBlock>
{
    protected override int Run(EffectServerBuffOnBlock node, BTEnv env)
    {
        Buff buff = env.GetEntity<Buff>(node.Buff);
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit attacker = env.GetEntity<Unit>(node.Attacker);
        int blockedDamage = env.GetStruct<int>(node.BlockedDamage);

        Log.Debug($"[Server] OnBlock effect - Unit: {unit?.Id}, Attacker: {attacker?.Id}, Blocked: {blockedDamage}");

        return RunChildren(node, env);
    }
}

// 闪避时处理器
public class BTEffectServerBuffOnDodgeHandler : ABTHandler<EffectServerBuffOnDodge>
{
    protected override int Run(EffectServerBuffOnDodge node, BTEnv env)
    {
        Buff buff = env.GetEntity<Buff>(node.Buff);
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit attacker = env.GetEntity<Unit>(node.Attacker);

        Log.Debug($"[Server] OnDodge effect - Unit: {unit?.Id}, Dodged attack from: {attacker?.Id}");

        return RunChildren(node, env);
    }
}

// 生命值阈值处理器
public class BTEffectServerBuffOnHealthThresholdHandler : ABTHandler<EffectServerBuffOnHealthThreshold>
{
    protected override int Run(EffectServerBuffOnHealthThreshold node, BTEnv env)
    {
        Buff buff = env.GetEntity<Buff>(node.Buff);
        Unit unit = env.GetEntity<Unit>(node.Unit);
        float currentHealthPercent = env.GetStruct<float>(node.CurrentHealthPercent);

        Log.Debug($"[Server] OnHealthThreshold effect - Unit: {unit?.Id}, Health: {currentHealthPercent:P1}, Threshold: {node.ThresholdPercent:P1}");

        return RunChildren(node, env);
    }
}

// 共用的子节点执行方法
public static class BTHandlerHelper
{
    public static int RunChildren(BTNode node, BTEnv env)
    {
        foreach (BTNode subNode in node.Children)
        {
            int ret = BTDispatcher.Instance.Handle(subNode, env);
            if (ret != 0)
            {
                return ret;
            }
        }
        return 0;
    }
}
```

### 4.4 分离的客户端EffectNode定义

```csharp
// 位置：cn.etetet.spell/Scripts/ModelView/Client/Root/

// 客户端攻击效果
public class EffectClientBuffOnAttack : EffectNode
{
    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Target = "Target";

    [BTOutput(typeof(int))]
    [BoxGroup("输出参数")]
    public string Damage = "Damage";
}

// 客户端被攻击效果
public class EffectClientBuffOnAttacked : EffectNode
{
    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Attacker = "Attacker";

    [BTOutput(typeof(int))]
    [BoxGroup("输出参数")]
    public string Damage = "Damage";
}

// 客户端暴击效果
public class EffectClientBuffOnCritical : EffectNode
{
    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Target = "Target";

    [BTOutput(typeof(int))]
    [BoxGroup("输出参数")]
    public string CriticalDamage = "CriticalDamage";
}

// 客户端格挡效果
public class EffectClientBuffOnBlock : EffectNode
{
    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Attacker = "Attacker";

    [BTOutput(typeof(int))]
    [BoxGroup("输出参数")]
    public string BlockedDamage = "BlockedDamage";
}

// 客户端闪避效果
public class EffectClientBuffOnDodge : EffectNode
{
    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Unit = "Unit";

    [BTOutput(typeof(Unit))]
    [BoxGroup("输出参数")]
    public string Attacker = "Attacker";
}
```

### 4.5 分离的客户端处理器

```csharp
// 位置：cn.etetet.spell/Scripts/HotfixView/Client/

// 客户端攻击效果处理器
public class BTEffectClientBuffOnAttackHandler : ABTHandler<EffectClientBuffOnAttack>
{
    protected override int Run(EffectClientBuffOnAttack node, BTEnv env)
    {
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit target = env.GetEntity<Unit>(node.Target);
        int damage = env.GetStruct<int>(node.Damage);

        Log.Console($"[Client] OnAttack effect - Unit: {unit?.Id}, Target: {target?.Id}, Damage: {damage}");

        // 播放攻击特效（伪代码）
        // PlayAttackEffect(unit, target, damage);

        return BTHandlerHelper.RunChildren(node, env);
    }
}

// 客户端被攻击效果处理器
public class BTEffectClientBuffOnAttackedHandler : ABTHandler<EffectClientBuffOnAttacked>
{
    protected override int Run(EffectClientBuffOnAttacked node, BTEnv env)
    {
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit attacker = env.GetEntity<Unit>(node.Attacker);
        int damage = env.GetStruct<int>(node.Damage);

        Log.Console($"[Client] OnAttacked effect - Unit: {unit?.Id}, Attacker: {attacker?.Id}, Damage: {damage}");

        // 播放受击特效（伪代码）
        // PlayHitEffect(unit, damage);

        return BTHandlerHelper.RunChildren(node, env);
    }
}

// 客户端暴击效果处理器
public class BTEffectClientBuffOnCriticalHandler : ABTHandler<EffectClientBuffOnCritical>
{
    protected override int Run(EffectClientBuffOnCritical node, BTEnv env)
    {
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit target = env.GetEntity<Unit>(node.Target);
        int criticalDamage = env.GetStruct<int>(node.CriticalDamage);

        Log.Console($"[Client] OnCritical effect - Unit: {unit?.Id}, Target: {target?.Id}, Critical: {criticalDamage}");

        // 播放暴击特效（伪代码）
        // PlayCriticalEffect(unit, target, criticalDamage);

        return BTHandlerHelper.RunChildren(node, env);
    }
}

// 客户端格挡效果处理器
public class BTEffectClientBuffOnBlockHandler : ABTHandler<EffectClientBuffOnBlock>
{
    protected override int Run(EffectClientBuffOnBlock node, BTEnv env)
    {
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit attacker = env.GetEntity<Unit>(node.Attacker);
        int blockedDamage = env.GetStruct<int>(node.BlockedDamage);

        Log.Console($"[Client] OnBlock effect - Unit: {unit?.Id}, Blocked: {blockedDamage}");

        // 播放格挡特效（伪代码）
        // PlayBlockEffect(unit, blockedDamage);

        return BTHandlerHelper.RunChildren(node, env);
    }
}

// 客户端闪避效果处理器
public class BTEffectClientBuffOnDodgeHandler : ABTHandler<EffectClientBuffOnDodge>
{
    protected override int Run(EffectClientBuffOnDodge node, BTEnv env)
    {
        Unit unit = env.GetEntity<Unit>(node.Unit);
        Unit attacker = env.GetEntity<Unit>(node.Attacker);

        Log.Console($"[Client] OnDodge effect - Unit: {unit?.Id}, Dodged attack from: {attacker?.Id}");

        // 播放闪避特效（伪代码）
        // PlayDodgeEffect(unit);

        return BTHandlerHelper.RunChildren(node, env);
    }
}
```

## 五、测试用例设计

### 5.1 RobotCase_020_BuffEffectIntegration（修正版）

```csharp
[RobotCaseHandler(RobotCaseType.BuffEffectIntegration)]
public class RobotCase_020_BuffEffectIntegration_Handler : ARobotCaseHandler
{
    protected override async ETTask Run(RobotCase robotCase, ETTask waitetype)
    {
        using Robot robot = new Robot(robotCase.Fiber, robotCase.Zone, "BuffEffectRobot");

        try
        {
            // 1. 基础初始化
            await robot.LoginAsync();
            await robot.EnterMapAsync();
            Log.Console("机器人登录并进入地图成功");

            // 2. 准备测试配置
            await PrepareBuffConfigs(robot);
            Log.Console("Buff配置准备完成");

            // 3. 添加被动Buff
            await SetupPassiveBuffs(robot);
            Log.Console("被动Buff设置完成");

            // 4. 开始战斗测试
            await ExecuteCombatWithBuffEffects(robot);
            Log.Console("战斗与Buff效果集成测试完成");

            Log.Console("RobotCase_020测试成功完成");
        }
        catch (Exception e)
        {
            Log.Error($"RobotCase_020测试失败: {e}");
            throw;
        }
    }

    private async ETTask PrepareBuffConfigs(Robot robot)
    {
        // 创建被动Buff配置
        var passiveBuffConfigs = CreatePassiveBuffConfigs();
        foreach (var config in passiveBuffConfigs)
        {
            BuffConfigCategory.Instance.Add(config);
        }

        // 创建效果Buff配置
        var effectBuffConfigs = CreateEffectBuffConfigs();
        foreach (var config in effectBuffConfigs)
        {
            BuffConfigCategory.Instance.Add(config);
        }
    }

    private List<BuffConfig> CreatePassiveBuffConfigs()
    {
        return new List<BuffConfig>
        {
            // 吸血被动Buff
            new BuffConfig
            {
                Id = 9001,
                Name = "吸血被动",
                DurationType = BuffDurationType.Permanent,
                Category = BuffCategory.Enhance,
                Effects = new List<EffectNode>
                {
                    new EffectServerBuffOnAttack
                    {
                        Children = new List<BTNode>
                        {
                            new EffectServerCreateBuff { BuffId = 9101 } // 创建治疗Buff
                        }
                    }
                }
            },

            // 反击被动Buff
            new BuffConfig
            {
                Id = 9002,
                Name = "反击被动",
                DurationType = BuffDurationType.Permanent,
                Category = BuffCategory.Enhance,
                Effects = new List<EffectNode>
                {
                    new EffectServerBuffOnAttacked
                    {
                        Children = new List<BTNode>
                        {
                            new EffectServerCreateBuff { BuffId = 9102 } // 创建反击伤害Buff
                        }
                    }
                }
            }
        };
    }

    private async ETTask SetupPassiveBuffs(Robot robot)
    {
        // 获取服务端单位
        string mapName = robot.Root.CurrentScene().Name;
        Fiber map = robot.Fiber.GetFiber("MapManager").GetFiber(mapName);
        Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
        Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);

        // 添加被动Buff（不是技能！）
        Buff vampireBuff = BuffHelper.CreateBuff(playerUnit, playerUnit.Id, 9001, 9001, null);
        Buff counterBuff = BuffHelper.CreateBuff(playerUnit, playerUnit.Id, 9002, 9002, null);

        Log.Console("被动Buff添加完成：吸血被动、反击被动");
    }

    private async ETTask ExecuteCombatWithBuffEffects(Robot robot)
    {
        // 创建怪物并开始战斗
        var monsters = await CreateTestMonsters(robot);

        // 开始战斗
        var combatResult = await StartCombat(robot, monsters);
        if (combatResult.Error != ErrorCore.OK)
        {
            throw new Exception($"开始战斗失败: {combatResult.Message}");
        }

        // 模拟战斗并验证Buff效果触发
        await SimulateCombatWithBuffEffects(robot);
    }

    private async ETTask SimulateCombatWithBuffEffects(Robot robot)
    {
        bool combatEnded = false;
        int buffEffectCount = 0;

        // 等待战斗结束
        while (!combatEnded)
        {
            await robot.Fiber.Root.GetComponent<TimerComponent>().WaitAsync(100);
            // 检查战斗状态和Buff效果触发情况
            // 通过日志或其他方式统计效果触发次数
        }

        // 验证Buff效果触发次数
        if (buffEffectCount == 0)
        {
            throw new Exception("没有检测到Buff效果触发");
        }

        Log.Console($"检测到 {buffEffectCount} 次Buff效果触发，测试通过");
    }
}
```

### 5.2 测试验证要点

1. **验证被动Buff存在**：检查单位是否有对应的长期Buff
2. **验证效果触发**：通过日志检查EffectNode是否在正确时机执行
3. **验证效果链**：检查一个效果是否能创建新的Buff
4. **验证客户端同步**：检查客户端是否收到对应的Buff消息和效果

## 六、实施步骤

### 第一阶段：Buff效果扩展（1天）
1. 为BuffComponent添加TriggerCombatEffect方法
2. 定义CombatTriggerType枚举
3. 创建EffectServerBuffCombat效果节点

### 第二阶段：战斗系统集成（2天）
1. 修改TurnBasedCombatComponentSystem，直接调用Buff方法
2. 在攻击流程中触发Buff的战斗效果
3. 实现各种战斗时机的Buff效果触发

### 第三阶段：测试验证（2天）
1. 创建RobotCase_020基础Buff效果测试
2. 验证被动Buff的正确添加和效果触发
3. 测试各种战斗场景下的Buff效果链

## 七、关键注意事项

### 7.1 架构原则
1. **技能即Buff**：技能系统只负责创建Buff，不处理效果
2. **EffectNode驱动**：所有效果通过EffectNode实现
3. **被动能力即长期Buff**：不需要额外的被动技能管理系统

### 7.2 性能考虑
1. **Buff数量控制**：避免单位拥有过多的被动Buff
2. **效果执行优化**：合理安排EffectNode的执行顺序
3. **内存管理**：及时清理临时效果Buff

### 7.3 扩展性设计
1. **配置驱动**：所有效果通过配置EffectNode实现
2. **模块化**：新增效果类型只需添加新的EffectNode
3. **向后兼容**：保持现有Buff系统API不变

## 八、预期成果

1. **正确的架构理解**：技能即Buff，EffectNode执行效果
2. **灵活的被动能力**：通过预添加Buff实现各种被动效果
3. **完整的战斗集成**：战斗系统正确触发Buff的EffectNode
4. **全面的测试覆盖**：验证Buff效果在各种时机的正确执行
5. **良好的扩展性**：易于添加新的效果类型和触发时机
6. **客户端同步**：完整的客户端效果处理和视觉反馈

## 九、战斗专用Buff系统（CombatOnly类型）

### 9.1 CombatOnly类型概述

**定义**：CombatOnly是一种特殊的BuffDurationType，仅在战斗期间生效的Buff类型。

**核心特点**：
- ✅ **战斗激活**：战斗开始时自动激活
- ✅ **战斗持续**：整场战斗期间持续存在
- ✅ **自动清理**：战斗结束时自动移除
- ✅ **状态感知**：能够感知战斗状态变化

### 9.2 枚举定义

```csharp
public enum BuffDurationType
{
    Auto = -1,       // 自动选择（根据战斗状态）
    Time = 0,        // 时间制（默认）
    Turn = 1,        // 回合制
    Permanent = 2,   // 永久
    Hybrid = 3,      // 混合模式（同时支持时间和回合）
    CombatOnly = 4   // 仅战斗期间（战斗开始时激活，战斗结束时移除）
}
```

### 9.3 适用场景

#### 9.3.1 战斗增益效果
```csharp
// 战斗药剂：进入战斗时生效，战斗结束时失效
BuffConfig combatPotionConfig = new BuffConfig
{
    Id = 8001,
    Name = "战斗药剂",
    DurationType = BuffDurationType.CombatOnly,
    Category = BuffCategory.Enhance,
    Effects = new List<EffectNode>
    {
        new EffectServerBuffAdd
        {
            // 增加攻击力20%
            Children = new List<BTNode>
            {
                new EffectServerModifyAttribute { Attribute = NumericType.Attack, Value = 0.2f, IsPercent = true }
            }
        }
    }
};
```

#### 9.3.2 战斗光环效果
```csharp
// 队长光环：战斗期间为队友提供加成
BuffConfig leaderAuraConfig = new BuffConfig
{
    Id = 8002,
    Name = "队长光环",
    DurationType = BuffDurationType.CombatOnly,
    Category = BuffCategory.Enhance,
    Effects = new List<EffectNode>
    {
        new EffectServerBuffCombatStart
        {
            // 战斗开始时为队友添加增益
            Children = new List<BTNode>
            {
                new EffectServerCreateTeamBuff { BuffId = 8003, Range = 5.0f }
            }
        }
    }
};
```

#### 9.3.3 战斗状态标记
```csharp
// 战斗模式：标记单位进入战斗状态
BuffConfig combatModeConfig = new BuffConfig
{
    Id = 8004,
    Name = "战斗模式",
    DurationType = BuffDurationType.CombatOnly,
    Category = BuffCategory.Other,
    Tags = BuffTag.Hidden, // 隐藏，不显示UI
    Effects = new List<EffectNode>
    {
        new EffectServerBuffAdd
        {
            Children = new List<BTNode>
            {
                new EffectServerSetCombatState { InCombat = true }
            }
        }
    }
};
```

### 9.4 生命周期管理

#### 9.4.1 战斗状态检测
```csharp
public static class BuffCombatExtension
{
    /// <summary>
    /// 检查是否在战斗中
    /// </summary>
    public static bool IsInCombat(this BuffComponent self)
    {
        Unit unit = self.GetParent<Unit>();
        if (unit == null) return false;

        // 通过TurnBasedCombatComponent检测战斗状态
        TurnBasedCombatComponent combatComponent = unit.GetComponent<TurnBasedCombatComponent>();
        return combatComponent != null && combatComponent.InCombat;
    }
}
```

#### 9.4.2 战斗开始处理
```csharp
/// <summary>
/// 战斗开始时的处理
/// </summary>
public static void OnCombatStart(this BuffComponent self)
{
    Log.Debug($"Combat started, activating CombatOnly buffs for unit {self.GetParent<Unit>()?.Id}");

    // 激活所有CombatOnly类型的Buff
    foreach (var child in self.Children.Values)
    {
        if (child is Buff buff && buff.DurationType == BuffDurationType.CombatOnly)
        {
            // CombatOnly Buff在战斗开始时激活，但不重复触发OnAdd效果
            Log.Debug($"Activating CombatOnly buff: {buff.ConfigId}");

            // 执行战斗开始效果
            EffectServerBuffCombatStart effect = buff.GetConfig().GetEffect<EffectServerBuffCombatStart>();
            if (effect != null)
            {
                using BTEnv env = BTEnv.Create(buff.Scene());
                env.AddEntity(effect.Buff, buff);
                env.AddEntity(effect.Unit, self.GetParent<Unit>());
                env.AddEntity(effect.Caster, buff.GetCaster());

                BTDispatcher.Instance.Handle(effect, env);
            }
        }
    }
}
```

#### 9.4.3 战斗结束处理
```csharp
/// <summary>
/// 战斗结束时的处理
/// </summary>
public static void OnCombatEnd(this BuffComponent self)
{
    Log.Debug($"Combat ended, removing CombatOnly buffs for unit {self.GetParent<Unit>()?.Id}");

    // 收集需要移除的CombatOnly Buff
    List<Buff> combatOnlyBuffs = new();
    foreach (var child in self.Children.Values)
    {
        if (child is Buff buff && buff.DurationType == BuffDurationType.CombatOnly)
        {
            combatOnlyBuffs.Add(buff);
        }
    }

    // 移除所有CombatOnly类型的Buff
    foreach (var buff in combatOnlyBuffs)
    {
        Log.Debug($"Removing CombatOnly buff: {buff.ConfigId}");
        BuffHelper.RemoveBuff(buff, BuffFlags.CombatEndRemove);
    }
}
```

### 9.5 战斗系统集成

#### 9.5.1 战斗开始集成
```csharp
// 在TurnBasedCombatComponentSystem.StartCombatAsync中添加
public static async ETTask StartCombatAsync(this TurnBasedCombatComponent self,
    Unit player, List<Unit> monsters)
{
    self.InCombat = true;
    self.RoundNumber = 1;
    // ... 其他初始化逻辑

    // 通知所有参战单位战斗开始
    player.GetComponent<BuffComponent>()?.OnCombatStart();
    foreach (var monster in monsters)
    {
        monster.GetComponent<BuffComponent>()?.OnCombatStart();
    }

    Log.Debug($"Combat started, activated CombatOnly buffs for all units");

    // 开始战斗循环
    await self.RunCombatLoop();
}
```

#### 9.5.2 战斗结束集成
```csharp
// 在TurnBasedCombatComponentSystem.EndCombat中添加
public static async ETTask EndCombat(this TurnBasedCombatComponent self)
{
    self.InCombat = false;

    // 通知所有单位战斗结束
    var unitComponent = self.Root().GetComponent<UnitComponent>();

    // 通知玩家
    Unit player = unitComponent.Get(self.PlayerUnitId);
    player?.GetComponent<BuffComponent>()?.OnCombatEnd();

    // 通知怪物
    foreach (long monsterId in self.MonsterUnitIds)
    {
        Unit monster = unitComponent.Get(monsterId);
        monster?.GetComponent<BuffComponent>()?.OnCombatEnd();
    }

    Log.Debug($"Combat ended, removed CombatOnly buffs for all units");

    // ... 其他清理逻辑
}
```

### 9.6 测试用例设计

#### 9.6.1 RobotCase_025_CombatOnlyBuffs
```csharp
[RobotCaseHandler(RobotCaseType.CombatOnlyBuffs)]
public class RobotCase_025_CombatOnlyBuffs_Handler : ARobotCaseHandler
{
    protected override async ETTask Run(RobotCase robotCase, ETTask waitetype)
    {
        using Robot robot = new Robot(robotCase.Fiber, robotCase.Zone, "CombatOnlyBuffRobot");

        try
        {
            // 1. 基础初始化
            await robot.LoginAsync();
            await robot.EnterMapAsync();
            Log.Console("机器人登录并进入地图成功");

            // 2. 准备CombatOnly Buff配置
            await PrepareCombatOnlyBuffConfigs(robot);
            Log.Console("CombatOnly Buff配置准备完成");

            // 3. 战斗前添加CombatOnly Buff
            await AddCombatOnlyBuffsBeforeCombat(robot);
            Log.Console("战斗前CombatOnly Buff添加完成");

            // 4. 验证战斗前Buff状态（应该存在但未激活）
            await VerifyBuffStateBeforeCombat(robot);
            Log.Console("战斗前Buff状态验证通过");

            // 5. 开始战斗
            await StartCombatAndVerifyActivation(robot);
            Log.Console("战斗开始，Buff激活验证通过");

            // 6. 战斗中验证Buff效果
            await VerifyBuffEffectsDuringCombat(robot);
            Log.Console("战斗中Buff效果验证通过");

            // 7. 结束战斗并验证清理
            await EndCombatAndVerifyCleanup(robot);
            Log.Console("战斗结束，Buff清理验证通过");

            Log.Console("RobotCase_025测试成功完成");
        }
        catch (Exception e)
        {
            Log.Error($"RobotCase_025测试失败: {e}");
            throw;
        }
    }

    private async ETTask PrepareCombatOnlyBuffConfigs(Robot robot)
    {
        // 创建战斗药剂Buff配置
        var combatPotionConfig = new BuffConfig
        {
            Id = 8001,
            Name = "战斗药剂",
            DurationType = BuffDurationType.CombatOnly,
            Category = BuffCategory.Enhance,
            Effects = new List<EffectNode>
            {
                new EffectServerBuffAdd
                {
                    Children = new List<BTNode>
                    {
                        new EffectServerLog { Message = "战斗药剂生效：攻击力+20%" }
                    }
                }
            }
        };

        BuffConfigCategory.Instance.Add(combatPotionConfig);
    }

    private async ETTask VerifyBuffStateBeforeCombat(Robot robot)
    {
        // 获取服务端单位
        string mapName = robot.Root.CurrentScene().Name;
        Fiber map = robot.Fiber.GetFiber("MapManager").GetFiber(mapName);
        Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
        Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);

        // 验证CombatOnly Buff存在
        BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
        var combatOnlyBuffs = buffComponent.GetCombatOnlyBuffs();

        if (combatOnlyBuffs.Count == 0)
        {
            throw new Exception("战斗前应该有CombatOnly Buff");
        }

        // 验证未在战斗中
        if (buffComponent.IsInCombat())
        {
            throw new Exception("战斗前不应该处于战斗状态");
        }

        Log.Console($"验证通过：存在{combatOnlyBuffs.Count}个CombatOnly Buff，当前非战斗状态");
    }

    private async ETTask StartCombatAndVerifyActivation(Robot robot)
    {
        // 创建怪物并开始战斗
        var monsters = await CreateTestMonsters(robot);
        var combatResult = await StartCombat(robot, monsters);

        if (combatResult.Error != ErrorCore.OK)
        {
            throw new Exception($"开始战斗失败: {combatResult.Message}");
        }

        // 验证战斗状态
        string mapName = robot.Root.CurrentScene().Name;
        Fiber map = robot.Fiber.GetFiber("MapManager").GetFiber(mapName);
        Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
        Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);

        BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
        if (!buffComponent.IsInCombat())
        {
            throw new Exception("战斗开始后应该处于战斗状态");
        }

        Log.Console("验证通过：战斗开始，进入战斗状态");
    }

    private async ETTask EndCombatAndVerifyCleanup(Robot robot)
    {
        // 等待战斗结束
        await WaitForCombatEnd(robot);

        // 验证CombatOnly Buff已被清理
        string mapName = robot.Root.CurrentScene().Name;
        Fiber map = robot.Fiber.GetFiber("MapManager").GetFiber(mapName);
        Client.PlayerComponent playerComponent = robot.Root.GetComponent<Client.PlayerComponent>();
        Unit playerUnit = map.Root.GetComponent<UnitComponent>().Get(playerComponent.MyId);

        BuffComponent buffComponent = playerUnit.GetComponent<BuffComponent>();
        var combatOnlyBuffs = buffComponent.GetCombatOnlyBuffs();

        if (combatOnlyBuffs.Count > 0)
        {
            throw new Exception("战斗结束后CombatOnly Buff应该被清理");
        }

        if (buffComponent.IsInCombat())
        {
            throw new Exception("战斗结束后应该退出战斗状态");
        }

        Log.Console("验证通过：战斗结束，CombatOnly Buff已清理，退出战斗状态");
    }
}
```

### 9.7 技术要点和注意事项

#### 9.7.1 性能优化
- 缓存CombatOnly类型的Buff列表，避免每次遍历
- 批量处理战斗状态变化，减少重复操作
- 合理控制CombatOnly Buff的数量

#### 9.7.2 边界条件处理
- 战斗异常中断时的Buff清理
- 多次快速进入/退出战斗的状态管理
- 跨Fiber的战斗状态同步

#### 9.7.3 兼容性考虑
- 与现有Buff系统的完全兼容
- 不影响其他BuffDurationType的正常运作
- 支持CombatOnly与其他类型的组合使用

## 十、后续工作

### 9.1 效果丰富化
- 更多类型的EffectNode实现
- 复杂的效果组合和链式反应
- 条件触发和概率触发

### 9.2 性能优化
- Buff效果执行批处理
- 效果计算结果缓存
- 客户端特效资源预加载

### 9.3 工具支持
- Buff效果配置编辑器
- 效果触发调试工具
- 客户端效果预览工具

---

*文档版本：7.0（分离EffectNode架构版）*
*更新内容：*
- *完全重写架构理解，明确技能即Buff原理*
- *移除错误的事件触发机制，改为直接调用Buff方法*
- *修正被动技能概念，改为被动Buff实现*
- *重写测试用例，验证Buff效果而不是技能触发*
- *更新客户端处理方式，通过EffectNode处理效果*
- *新增CombatOnly BuffDurationType战斗专用Buff系统*
- *添加完整的战斗状态管理和生命周期处理*
- *提供详细的使用场景和测试用例*
- *【新增】分离EffectNode架构：将单一EffectServerBuffCombat拆分为多个独立的EffectNode*
- *【新增】性能优化：移除TriggerType枚举和foreach循环，改为直接检查对应EffectNode*
- *【新增】按需配置：技能配置时需要什么触发就挂载什么EffectNode，提高配置灵活性*
- *【新增】代码清晰：每个EffectNode功能单一职责明确，易于维护和扩展*
*创建日期：2024年*