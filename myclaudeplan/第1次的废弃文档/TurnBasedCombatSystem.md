# ET框架回合制战斗系统 - TurnBasedCombat包

## 一、总体架构设计

### 1.1 设计原则
- **模块化设计**：创建独立的回合制战斗包
- **最小侵入**：对现有系统做最小改动
- **异步非阻塞**：使用ObjectWait机制实现玩家操作等待
- **符合ET规范**：严格遵循Entity-System分离原则

### 1.2 包职责
```
cn.etetet.turnbasedcombat (回合制战斗包)
├── 负责战斗流程管理
├── 回合顺序控制
├── 玩家操作等待
└── 调用Buff系统的回合制功能
```

## 二、包结构

```
cn.etetet.turnbasedcombat/
├── Scripts/
│   ├── Model/
│   │   ├── TurnBasedCombatComponent.cs
│   │   ├── CombatWaitTypes.cs
│   │   ├── CombatEvents.cs
│   │   ├── CombatConfig.cs
│   │   └── CombatConfigCategory.cs
│   ├── Hotfix/
│   │   ├── TurnBasedCombatComponentSystem.cs
│   │   ├── CombatHandlers.cs
│   │   └── CombatAISystem.cs
│   ├── ModelView/
│   │   └── TurnBasedCombatUIComponent.cs
│   └── HotfixView/
│       └── TurnBasedCombatUIComponentSystem.cs
```

## 三、战斗配置定义

### 3.1 CombatConfig.cs
```csharp
namespace ET
{
    [ConfigProcess(ConfigType.Bson)]
    public partial class CombatConfigCategory : Singleton<CombatConfigCategory>, ISingletonAwake, IConfig
    {
        [BsonElement]
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfArrays)]
        private Dictionary<int, CombatConfig> dict = new();

        public void Awake() { }

        public void Add(CombatConfig config)
        {
            this.dict.Add(config.Id, config);
        }

        public CombatConfig Get(int id)
        {
            this.dict.TryGetValue(id, out CombatConfig item);
            return item;
        }

        public CombatConfig GetByType(CombatType combatType)
        {
            // 根据战斗类型获取配置
            return this.dict.Values.FirstOrDefault(c => c.CombatType == combatType);
        }
    }

    [HideReferenceObjectPicker]
    [System.Serializable]
    public partial class CombatConfig : ProtoObject
    {
        [LabelText("ID")]
        public int Id;

        [LabelText("战斗类型")]
        public CombatType CombatType;

        [LabelText("最大回合数")]
        public int MaxRounds = 100;

        [LabelText("允许逃跑")]
        public bool AllowEscape = true;

        [LabelText("允许投降")]
        public bool AllowSurrender = true;

        [LabelText("回合超时时间(毫秒)")]
        public int TurnTimeout = 30000;

        [LabelText("AI决策延迟(毫秒)")]
        public int AIDecisionDelay = 1000;

        [LabelText("自动战斗")]
        public bool AutoBattle = false;
    }
}
```

### 3.2 等待类型定义
```csharp
// CombatWaitTypes.cs
namespace ET
{
    // 等待玩家操作
    public struct Wait_PlayerAction : IWaitType
    {
        public int Error { get; set; }

        // 玩家执行的动作
        public ActionType ActionType { get; set; }
        public long TargetId { get; set; }
        public int SkillId { get; set; }
        public float3 TargetPosition { get; set; }
    }

    public enum ActionType
    {
        None = 0,
        Attack = 1,
        UseSkill = 2,
        UseItem = 3,
        Defend = 4,
        Escape = 5,
        Pass = 6,      // 跳过回合
    }

    // 等待战斗结束
    public struct Wait_CombatEnd : IWaitType
    {
        public int Error { get; set; }
        public CombatEndReason Reason { get; set; }
        public bool IsWin { get; set; }
    }
}
```

## 四、TurnBasedCombatComponent定义

