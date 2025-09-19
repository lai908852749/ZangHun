# ET框架回合制战斗系统与Buff系统集成测试计划

## 一、项目背景

### 1.1 现状分析
- **回合制战斗系统**：已在`cn.etetet.wow`包中实现基础回合制战斗流程
- **Buff系统**：已在`cn.etetet.spell`包中实现完整的Buff机制，包含回合制支持
- **测试用例**：已完成10个Buff单项测试（RobotCase_010-019）
- **集成问题**：两个系统相对独立，缺少战斗过程中的Buff触发机制

### 1.2 核心需求
1. 战斗系统与Buff系统无缝集成
2. 支持丰富的战斗触发时机
3. 保持系统解耦，使用事件系统通信
4. 完善的测试验证体系

## 二、系统架构设计

### 2.1 包结构与职责

#### cn.etetet.wow (第5层，游戏业务包)
- **职责**：战斗逻辑、游戏流程控制
- **核心组件**：
  - TurnBasedCombatComponent：回合制战斗主组件
  - CombatRecordComponent：战斗记录组件
- **不依赖spell包**，通过事件系统通信

#### cn.etetet.spell (第5层，技能系统包)
- **职责**：Buff系统、技能系统、效果处理
- **核心组件**：
  - BuffComponent：Buff管理组件
  - BuffTurnBasedSystem：回合制Buff处理
  - BuffHelper：Buff辅助功能
- **监听战斗事件**，触发相应Buff效果

#### cn.etetet.robotcase (第5层，测试包)
- **职责**：所有测试用例的集中管理
- **可访问**：所有第5层包（通过AllowSameLevelAccess）

### 2.2 关键发现
- **BuffComponent初始化**：已在`UnitFactory.cs`中为所有Unit自动添加，无需重复初始化
- **事件系统示例**：参考`InputSystemComponentSystem.CastSpell`方法使用`EventSystem.Instance.Publish`

## 三、集成方案设计

### 3.1 事件驱动架构

#### 3.1.1 战斗事件定义（cn.etetet.wow包）
```csharp
// 攻击事件
public struct OnCombatAttack
{
    public EntityRef<Unit> Attacker;  // 攻击者
    public EntityRef<Unit> Target;    // 目标
    public int Damage;                // 伤害值
    public DamageType Type;           // 伤害类型(Physical/Magic)
}

// 防御事件
public struct OnCombatDefend
{
    public EntityRef<Unit> Defender;   // 防御者
    public EntityRef<Unit> Attacker;   // 攻击者
    public DefenseResult Result;       // 防御结果(Block/Dodge/Parry)
}

// 暴击事件
public struct OnCombatCritical
{
    public EntityRef<Unit> Attacker;   // 攻击者
    public EntityRef<Unit> Target;     // 目标
    public int CriticalDamage;        // 暴击伤害
}

// 生命值阈值事件
public struct OnHealthThreshold
{
    public EntityRef<Unit> Unit;       // 单位
    public float Percentage;           // 当前百分比
    public ThresholdType Type;         // 阈值类型(Below25/Below50等)
}

// 回合事件
public struct OnTurnStart
{
    public EntityRef<Unit> Unit;       // 行动单位
    public int RoundNumber;            // 回合数
}

public struct OnTurnEnd
{
    public EntityRef<Unit> Unit;       // 行动单位
    public int RoundNumber;            // 回合数
}
```

#### 3.1.2 事件监听器（cn.etetet.spell包）
```csharp
[Event(SceneType.Map)]
public class OnCombatAttack_BuffTrigger : AEvent<Scene, OnCombatAttack>
{
    protected override async ETTask Run(Scene scene, OnCombatAttack args)
    {
        Unit attacker = args.Attacker;
        Unit target = args.Target;

        // 触发攻击者的"攻击时"Buff
        BuffComponent attackerBuff = attacker?.GetComponent<BuffComponent>();
        attackerBuff?.TriggerBuffs(BuffTriggerType.OnAttack, args);

        // 触发目标的"被攻击时"Buff
        BuffComponent targetBuff = target?.GetComponent<BuffComponent>();
        targetBuff?.TriggerBuffs(BuffTriggerType.OnAttacked, args);
    }
}
```

