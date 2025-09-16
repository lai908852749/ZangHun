# ET框架回合制战斗系统实施方案

## 一、项目背景

本项目是基于ET 9.0框架的MMO游戏项目，现需要在保留原有MMO实时战斗系统的基础上，添加回合制战斗系统。经过分析，决定在现有包结构中直接添加回合制功能，而不是创建新的包。

## 二、现状分析

### 2.1 现有战斗相关包结构

项目中战斗相关功能分布在以下包中：

1. **cn.etetet.spell** - 技能系统包
   - 包含Buff系统、技能组件、伤害计算（DamageHelper）
   - 已有完整的技能冷却、修正值、施法逻辑

2. **cn.etetet.numeric** - 数值系统包
   - NumericComponent管理所有数值
   - NumericType定义数值常量
   - 支持Base/Add/Pct/FinalAdd/FinalPct修正体系

3. **cn.etetet.unit** - 单位系统包
   - Unit基础实体定义
   - 单位组件管理

4. **cn.etetet.ai** - AI系统包
   - AI行为控制
   - 可扩展用于回合制AI

5. **cn.etetet.wow** - 游戏主包
   - 包含游戏特有逻辑
   - Map相关功能
   - 宠物系统

6. **cn.etetet.btnode** - 行为树节点包
   - BTDamageHandler调用DamageHelper

### 2.2 需要保留的MMO系统

- **DamageHelper.cs**: MMO实时战斗伤害计算，保持不变
- **BuffComponent**: 基于时间的Buff系统，不需要转换为回合制
- **SpellComponent**: 技能系统，通过接口集成到回合制
- **NumericComponent**: 数值管理核心，需要扩展新数值类型

## 三、回合制战斗系统设计

### 3.1 文件夹结构规划

```
cn.etetet.wow/Scripts/
├── Model/
│   ├── Share/Map/Combat/              # 新建Combat文件夹
│   │   ├── TurnBasedCombatComponent.cs
│   │   ├── CombatRandomComponent.cs
│   │   ├── CombatRecordComponent.cs
│   │   ├── CombatUnitInfo.cs
│   │   └── CombatPhase.cs
│   └── Server/Combat/                  # 服务器专用组件
│       └── CombatValidationComponent.cs
├── Hotfix/
│   └── Server/Combat/
│       ├── TurnBasedCombatComponentSystem.cs
│       ├── CombatRandomComponentSystem.cs
│       ├── CombatRecordComponentSystem.cs
│       ├── TurnBasedCombatHelper.cs
│       ├── CombatDamageHelper.cs
│       └── Handler/
│           ├── C2M_EnterTurnBasedCombatHandler.cs
│           ├── C2M_TurnBasedActionHandler.cs
│           └── C2M_ExitTurnBasedCombatHandler.cs
└── Proto/
    └── WOW_Combat_C_10700.proto

cn.etetet.numeric/Scripts/Model/Share/
└── NumericType.cs                     # 扩展添加新数值类型

cn.etetet.ai/Scripts/
├── Model/Share/Combat/
│   └── TurnBasedAIComponent.cs
└── Hotfix/Server/Combat/
    └── TurnBasedAIComponentSystem.cs
```

### 3.2 核心组件定义

#### TurnBasedCombatComponent（战斗管理组件）

```csharp
namespace ET
{
    [ComponentOf(typeof(Scene))]
    public class TurnBasedCombatComponent : Entity, IAwake, IDestroy
    {
        // 战斗基础状态
        public bool InCombat { get; set; }
        public int RoundNumber { get; set; }
        public CombatPhase Phase { get; set; }

        // 参战单位管理
        public List<long> TurnOrder = new();
        public Dictionary<long, CombatUnitInfo> UnitInfos = new();
        public int CurrentTurnIndex { get; set; }

        // 随机数种子（用于同步）
        public int RandomSeed { get; set; }

        // 战斗配置
        public int MaxRounds { get; set; } = 30;
        public long TurnTimeout { get; set; } = 30000; // 30秒超时

        // 回合计时器
        public long TurnTimer { get; set; }

        // 战斗记录引用
        public EntityRef<CombatRecordComponent> RecordRef { get; set; }
    }
}
```