```csharp
[ComponentOf(typeof(Scene))]
public class TurnBasedCombatComponent : Entity, IAwake, IDestroy
{
    // === 战斗状态 ===
    public bool InCombat { get; set; }
    public int RoundNumber { get; set; }
    public CombatPhase Phase { get; set; }

    // === 参战单位管理 ===
    public HashSet<long> CombatUnits = new();
    public List<long> TurnOrder = new();
    public Dictionary<long, CombatUnitInfo> UnitInfos = new();

    // === 当前行动管理 ===
    public int CurrentTurnIndex { get; set; }
    public long CurrentActorId { get; set; }

    // === 战斗发起信息 ===
    public long InitiatorId { get; set; }
    public long TargetId { get; set; }
    public CombatType CombatType { get; set; }

    // === 战斗配置引用 ===
    public int CombatConfigId { get; set; }  // 配置ID

    // 获取配置的辅助方法
    public CombatConfig GetConfig()
    {
        return CombatConfigCategory.Instance.Get(this.CombatConfigId);
    }

    // === 战斗统计 ===
    public long CombatStartTime { get; set; }
    public Dictionary<long, CombatStatistics> Statistics = new();
}

public enum CombatPhase
{
    Preparation,  // 准备阶段
    InProgress,   // 进行中
    Settlement,   // 结算阶段
    Ended        // 已结束
}

public enum CombatType
{
    Normal,      // 普通战斗
    Boss,        // Boss战
    PvP,         // PvP战斗
    Arena        // 竞技场
}

public enum CombatEndReason
{
    Victory,     // 胜利
    Defeat,      // 失败
    Escape,      // 逃跑
    Surrender,   // 投降
    RoundLimit,  // 回合数限制
    Timeout,     // 超时
    Disconnect   // 断线
}

public class CombatUnitInfo
{
    public long UnitId { get; set; }
    public int Initiative { get; set; }    // 先攻值
    public int ActionCount { get; set; }   // 行动次数
    public bool IsPlayer { get; set; }     // 是否玩家控制
    public bool IsAlive { get; set; } = true;
}

public class CombatStatistics
{
    public int TurnCount { get; set; }     // 回合数
    public int DamageDealt { get; set; }   // 造成伤害
    public int DamageTaken { get; set; }   // 受到伤害
    public int HealingDone { get; set; }   // 治疗量
    public int SkillsUsed { get; set; }    // 技能使用次数
}
```

## 五、TurnBasedCombatComponentSystem核心实现