### 3.2 Buff触发机制扩展

#### 3.2.1 触发时机分类

**战斗触发类**：
1. **攻击时触发**
   - OnPhysicalAttack - 发起物理攻击时
   - OnMagicAttack - 发起魔法攻击时
   - OnWeaponAttack - 使用武器攻击时
   - OnWeaponHit - 武器击中目标时

2. **受击时触发**
   - OnPhysicalAttacked - 受到物理攻击时
   - OnMagicAttacked - 受到魔法攻击时
   - OnCriticalReceived - 受到暴击时

3. **防御触发**
   - OnBlockSuccess - 格挡成功时
   - OnBlockedByTarget - 被目标格挡时
   - OnDodgeSuccess - 闪避成功时
   - OnMissedTarget - 攻击未命中时
   - OnParrySuccess - 招架成功时

4. **特殊触发**
   - OnCriticalStrike - 造成暴击时
   - OnHealthBelow25 - 生命值低于25%时
   - OnHealthBelow50 - 生命值低于50%时
   - OnManaBelow30 - 法力值低于30%时

#### 3.2.2 参数说明

每个触发节点的标准参数：
- **Buff**: 当前Buff实例（触发的Buff本身）
- **Unit**: 拥有Buff的单位（Buff宿主）
- **Caster**: Buff施加者（谁给Unit加的Buff）
- **Attacker**: 攻击者（发起攻击的单位）
- **Target**: 目标（被攻击的单位）

**关系说明**：
- 当Unit攻击别人时：Unit == Attacker
- 当Unit被攻击时：Unit == Target
- Unit始终是Buff的宿主，Attacker/Target根据战斗情况变化

#### 3.2.3 新增EffectNode类型

```csharp
// 位置：cn.etetet.spell/Scripts/Model/Share/Root/

// 物理攻击触发
public class EffectServerBuffOnPhysicalAttacked : EffectNode
{
    public string Buff = "Buff";
    public string Unit = "Unit";
    public string Caster = "Caster";
    public string Attacker = "Attacker";
    public string Damage = "Damage";
}

// 魔法攻击触发
public class EffectServerBuffOnMagicAttacked : EffectNode
{
    public string Buff = "Buff";
    public string Unit = "Unit";
    public string Caster = "Caster";
    public string Attacker = "Attacker";
    public string Damage = "Damage";
}

// 格挡成功触发
public class EffectServerBuffOnBlockSuccess : EffectNode
{
    public string Buff = "Buff";
    public string Unit = "Unit";
    public string Caster = "Caster";
    public string Attacker = "Attacker";
    public string BlockedDamage = "BlockedDamage";
}

// 暴击触发
public class EffectServerBuffOnCritical : EffectNode
{
    public string Buff = "Buff";
    public string Unit = "Unit";
    public string Caster = "Caster";
    public string Target = "Target";
    public string CriticalDamage = "CriticalDamage";
}

// 生命阈值触发
public class EffectServerBuffOnHealthThreshold : EffectNode
{
    public string Buff = "Buff";
    public string Unit = "Unit";
    public string Caster = "Caster";
    public string CurrentHP = "CurrentHP";
    public string MaxHP = "MaxHP";
    public string Percentage = "Percentage";
}
```

## 四、战斗系统改造方案

### 4.1 TurnBasedCombatComponentSystem增强

#### 4.1.1 回合循环集成
```csharp
private static async ETTask RunCombatLoop(this TurnBasedCombatComponent self)
{
    while (self.InCombat)
    {
        for (int i = 0; i < self.TurnOrder.Count; i++)
        {
            long actorId = self.TurnOrder[i];
            Unit actor = self.Root.GetComponent<UnitComponent>().Get(actorId);

            // 获取BuffComponent（已在UnitFactory中初始化）
            BuffComponent buffComponent = actor?.GetComponent<BuffComponent>();

            // 发布回合开始事件
            EventSystem.Instance.Publish(self.Scene(), new OnTurnStart
            {
                Unit = actor,
                RoundNumber = self.RoundNumber
            });

            // 触发Buff回合开始效果
            buffComponent?.OnTurnStart(self.RoundNumber);

            // 处理单位行动
            if (actorId == self.PlayerUnitId)
            {
                await self.ProcessPlayerTurn(actor);
            }
            else
            {
                await self.ProcessMonsterTurn(actor);
            }

            // 触发Buff回合结束效果
            buffComponent?.OnTurnEnd(self.RoundNumber);

            // 发布回合结束事件
            EventSystem.Instance.Publish(self.Scene(), new OnTurnEnd
            {
                Unit = actor,
                RoundNumber = self.RoundNumber
            });
        }

        self.RoundNumber++;
    }
}
```