#### CombatRandomComponent（随机数管理）

```csharp
namespace ET
{
    [ChildOf(typeof(TurnBasedCombatComponent))]
    public class CombatRandomComponent : Entity, IAwake<int>
    {
        public int Seed { get; set; }
        public System.Random Random { get; set; }
    }
}
```

#### CombatRecordComponent（战斗记录）

```csharp
namespace ET
{
    [ChildOf(typeof(TurnBasedCombatComponent))]
    public class CombatRecordComponent : Entity, IAwake, IDestroy
    {
        public List<CombatAction> Actions = new();
        public long CombatStartTime { get; set; }
        public long CombatEndTime { get; set; }
        public int TotalRounds { get; set; }
        public bool IsValidated { get; set; }
    }
}
```

### 3.3 数值类型扩展

在 `NumericType.cs` 中添加：

```csharp
// 回合制战斗相关数值
public const int Attack = 2001;           // 攻击力
public const int AttackBase = 20011;
public const int AttackAdd = 20012;
public const int AttackPct = 20013;
public const int AttackFinalAdd = 20014;
public const int AttackFinalPct = 20015;

public const int Defense = 2002;          // 防御力
public const int DefenseBase = 20021;
public const int DefenseAdd = 20022;
public const int DefensePct = 20023;
public const int DefenseFinalAdd = 20024;
public const int DefenseFinalPct = 20025;

public const int CritRate = 2003;         // 暴击率
public const int DodgeRate = 2004;        // 闪避率
public const int BlockRate = 2005;        // 格挡率
public const int CritDamage = 2006;       // 暴击伤害倍率
```

### 3.4 消息协议定义

```protobuf
// WOW_Combat_C_10700.proto

// 请求进入回合制战斗
message C2M_EnterTurnBasedCombat // ILocationRequest
{
    int32 RpcId = 90;
    repeated int64 TargetIds = 1;  // PVP时的目标玩家
}

message M2C_EnterTurnBasedCombat // ILocationResponse
{
    int32 RpcId = 90;
    int32 Error = 91;
    string Message = 92;

    int32 RandomSeed = 1;
    repeated int64 UnitIds = 2;
    repeated CombatUnitData Units = 3;
}

// 玩家执行动作
message C2M_TurnBasedAction // ILocationRequest
{
    int32 RpcId = 90;
    int32 ActionType = 1;    // 1=普攻 2=技能 3=道具 4=跳过
    int64 TargetId = 2;
    int32 SkillId = 3;
    int32 ItemId = 4;
}

message M2C_TurnBasedAction // ILocationResponse
{
    int32 RpcId = 90;
    int32 Error = 91;
    string Message = 92;
}

// 战斗事件广播
message M2C_TurnBasedCombatEvent // ILocationMessage
{
    int32 EventType = 1;     // 1=伤害 2=治疗 3=buff 4=回合开始 5=回合结束
    int64 ActorId = 2;
    int64 TargetId = 3;
    int32 Value = 4;
    bool IsCrit = 5;
    bool IsDodge = 6;
    bool IsBlock = 7;
    int32 CurrentRound = 8;
}

// 战斗结束
message M2C_TurnBasedCombatEnd // ILocationMessage
{
    bool Win = 1;
    repeated Reward Rewards = 2;
    CombatStatistics Stats = 3;
}
```

## 四、实施计划

### 第一阶段：基础框架搭建（2天）

1. **创建文件夹结构**
   - 在wow包创建Combat文件夹
   - 在ai包创建Combat文件夹

2. **实现核心组件**
   - TurnBasedCombatComponent
   - CombatRandomComponent
   - CombatRecordComponent
   - CombatUnitInfo数据结构