```csharp
[EntitySystemOf(typeof(TurnBasedCombatComponent))]
public static partial class TurnBasedCombatComponentSystem
{
    [EntitySystem]
    private static void Awake(this TurnBasedCombatComponent self)
    {
        self.CombatUnits = new HashSet<long>();
        self.TurnOrder = new List<long>();
        self.UnitInfos = new Dictionary<long, CombatUnitInfo>();
        self.Statistics = new Dictionary<long, CombatStatistics>();

        // 添加ObjectWait组件用于等待
        self.AddComponent<ObjectWait>();
    }

    [EntitySystem]
    private static void Destroy(this TurnBasedCombatComponent self)
    {
        // 如果战斗还在进行，强制结束
        if (self.InCombat)
        {
            self.ForceEndCombat();
        }
    }

    // 开始战斗（异步流程）
    public static async ETTask StartCombatAsync(this TurnBasedCombatComponent self,
        List<Unit> participants, CombatType combatType)
    {
        self.InCombat = true;
        self.RoundNumber = 1;
        self.Phase = CombatPhase.Preparation;
        self.CombatType = combatType;
        self.CombatStartTime = TimeInfo.Instance.ServerNow();

        // 获取战斗配置
        CombatConfig config = CombatConfigCategory.Instance.GetByType(combatType);
        if (config == null)
        {
            Log.Error($"Combat config not found for type: {combatType}");
            config = new CombatConfig(); // 使用默认配置
        }
        self.CombatConfigId = config.Id;

        // 初始化参战单位
        self.InitializeCombatants(participants);

        // 计算初始行动顺序
        self.CalculateTurnOrder();

        // 转换现有Buff为回合制
        self.ConvertExistingBuffs(participants);

        // 进入战斗阶段
        self.Phase = CombatPhase.InProgress;

        // 发布战斗开始事件
        EventSystem.Instance.Publish(self.Scene(),
            new CombatStartEvent
            {
                Participants = participants,
                CombatType = combatType
            });

        // 开始战斗循环
        await self.RunCombatLoop();
    }

    // 核心战斗循环
    private static async ETTask RunCombatLoop(this TurnBasedCombatComponent self)
    {
        EntityRef<TurnBasedCombatComponent> selfRef = self;

        while (self.InCombat)
        {
            // 回合开始
            Log.Debug($"Round {self.RoundNumber} starts");

            // 遍历当前回合的所有单位
            for (int i = 0; i < self.TurnOrder.Count; i++)
            {
                // await后重新获取self
                self = selfRef;
                if (!self.InCombat) break;

                long unitId = self.TurnOrder[i];
                CombatUnitInfo unitInfo = self.UnitInfos[unitId];

                // 跳过死亡单位
                if (!unitInfo.IsAlive) continue;

                Unit unit = self.Scene().GetComponent<UnitComponent>().Get(unitId);
                if (unit == null || unit.IsDisposed)
                {
                    unitInfo.IsAlive = false;
                    continue;
                }

                // 处理单位回合
                await self.ProcessUnitTurn(unit, unitInfo);

                // await后重新获取self
                self = selfRef;

                // 检查战斗结束条件
                if (self.CheckCombatEnd())
                {
                    await self.EndCombat(self.DetermineCombatResult());
                    return;
                }
            }

            // 回合结束，进入下一回合
            self.RoundNumber++;

            // 检查最大回合数
            CombatConfig config = self.GetConfig();
            if (self.RoundNumber > config.MaxRounds)
            {
                await self.EndCombat(CombatEndReason.RoundLimit);
                return;
            }

            // 重新计算行动顺序
            self.CalculateTurnOrder();
        }
    }

    // 处理单位回合（核心异步等待逻辑）
    private static async ETTask ProcessUnitTurn(this TurnBasedCombatComponent self,
        Unit unit, CombatUnitInfo unitInfo)
    {
        EntityRef<TurnBasedCombatComponent> selfRef = self;
        EntityRef<Unit> unitRef = unit;

        // 设置当前行动者
        self.CurrentActorId = unit.Id;

        // 触发回合开始
        self.OnTurnStart(unit);

        // 根据单位类型处理行动
        if (unitInfo.IsPlayer)
        {
            // 玩家单位：等待玩家操作（无时间限制）
            await self.WaitForPlayerAction(unit);
        }
        else
        {
            // AI单位：执行AI逻辑
            await self.ExecuteAIAction(unit);
        }

        // await后重新获取
        self = selfRef;
        unit = unitRef;

        // 触发回合结束
        self.OnTurnEnd(unit);

        // 更新统计
        unitInfo.ActionCount++;
        self.Statistics[unit.Id].TurnCount++;
    }

    // 等待玩家操作（核心Wait机制，无时间限制）
    private static async ETTask WaitForPlayerAction(this TurnBasedCombatComponent self,
        Unit unit)
    {
        EntityRef<TurnBasedCombatComponent> selfRef = self;
        EntityRef<Unit> unitRef = unit;

        // 获取ObjectWait组件
        ObjectWait objectWait = self.GetComponent<ObjectWait>();

        // 通知UI显示操作界面
        EventSystem.Instance.Publish(self.Scene(),
            new ShowPlayerActionUIEvent
            {
                UnitId = unit.Id,
                ValidTargets = self.GetValidTargets(unit.Id),
                AvailableSkills = self.GetAvailableSkills(unit)
            });

        Log.Debug($"Waiting for player {unit.Id} action...");

        // 等待玩家操作（无时间限制）
        Wait_PlayerAction playerAction = await objectWait.Wait<Wait_PlayerAction>();

        // await后重新获取
        self = selfRef;
        unit = unitRef;

        // 检查等待结果
        if (playerAction.Error != WaitTypeError.Success)
        {
            // 处理错误（取消、断线等）
            Log.Warning($"Player action wait error: {playerAction.Error}");
            playerAction.ActionType = ActionType.Pass;
        }

        // 执行玩家选择的动作
        await self.ExecuteAction(unit, playerAction);
    }

    // 执行动作
    private static async ETTask ExecuteAction(this TurnBasedCombatComponent self,
        Unit unit, Wait_PlayerAction action)
    {
        EntityRef<TurnBasedCombatComponent> selfRef = self;

        switch (action.ActionType)
        {
            case ActionType.Attack:
                await self.ExecuteAttack(unit, action.TargetId);
                break;

            case ActionType.UseSkill:
                await self.ExecuteSkill(unit, action.SkillId, action.TargetId);
                break;

            case ActionType.UseItem:
                await self.ExecuteUseItem(unit, action.TargetId);
                break;

            case ActionType.Defend:
                self.ExecuteDefend(unit);
                break;

            case ActionType.Escape:
                bool escapeSuccess = await self.TryEscape(unit);
                if (escapeSuccess)
                {
                    self = selfRef;
                    await self.EndCombat(CombatEndReason.Escape);
                }
                break;

            case ActionType.Pass:
                // 跳过回合
                Log.Debug($"Unit {unit.Id} passes the turn");
                break;
        }
    }

    // 执行AI行动
    private static async ETTask ExecuteAIAction(this TurnBasedCombatComponent self,
        Unit unit)
    {
        // 简单的AI决策
        List<long> targets = self.GetValidTargets(unit.Id, TargetType.Enemy);
        if (targets.Count > 0)
        {
            Wait_PlayerAction action = new()
            {
                ActionType = ActionType.Attack,
                TargetId = targets[RandomGenerator.RandomInt(0, targets.Count)]
            };
            await self.ExecuteAction(unit, action);
        }
        else
        {
            // 没有有效目标，跳过回合
            Wait_PlayerAction action = new() { ActionType = ActionType.Pass };
            await self.ExecuteAction(unit, action);
        }
    }

    // 初始化参战单位
    private static void InitializeCombatants(this TurnBasedCombatComponent self,
        List<Unit> participants)
    {
        foreach (Unit unit in participants)
        {
            self.CombatUnits.Add(unit.Id);

            // 判断是否玩家控制
            bool isPlayer = unit.GetComponent<PlayerComponent>() != null;

            self.UnitInfos[unit.Id] = new CombatUnitInfo
            {
                UnitId = unit.Id,
                Initiative = unit.GetComponent<NumericComponent>()
                    .GetAsInt(NumericType.Speed),
                IsPlayer = isPlayer
            };

            self.Statistics[unit.Id] = new CombatStatistics();

            // 添加ObjectWait组件到单位（如果需要）
            if (!unit.GetComponent<ObjectWait>())
            {
                unit.AddComponent<ObjectWait>();
            }
        }
    }

    // 计算行动顺序
    private static void CalculateTurnOrder(this TurnBasedCombatComponent self)
    {
        self.TurnOrder.Clear();

        // 按先攻值排序
        var sortedUnits = self.UnitInfos.Values
            .Where(u => u.IsAlive)
            .OrderByDescending(u => u.Initiative)
            .ThenBy(u => u.ActionCount)
            .Select(u => u.UnitId)
            .ToList();

        self.TurnOrder.AddRange(sortedUnits);
    }

    // 转换现有Buff为回合制
    private static void ConvertExistingBuffs(this TurnBasedCombatComponent self,
        List<Unit> participants)
    {
        foreach (Unit unit in participants)
        {
            BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
            if (buffComponent == null) continue;

            foreach (Buff buff in buffComponent.Children.Values)
            {
                if (buff.DurationType == BuffDurationType.Time)
                {
                    // 智能转换：时间转回合
                    buff.DurationType = BuffDurationType.Hybrid;
                    long remainTime = buff.ExpireTime - TimeInfo.Instance.ServerNow();
                    buff.RemainTurn = Math.Max(1, (int)(remainTime / 5000)); // 假设每回合5秒
                    buff.CreatedRound = self.RoundNumber;
                }
            }
        }
    }

    // 还原Buff为时间制
    private static void RestoreBuffsToTimeBased(this TurnBasedCombatComponent self)
    {
        foreach (long unitId in self.CombatUnits)
        {
            Unit unit = self.Scene().GetComponent<UnitComponent>().Get(unitId);
            if (unit == null) continue;

            BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
            if (buffComponent == null) continue;

            foreach (Buff buff in buffComponent.Children.Values)
            {
                if (buff.DurationType == BuffDurationType.Turn)
                {
                    // 回合制转时间制
                    buff.DurationType = BuffDurationType.Time;
                    // 根据剩余回合设置时间
                    buff.ExpireTime = TimeInfo.Instance.ServerNow() + buff.RemainTurn * 5000;
                }
            }
        }
    }

    // 回合开始处理
    private static void OnTurnStart(this TurnBasedCombatComponent self, Unit unit)
    {
        // 调用Buff系统的回合开始
        BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
        buffComponent?.OnTurnStart(self.RoundNumber);

        // 发布回合开始事件
        EventSystem.Instance.Publish(self.Scene(),
            new TurnStartEvent
            {
                UnitId = unit.Id,
                Round = self.RoundNumber
            });

        Log.Debug($"Turn start: Unit {unit.Id}, Round {self.RoundNumber}");
    }

    // 回合结束处理
    private static void OnTurnEnd(this TurnBasedCombatComponent self, Unit unit)
    {
        // 调用Buff系统的回合结束（已包含优先级处理）
        BuffComponent buffComponent = unit.GetComponent<BuffComponent>();
        buffComponent?.OnTurnEnd(self.RoundNumber);

        // 发布回合结束事件
        EventSystem.Instance.Publish(self.Scene(),
            new TurnEndEvent
            {
                UnitId = unit.Id,
                Round = self.RoundNumber
            });

        Log.Debug($"Turn end: Unit {unit.Id}, Round {self.RoundNumber}");
    }

    // 检查战斗结束条件
    private static bool CheckCombatEnd(this TurnBasedCombatComponent self)
    {
        // 检查是否有一方全部死亡
        bool hasAlivePlayer = false;
        bool hasAliveEnemy = false;

        foreach (var info in self.UnitInfos.Values)
        {
            if (!info.IsAlive) continue;

            if (info.IsPlayer)
                hasAlivePlayer = true;
            else
                hasAliveEnemy = true;
        }

        return !hasAlivePlayer || !hasAliveEnemy;
    }

    // 判定战斗结果
    private static CombatEndReason DetermineCombatResult(this TurnBasedCombatComponent self)
    {
        bool hasAlivePlayer = self.UnitInfos.Values
            .Any(u => u.IsPlayer && u.IsAlive);

        return hasAlivePlayer ? CombatEndReason.Victory : CombatEndReason.Defeat;
    }

    // 结束战斗
    private static async ETTask EndCombat(this TurnBasedCombatComponent self,
        CombatEndReason reason)
    {
        self.Phase = CombatPhase.Settlement;
        self.InCombat = false;

        Log.Info($"Combat ended: {reason}");

        // 还原所有Buff
        self.RestoreBuffsToTimeBased();

        // 发布战斗结束事件
        EventSystem.Instance.Publish(self.Scene(),
            new CombatEndEvent
            {
                Reason = reason,
                Statistics = self.Statistics,
                Duration = TimeInfo.Instance.ServerNow() - self.CombatStartTime
            });

        // 通知等待战斗结束的地方
        ObjectWait objectWait = self.GetComponent<ObjectWait>();
        objectWait.Notify(new Wait_CombatEnd
        {
            Error = WaitTypeError.Success,
            Reason = reason,
            IsWin = reason == CombatEndReason.Victory
        });

        // 清理状态
        self.Phase = CombatPhase.Ended;
        self.CombatUnits.Clear();
        self.TurnOrder.Clear();
        self.UnitInfos.Clear();

        await ETTask.CompletedTask;
    }

    // 获取有效目标
    private static List<long> GetValidTargets(this TurnBasedCombatComponent self,
        long unitId, TargetType targetType = TargetType.All)
    {
        List<long> targets = new();
        CombatUnitInfo actorInfo = self.UnitInfos[unitId];

        foreach (var info in self.UnitInfos.Values)
        {
            if (!info.IsAlive || info.UnitId == unitId) continue;

            switch (targetType)
            {
                case TargetType.Enemy:
                    if (info.IsPlayer != actorInfo.IsPlayer)
                        targets.Add(info.UnitId);
                    break;
                case TargetType.Ally:
                    if (info.IsPlayer == actorInfo.IsPlayer)
                        targets.Add(info.UnitId);
                    break;
                case TargetType.All:
                    targets.Add(info.UnitId);
                    break;
            }
        }

        return targets;
    }
}
```