#### 4.1.2 攻击流程增强
```csharp
private static async ETTask ExecuteAttack(this TurnBasedCombatComponent self,
    Unit attacker, Unit target)
{
    var attackerNum = attacker.GetComponent<NumericComponent>();
    var targetNum = target.GetComponent<NumericComponent>();

    // 1. 计算基础伤害
    int baseDamage = attackerNum.GetAsInt(NumericType.Attack);
    DamageType damageType = DamageType.Physical; // 根据实际情况判断

    // 2. 发布攻击前事件
    EventSystem.Instance.Publish(self.Scene(), new OnCombatAttack
    {
        Attacker = attacker,
        Target = target,
        Damage = baseDamage,
        Type = damageType
    });

    // 3. 判定命中/闪避/格挡/暴击
    CombatResult result = CalculateCombatResult(attacker, target);

    // 4. 根据判定结果处理
    switch (result.Type)
    {
        case ResultType.Hit:
            // 正常命中
            ApplyDamage(target, result.FinalDamage);
            break;

        case ResultType.Critical:
            // 暴击
            EventSystem.Instance.Publish(self.Scene(), new OnCombatCritical
            {
                Attacker = attacker,
                Target = target,
                CriticalDamage = result.FinalDamage
            });
            ApplyDamage(target, result.FinalDamage);
            break;

        case ResultType.Block:
            // 被格挡
            EventSystem.Instance.Publish(self.Scene(), new OnCombatDefend
            {
                Defender = target,
                Attacker = attacker,
                Result = DefenseResult.Block
            });
            break;

        case ResultType.Dodge:
            // 被闪避
            EventSystem.Instance.Publish(self.Scene(), new OnCombatDefend
            {
                Defender = target,
                Attacker = attacker,
                Result = DefenseResult.Dodge
            });
            break;
    }

    // 5. 检查生命值阈值
    int currentHP = targetNum.GetAsInt(NumericType.HP);
    int maxHP = targetNum.GetAsInt(NumericType.MaxHP);
    float percentage = (float)currentHP / maxHP;

    if (percentage < 0.25f)
    {
        EventSystem.Instance.Publish(self.Scene(), new OnHealthThreshold
        {
            Unit = target,
            Percentage = percentage,
            Type = ThresholdType.Below25
        });
    }
}
```

## 五、测试用例设计

### 5.1 测试用例规划

#### 现有测试（已完成）
- RobotCase_009：基础回合制战斗
- RobotCase_010：Buff基础回合制功能
- RobotCase_011：Buff标签系统
- RobotCase_012：Buff互斥组
- RobotCase_013：Buff免疫系统
- RobotCase_014：Buff效果节点
- RobotCase_015：Buff渐进效果
- RobotCase_016：Buff传播机制
- RobotCase_017：伤害累积
- RobotCase_018：治疗累积
- RobotCase_019：复杂场景综合测试

#### 新增集成测试（待实现）
- **RobotCase_020_CombatWithBuff**：战斗中的Buff基础集成
- **RobotCase_021_AttackTriggers**：攻击触发测试
- **RobotCase_022_DefenseTriggers**：防御触发测试
- **RobotCase_023_ThresholdTriggers**：阈值触发测试
- **RobotCase_024_ChainTriggers**：连锁触发测试
- **RobotCase_025_ComplexCombat**：复杂战斗场景

### 5.2 RobotCase_020测试场景设计