3. **扩展数值系统**
   - 在NumericType.cs添加新数值常量
   - 验证NumericComponent自动支持新数值

### 第二阶段：战斗流程实现（3天）

1. **实现System类**
   - TurnBasedCombatComponentSystem（主逻辑）
   - CombatRandomComponentSystem（随机数）
   - CombatRecordComponentSystem（记录）

2. **实现战斗辅助类**
   - TurnBasedCombatHelper（战斗流程控制）
   - CombatDamageHelper（伤害计算，区别于DamageHelper）

3. **实现消息处理**
   - 定义Proto消息
   - 实现Handler类

### 第三阶段：AI系统集成（2天）

1. **实现AI组件**
   - TurnBasedAIComponent
   - TurnBasedAIComponentSystem

2. **AI决策逻辑**
   - 简化版：攻击血量最少目标
   - 预留扩展接口

### 第四阶段：技能系统集成（2天）

1. **技能接口适配**
   - 在CombatDamageHelper中预留技能接口
   - 通过伪代码方式集成SpellComponent

2. **Buff系统兼容**
   - 保持时间制Buff不变
   - 在回合制中检查Buff效果

### 第五阶段：测试与优化（3天）

1. **创建测试用例**
   - 在robotcase包创建回合制战斗测试
   - 测试基础战斗流程
   - 测试AI决策

2. **性能优化**
   - 优化行动顺序计算
   - 优化消息同步

3. **反作弊验证**
   - 实现战斗记录验证
   - 服务器端重算验证

## 五、关键技术点

### 5.1 两套战斗系统共存

- 通过检查实体是否有`TurnBasedCombatComponent`来判断战斗模式
- MMO战斗继续使用`DamageHelper`
- 回合制战斗使用`CombatDamageHelper`
- 战斗中禁用移动等MMO特性

### 5.2 随机数同步

- 服务器生成随机种子发给所有客户端
- 使用`CombatRandomComponent`统一管理随机数
- 确保客户端和服务器计算结果一致

### 5.3 等待机制

- 使用`ObjectWait`实现玩家操作等待
- 设置超时机制，超时自动跳过
- AI单位不需要等待，直接执行

### 5.4 与现有系统集成

- **NumericComponent**: 自动支持新增数值类型
- **BuffComponent**: 时间制Buff在回合中持续生效
- **SpellComponent**: 通过接口调用技能效果
- **ThreatComponent**: 回合制战斗中可选禁用仇恨

## 六、注意事项

1. **不要修改现有MMO系统**
   - DamageHelper.cs保持不变
   - 现有的Spell系统不做修改
   - Unit基础类不做修改

2. **遵循ET框架规范**
   - Entity只包含数据，不包含方法
   - System通过静态扩展方法实现逻辑
   - 使用EntityRef处理异步安全

3. **性能考虑**
   - 战斗记录要控制大小
   - 及时清理战斗实例
   - 合理使用对象池

4. **扩展性设计**
   - AI决策预留接口
   - 技能系统预留接口
   - 战斗规则可配置化

## 七、风险点

1. **消息同步延迟**
   - 风险：网络延迟导致回合超时
   - 对策：合理设置超时时间，提供断线重连

2. **随机数不一致**
   - 风险：客户端服务器计算结果不同
   - 对策：严格使用统一的随机数组件

3. **性能瓶颈**
   - 风险：大量战斗同时进行
   - 对策：使用Fiber分散到不同线程

## 八、后续优化方向

1. **功能扩展**
   - 添加更复杂的AI策略
   - 支持多人团队战斗
   - 添加战斗回放功能

2. **配置化**
   - 战斗参数Excel配置
   - AI行为配置化
   - 技能效果配置化

3. **优化体验**
   - 战斗动画优化
   - 网络优化减少延迟
   - 添加自动战斗功能

---

文档生成时间：2024年
基于ET 9.0框架（西施版本）