## 六、玩家操作处理器

```csharp
// C2M_PlayerActionHandler.cs - 处理玩家输入
[MessageHandler(SceneType.Map)]
public class C2M_PlayerActionHandler : MessageHandler<Scene, C2M_PlayerAction>
{
    protected override async ETTask Run(Scene scene, C2M_PlayerAction message)
    {
        // 获取战斗组件
        TurnBasedCombatComponent combat = scene.GetComponent<TurnBasedCombatComponent>();
        if (combat == null || !combat.InCombat)
        {
            Log.Warning("No combat in progress");
            return;
        }

        // 验证是否当前玩家的回合
        if (combat.CurrentActorId != message.UnitId)
        {
            Log.Warning($"Not player's turn: {message.UnitId}");
            return;
        }

        // 构造玩家动作
        Wait_PlayerAction action = new()
        {
            Error = WaitTypeError.Success,
            ActionType = (ActionType)message.ActionType,
            TargetId = message.TargetId,
            SkillId = message.SkillId,
            TargetPosition = message.TargetPosition
        };

        // 通知战斗系统玩家已操作
        ObjectWait objectWait = combat.GetComponent<ObjectWait>();
        objectWait.Notify(action);

        Log.Debug($"Player action received: {action.ActionType}");

        await ETTask.CompletedTask;
    }
}
```