```csharp
测试目标：验证战斗与Buff系统的基础集成

测试步骤：
1. 创建玩家和2个怪物进入战斗
2. 战斗开始前给玩家添加以下Buff：
   - 回春术（每回合恢复HP）
   - 物理攻击增强（攻击力+20%）
   - 反击（被攻击时30%概率反击）
3. 第一回合：
   - 验证回春术在回合开始触发
   - 玩家攻击怪物A，验证攻击力增强
   - 怪物A攻击玩家，验证反击触发
4. 第二回合：
   - 怪物B攻击触发玩家中毒Buff
   - 验证中毒在回合结束时造成伤害
5. 验证所有Buff的触发时机和效果

预期结果：
- 每回合正确触发OnTurnStart/OnTurnEnd
- 攻击增强Buff正确计算伤害
- 反击Buff在被攻击时触发
- 中毒Buff每回合结束造成伤害
- 所有Buff状态正确同步到客户端
```

### 5.3 测试验证要点

1. **功能验证**
   - 各种触发时机的正确性
   - Buff效果的准确计算
   - 事件发布与监听的完整性
   - 客户端消息同步

2. **性能验证**
   - 20+个Buff同时存在的性能
   - 大量事件触发的处理效率
   - 内存使用和GC情况

3. **边界条件**
   - Unit死亡时的Buff清理
   - 战斗中断时的状态处理
   - RemainTurn=0的边界情况
   - 同时触发多个Buff的优先级

## 六、实施步骤

### 第一阶段：基础集成（2天）
1. ✅ 分析现有系统结构
2. ✅ 设计事件系统集成方案
3. 在TurnBasedCombatComponentSystem中集成BuffComponent回合处理
4. 创建基础战斗事件定义

### 第二阶段：触发机制实现（3天）
1. 创建所有触发时机的EffectNode
2. 实现事件监听器
3. 增强ExecuteAttack方法
4. 实现战斗结果判定逻辑

### 第三阶段：测试验证（2天）
1. 创建RobotCase_020基础集成测试
2. 创建其他触发机制测试
3. 性能测试和优化
4. Bug修复和完善

## 七、注意事项

### 7.1 技术要点
1. **使用事件系统解耦**：战斗系统和Buff系统通过事件通信，避免直接依赖
2. **BuffComponent已初始化**：在UnitFactory中已为所有Unit添加，无需重复初始化
3. **消息同步**：使用MapMessageHelper.NoticeClient确保Buff状态同步
4. **EntityRef使用**：事件中使用EntityRef避免Entity失效问题

### 7.2 性能优化
1. **批量处理**：多个Buff同时触发时批量处理
2. **缓存优化**：频繁访问的配置数据缓存
3. **对象池**：事件对象使用对象池减少GC

### 7.3 扩展性考虑
1. **配置驱动**：触发条件通过配置而非硬编码
2. **模块化设计**：新增触发类型只需添加EffectNode和事件处理器
3. **向后兼容**：保持现有API不变，通过扩展实现新功能

## 八、风险与对策

### 风险1：事件触发顺序问题
- **风险**：多个事件同时触发时的执行顺序不确定
- **对策**：实现事件优先级机制，确保关键事件优先处理

### 风险2：性能瓶颈
- **风险**：大量Buff和事件可能造成性能问题
- **对策**：使用批处理、缓存和对象池优化

### 风险3：测试覆盖不足
- **风险**：复杂的触发组合可能存在未测试的场景
- **对策**：设计全面的测试矩阵，确保所有组合都被覆盖

## 九、预期成果

1. **功能完整性**：实现战斗与Buff系统的完整集成
2. **系统解耦**：通过事件系统保持模块独立性
3. **扩展性强**：易于添加新的触发机制和Buff效果
4. **测试完善**：全面的测试用例保证系统稳定性
5. **性能良好**：优化后能支持复杂战斗场景

## 十、后续优化方向

1. **AI集成**：让怪物AI能够智能使用Buff
2. **技能系统集成**：将技能释放与Buff系统结合
3. **视觉效果**：添加Buff触发的特效表现
4. **数据分析**：收集Buff触发数据用于平衡性调整
5. **配置工具**：开发可视化的Buff配置编辑器

---

*文档版本：1.0*
*创建日期：2024年*
*作者：Claude AI助手*
*项目：ET框架回合制战斗系统*