## 七、使用示例

### 7.1 发起战斗
```csharp
// 玩家遇敌，开始战斗
public static async ETTask StartBattle(Scene scene, Unit player, List<Unit> enemies)
{
    // 创建战斗组件
    TurnBasedCombatComponent combat = scene.AddComponent<TurnBasedCombatComponent>();

    // 准备参战单位
    List<Unit> participants = new() { player };
    participants.AddRange(enemies);

    // 开始战斗（异步执行）
    ETTask combatTask = combat.StartCombatAsync(participants, CombatType.Normal);

    // 可以等待战斗结束
    ObjectWait objectWait = combat.GetComponent<ObjectWait>();
    Wait_CombatEnd result = await objectWait.Wait<Wait_CombatEnd>();

    // 处理战斗结果
    if (result.IsWin)
    {
        Log.Info("Player wins!");
        // 发放奖励
    }
    else
    {
        Log.Info("Player loses!");
        // 处理失败
    }
}
```

### 7.2 客户端发送玩家操作
```csharp
// 玩家点击攻击按钮
public static void OnAttackButtonClick(long targetId)
{
    C2M_PlayerAction message = C2M_PlayerAction.Create();
    message.UnitId = MyUnitId;
    message.ActionType = (int)ActionType.Attack;
    message.TargetId = targetId;

    Root.GetComponent<ClientSenderComponent>().Send(message);
}

// 玩家使用技能
public static void OnUseSkillButtonClick(int skillId, long targetId)
{
    C2M_PlayerAction message = C2M_PlayerAction.Create();
    message.UnitId = MyUnitId;
    message.ActionType = (int)ActionType.UseSkill;
    message.SkillId = skillId;
    message.TargetId = targetId;

    Root.GetComponent<ClientSenderComponent>().Send(message);
}
```

## 八、测试用例

```csharp
public class TurnBasedCombatTest : ARobotCaseHandler
{
    protected override async ETTask Run(RobotCase robotCase)
    {
        // 创建测试单位
        Scene scene = robotCase.Scene();
        Unit player = CreateTestUnit(scene, true);
        Unit enemy = CreateTestUnit(scene, false);

        // 开始战斗
        TurnBasedCombatComponent combat = scene.AddComponent<TurnBasedCombatComponent>();
        ETTask combatTask = combat.StartCombatAsync(
            new List<Unit> { player, enemy },
            CombatType.Normal);

        // 模拟玩家操作
        await Fiber.Instance.Root.GetComponent<TimerComponent>().WaitAsync(1000);

        // 发送攻击指令
        Wait_PlayerAction action = new()
        {
            Error = WaitTypeError.Success,
            ActionType = ActionType.Attack,
            TargetId = enemy.Id
        };
        combat.GetComponent<ObjectWait>().Notify(action);

        // 等待战斗结束
        Wait_CombatEnd result = await combat.GetComponent<ObjectWait>()
            .Wait<Wait_CombatEnd>();

        // 验证结果
        Assert.Equal(WaitTypeError.Success, result.Error);
        Log.Info($"Combat ended: {result.Reason}, IsWin: {result.IsWin}");
    }
}
```

## 九、关键技术要点

### 9.1 ObjectWait机制
- 使用ET框架的ObjectWait实现异步等待
- 玩家操作无时间限制，等待玩家完成操作
- 支持取消和错误处理

### 9.2 EntityRef安全
- 在async/await环境下使用EntityRef确保实体引用安全
- await后必须重新获取Entity

### 9.3 模块化设计
- 战斗系统独立包设计
- 通过事件系统解耦
- 各模块可独立测试

### 9.4 异步流程控制
- 使用async/await确保战斗流程的连贯性
- 无阻塞等待，基于ETTask实现高效的异步等待
- 不限制玩家操作时间

## 十、包依赖关系

- **依赖spell包**：调用Buff系统的回合制功能
- **依赖proto包**：消息定义
- **依赖unit包**：Unit实体和组件

## 十一、注意事项

1. **消息定义**：需要在proto包中定义C2M_PlayerAction消息
2. **数值类型**：确保NumericType中定义了Speed等属性
3. **日志输出**：使用英文日志，符合项目规范
4. **测试覆盖**：确保所有分支逻辑都有测试覆盖
5. **性能考虑**：大量单位战斗时注意排序的性能影响
6. **Entity安全**：严格遵循EntityRef使用规范