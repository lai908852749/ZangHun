# ET框架回合制战斗系统 - 快速Demo版

## 一、系统概述

这是一个精简的回合制战斗系统，专为快速实现demo而设计。系统保留了核心战斗机制，移除了复杂的技能、Buff等系统，使用伪代码标注了扩展点。

### 核心特性

- ✅ 基础回合制战斗流程
- ✅ 伤害计算（攻击、防御、暴击、闪避、格挡）
- ✅ 简单AI（攻击血量最低目标）
- ✅ 战斗记录和防作弊
- ✅ 随机数种子系统
- 🔄 技能系统（伪代码，预留接口）
- 🔄 Buff系统（伪代码，预留接口）
- 🔄 断线重连（伪代码，基础框架）

## 二、基础配置

### 2.1 战斗配置类

```csharp
namespace ET
{
    [HideReferenceObjectPicker]
    [System.Serializable]
    public partial class CombatConfig : ProtoObject
    {
        [LabelText("ID")]
        public int Id;

        [LabelText("战斗类型")]
        public CombatType CombatType;

        [LabelText("允许逃跑")]
        public bool AllowEscape = true;

        [LabelText("允许投降")]
        public bool AllowSurrender = true;

        [LabelText("AI决策延迟(毫秒)")]
        public int AIDecisionDelay = 500; // 简化为500ms

        [LabelText("随机种子")]
        public int RandomSeed = 0; // 0表示使用系统时间生成
    }
}
```

### 2.2 枚举定义

```csharp
// 战斗类型
public enum CombatType
{
    Normal = 0,     // 普通战斗
    Boss = 1,       // Boss战斗
    Arena = 2,      // 竞技场
}

// 战斗阶段
public enum CombatPhase
{
    None = 0,
    Preparation = 1,  // 准备阶段
    InProgress = 2,   // 战斗中
    Settlement = 3,   // 结算阶段
    Ended = 4        // 已结束
}

// 动作类型（精简版）
public enum ActionType
{
    None = 0,
    Attack = 1,     // 普通攻击
    Pass = 2,       // 跳过回合
    Escape = 3,     // 逃跑
    Surrender = 4   // 投降
}

// 战斗结束原因
public enum CombatEndReason
{
    Victory = 0,     // 胜利
    Defeat = 1,      // 失败
    Escape = 2,      // 逃跑
    Surrender = 3,   // 投降
    Disconnect = 4   // 断线
}
```

## 三、核心组件

### 3.1 TurnBasedCombatComponent - 主战斗组件

```csharp
namespace ET
{
    [ComponentOf(typeof(Scene))]
    public class TurnBasedCombatComponent : Entity, IAwake, IDestroy
    {
        // 战斗状态
        public bool InCombat { get; set; }
        public int RoundNumber { get; set; }
        public CombatPhase Phase { get; set; }
        public CombatType CombatType { get; set; }
        public int CombatConfigId { get; set; }

        // 战斗时间
        public long CombatStartTime { get; set; }

        // 参战单位
        public List<long> CombatUnits = new();
        public List<long> TurnOrder = new(); // 当前回合的行动顺序
        public Dictionary<long, CombatUnitInfo> UnitInfos = new();

        // 当前行动者
        public long CurrentActorId { get; set; }

        // 战斗统计
        public Dictionary<long, CombatStatistics> Statistics = new();
    }

    // 单位战斗信息
    public class CombatUnitInfo
    {
        public long UnitId { get; set; }
        public bool IsPlayer { get; set; }
        public bool IsAlive { get; set; } = true;
        public int Team { get; set; } // 0=玩家队伍, 1=敌方队伍

        // 断线相关（伪代码）
        public bool IsDisconnected { get; set; }
        public long DisconnectTime { get; set; }
    }

    // 战斗统计
    public class CombatStatistics
    {
        public int DamageDealt { get; set; }
        public int DamageTaken { get; set; }
        public int AttackCount { get; set; }
        public int CriticalCount { get; set; }
        public int DodgeCount { get; set; }
        public int BlockCount { get; set; }
    }
}
```

### 3.2 CombatRandomComponent - 随机数组件

```csharp
namespace ET
{
    [ComponentOf(typeof(TurnBasedCombatComponent))]
    public class CombatRandomComponent : Entity, IAwake<int>
    {
        private Random random;
        private int seed;
        private int sequenceNumber;

        public int Seed => seed;
        public int SequenceNumber => sequenceNumber;

        public void Initialize(int seedValue)
        {
            this.seed = seedValue == 0 ? (int)(TimeInfo.Instance.ServerNow() % int.MaxValue) : seedValue;
            this.random = new Random(this.seed);
            this.sequenceNumber = 0;
        }

        public float GetRandom()
        {
            sequenceNumber++;
            return (float)random.NextDouble();
        }

        public int GetRandomInt(int min, int max)
        {
            sequenceNumber++;
            return random.Next(min, max);
        }

        public bool CheckProbability(float probability)
        {
            return GetRandom() < probability;
        }
    }
}
```

### 3.3 CombatRecordComponent - 战斗记录组件

```csharp
namespace ET
{
    [ComponentOf(typeof(TurnBasedCombatComponent))]
    public class CombatRecordComponent : Entity, IAwake
    {
        public long CombatId { get; set; }
        public int RandomSeed { get; set; }
        public List<CombatActionRecord> Actions = new();
        public long CombatStartTime { get; set; }
        public long CombatEndTime { get; set; }
        public int TotalRounds { get; set; }

        public void RecordAction(CombatActionRecord record)
        {
            record.Timestamp = TimeInfo.Instance.ClientNow();
            record.SequenceNumber = Actions.Count;
            Actions.Add(record);
        }
    }

    // 战斗动作记录
    public class CombatActionRecord
    {
        public long Timestamp { get; set; }
        public int SequenceNumber { get; set; }
        public long UnitId { get; set; }
        public ActionType ActionType { get; set; }
        public long TargetId { get; set; }
        public DamageResult DamageResult { get; set; }
        public int RandomSequenceNumber { get; set; }
    }

    // 伤害结果
    public struct DamageResult
    {
        public int FinalDamage;
        public int BaseDamage;
        public bool IsCritical;
        public bool IsDodged;
        public bool IsBlocked;
        public float CritMultiplier;
    }
}
```

## 四、System实现

### 4.1 TurnBasedCombatComponentSystem - 主战斗系统

```csharp
namespace ET
{
    [EntitySystemOf(typeof(TurnBasedCombatComponent))]
    public static partial class TurnBasedCombatComponentSystem
    {
        [EntitySystem]
        private static void Awake(this TurnBasedCombatComponent self)
        {
            self.Phase = CombatPhase.None;
            self.InCombat = false;
            self.RoundNumber = 0;
        }

        [EntitySystem]
        private static void Destroy(this TurnBasedCombatComponent self)
        {
            self.CombatUnits.Clear();
            self.TurnOrder.Clear();
            self.UnitInfos.Clear();
            self.Statistics.Clear();
        }

        // 开始战斗
        public static async ETTask StartCombatAsync(this TurnBasedCombatComponent self,
            List<Unit> participants, CombatType combatType)
        {
            self.InCombat = true;
            self.RoundNumber = 1;
            self.Phase = CombatPhase.Preparation;
            self.CombatType = combatType;
            self.CombatStartTime = TimeInfo.Instance.ServerNow();

            // 获取配置
            CombatConfig config = CombatConfigCategory.Instance.GetByType(combatType);
            if (config == null)
            {
                config = new CombatConfig();
            }
            self.CombatConfigId = config.Id;

            // 初始化随机数
            var randomComponent = self.AddComponent<CombatRandomComponent, int>(config.RandomSeed);

            // 初始化战斗记录
            var record = self.AddComponent<CombatRecordComponent>();
            record.CombatId = IdGenerator.Instance.GenerateId();
            record.RandomSeed = randomComponent.Seed;
            record.CombatStartTime = self.CombatStartTime;

            // 初始化参战单位
            self.InitializeCombatants(participants);

            // 计算行动顺序
            self.CalculateTurnOrder();

            // 进入战斗
            self.Phase = CombatPhase.InProgress;

            Log.Debug($"Combat started with {participants.Count} participants");

            // 开始战斗循环
            await self.RunCombatLoop();
        }

        // 初始化参战单位
        private static void InitializeCombatants(this TurnBasedCombatComponent self, List<Unit> participants)
        {
            self.CombatUnits.Clear();
            self.UnitInfos.Clear();
            self.Statistics.Clear();

            foreach (var unit in participants)
            {
                self.CombatUnits.Add(unit.Id);

                // 判断是玩家还是怪物（简化判断）
                bool isPlayer = unit.GetComponent<UnitTypeComponent>()?.IsPlayer ?? false;

                self.UnitInfos[unit.Id] = new CombatUnitInfo
                {
                    UnitId = unit.Id,
                    IsPlayer = isPlayer,
                    IsAlive = true,
                    Team = isPlayer ? 0 : 1
                };

                self.Statistics[unit.Id] = new CombatStatistics();
            }
        }

        // 计算行动顺序（按速度排序）
        private static void CalculateTurnOrder(this TurnBasedCombatComponent self)
        {
            self.TurnOrder.Clear();

            var aliveUnits = self.CombatUnits
                .Where(id => self.UnitInfos[id].IsAlive)
                .ToList();

            // 按速度排序
            var scene = self.Scene();
            aliveUnits.Sort((a, b) =>
            {
                Unit unitA = scene.GetComponent<UnitComponent>().Get(a);
                Unit unitB = scene.GetComponent<UnitComponent>().Get(b);

                int speedA = unitA?.GetComponent<NumericComponent>()?.GetAsInt(NumericType.Speed) ?? 0;
                int speedB = unitB?.GetComponent<NumericComponent>()?.GetAsInt(NumericType.Speed) ?? 0;

                return speedB.CompareTo(speedA); // 速度高的先行动
            });

            self.TurnOrder = aliveUnits;
        }

        // 战斗主循环
        private static async ETTask RunCombatLoop(this TurnBasedCombatComponent self)
        {
            EntityRef<TurnBasedCombatComponent> selfRef = self;

            while (self.InCombat)
            {
                Log.Debug($"Round {self.RoundNumber} starts");

                // 处理每个单位的回合
                for (int i = 0; i < self.TurnOrder.Count; i++)
                {
                    self = selfRef;
                    if (!self.InCombat) break;

                    long unitId = self.TurnOrder[i];
                    CombatUnitInfo unitInfo = self.UnitInfos[unitId];

                    if (!unitInfo.IsAlive) continue;

                    Unit unit = self.Scene().GetComponent<UnitComponent>().Get(unitId);
                    if (unit == null || unit.IsDisposed)
                    {
                        unitInfo.IsAlive = false;
                        continue;
                    }

                    self.CurrentActorId = unitId;

                    // 处理回合
                    if (unitInfo.IsPlayer && !unitInfo.IsDisconnected)
                    {
                        // 玩家回合
                        await self.ProcessPlayerTurn(unit);
                    }
                    else
                    {
                        // AI回合（怪物或断线玩家）
                        await self.ProcessAITurn(unit);
                    }

                    self = selfRef;

                    // 检查战斗结束
                    if (self.CheckCombatEnd())
                    {
                        await self.EndCombat(self.DetermineCombatResult());
                        return;
                    }
                }

                self.RoundNumber++;
                self.CalculateTurnOrder();
            }
        }

        // 处理玩家回合
        private static async ETTask ProcessPlayerTurn(this TurnBasedCombatComponent self, Unit unit)
        {
            EntityRef<TurnBasedCombatComponent> selfRef = self;
            EntityRef<Unit> unitRef = unit;

            // 等待玩家操作
            var objectWait = self.GetComponent<ObjectWait>() ?? self.AddComponent<ObjectWait>();

            // 发送UI事件
            EventSystem.Instance.Publish(self.Scene(),
                new ShowPlayerActionUIEvent
                {
                    UnitId = unit.Id,
                    ValidTargets = self.GetValidTargets(unit.Id, TargetType.Enemy)
                });

            // 等待玩家操作
            Wait_PlayerAction action = await objectWait.Wait<Wait_PlayerAction>();

            self = selfRef;
            unit = unitRef;

            if (unit == null || unit.IsDisposed) return;

            // 执行玩家动作
            await self.ExecuteAction(unit, action);
        }

        // 处理AI回合（极简版）
        private static async ETTask ProcessAITurn(this TurnBasedCombatComponent self, Unit unit)
        {
            EntityRef<TurnBasedCombatComponent> selfRef = self;
            EntityRef<Unit> unitRef = unit;

            // AI延迟（模拟思考）
            var config = self.GetConfig();
            if (config.AIDecisionDelay > 0)
            {
                await self.Root.GetComponent<TimerComponent>().WaitAsync(config.AIDecisionDelay);
                self = selfRef;
                unit = unitRef;
            }

            // 简单AI：攻击血量最低的敌人
            var enemies = self.GetValidTargets(unit.Id, TargetType.Enemy);
            if (enemies.Count == 0)
            {
                // 没有目标，跳过回合
                return;
            }

            // 选择血量最低的目标
            long targetId = self.SelectWeakestTarget(enemies);

            // 执行攻击
            var action = new Wait_PlayerAction
            {
                ActionType = ActionType.Attack,
                TargetId = targetId
            };

            Log.Debug($"AI {unit.Id} attacks {targetId}");

            await self.ExecuteAction(unit, action);
        }

        // 选择血量最低的目标
        private static long SelectWeakestTarget(this TurnBasedCombatComponent self, List<long> targets)
        {
            long weakestId = targets[0];
            int lowestHp = int.MaxValue;

            var unitComponent = self.Scene().GetComponent<UnitComponent>();

            foreach (long targetId in targets)
            {
                Unit target = unitComponent.Get(targetId);
                if (target == null) continue;

                int hp = target.GetComponent<NumericComponent>()?.GetAsInt(NumericType.Hp) ?? 0;
                if (hp < lowestHp)
                {
                    lowestHp = hp;
                    weakestId = targetId;
                }
            }

            return weakestId;
        }

        // 执行动作
        private static async ETTask ExecuteAction(this TurnBasedCombatComponent self, Unit unit, Wait_PlayerAction action)
        {
            var record = self.GetComponent<CombatRecordComponent>();

            switch (action.ActionType)
            {
                case ActionType.Attack:
                    await self.ExecuteAttack(unit, action.TargetId);
                    break;

                case ActionType.Pass:
                    Log.Debug($"Unit {unit.Id} passes turn");
                    record.RecordAction(new CombatActionRecord
                    {
                        UnitId = unit.Id,
                        ActionType = ActionType.Pass
                    });
                    break;

                case ActionType.Escape:
                    if (self.GetConfig().AllowEscape)
                    {
                        await self.EndCombat(CombatEndReason.Escape);
                    }
                    break;

                case ActionType.Surrender:
                    if (self.GetConfig().AllowSurrender)
                    {
                        await self.EndCombat(CombatEndReason.Surrender);
                    }
                    break;

                default:
                    Log.Warning($"Unknown action type: {action.ActionType}");
                    break;
            }

            await ETTask.CompletedTask;
        }

        // 执行攻击
        private static async ETTask ExecuteAttack(this TurnBasedCombatComponent self, Unit attacker, long targetId)
        {
            Unit target = self.Scene().GetComponent<UnitComponent>().Get(targetId);
            if (target == null || target.IsDisposed) return;

            var record = self.GetComponent<CombatRecordComponent>();
            var randomGen = self.GetComponent<CombatRandomComponent>();

            // 计算伤害
            DamageResult damage = self.CalculateDamage(attacker, target);

            // 应用伤害
            if (damage.FinalDamage > 0)
            {
                var targetNum = target.GetComponent<NumericComponent>();
                targetNum.Add(NumericType.Hp, -damage.FinalDamage);

                // 更新统计
                if (self.Statistics.TryGetValue(attacker.Id, out var attackerStats))
                {
                    attackerStats.DamageDealt += damage.FinalDamage;
                    attackerStats.AttackCount++;
                    if (damage.IsCritical) attackerStats.CriticalCount++;
                }

                if (self.Statistics.TryGetValue(targetId, out var targetStats))
                {
                    targetStats.DamageTaken += damage.FinalDamage;
                    if (damage.IsDodged) targetStats.DodgeCount++;
                    if (damage.IsBlocked) targetStats.BlockCount++;
                }

                // 检查目标是否死亡
                if (targetNum.GetAsInt(NumericType.Hp) <= 0)
                {
                    self.UnitInfos[targetId].IsAlive = false;
                    Log.Info($"Unit {targetId} defeated!");
                }
            }

            // 记录动作
            record.RecordAction(new CombatActionRecord
            {
                UnitId = attacker.Id,
                ActionType = ActionType.Attack,
                TargetId = targetId,
                DamageResult = damage,
                RandomSequenceNumber = randomGen.SequenceNumber
            });

            // 发送战斗事件（用于UI显示）
            EventSystem.Instance.Publish(self.Scene(),
                new CombatAttackEvent
                {
                    AttackerId = attacker.Id,
                    TargetId = targetId,
                    Damage = damage
                });

            await ETTask.CompletedTask;
        }

        // 计算伤害
        private static DamageResult CalculateDamage(this TurnBasedCombatComponent self, Unit attacker, Unit target)
        {
            var attackerNum = attacker.GetComponent<NumericComponent>();
            var targetNum = target.GetComponent<NumericComponent>();
            var randomGen = self.GetComponent<CombatRandomComponent>();

            DamageResult result = new DamageResult();

            // 基础攻击力
            result.BaseDamage = attackerNum.GetAsInt(NumericType.Attack);

            // TODO: 技能伤害加成（伪代码）
            // if (skillId > 0)
            // {
            //     var skillConfig = SkillConfigCategory.Instance.Get(skillId);
            //     result.BaseDamage = (int)(result.BaseDamage * skillConfig.DamageMultiplier);
            // }

            // 闪避判定
            float dodgeRate = targetNum.GetAsFloat(NumericType.DodgeRate);
            result.IsDodged = randomGen.CheckProbability(dodgeRate);
            if (result.IsDodged)
            {
                result.FinalDamage = 0;
                Log.Debug($"Attack dodged! DodgeRate: {dodgeRate:F2}");
                return result;
            }

            // 暴击判定
            float critRate = attackerNum.GetAsFloat(NumericType.CritRate);
            result.IsCritical = randomGen.CheckProbability(critRate);
            if (result.IsCritical)
            {
                result.CritMultiplier = attackerNum.GetAsFloat(NumericType.CritDamage);
                if (result.CritMultiplier <= 0) result.CritMultiplier = 2.0f;
                result.BaseDamage = (int)(result.BaseDamage * result.CritMultiplier);
                Log.Debug($"Critical hit! Multiplier: {result.CritMultiplier:F1}");
            }

            // 防御减伤
            int defense = targetNum.GetAsInt(NumericType.Defense);
            result.FinalDamage = Math.Max(1, result.BaseDamage - defense);

            // 格挡判定
            float blockRate = targetNum.GetAsFloat(NumericType.BlockRate);
            result.IsBlocked = randomGen.CheckProbability(blockRate);
            if (result.IsBlocked)
            {
                result.FinalDamage = result.FinalDamage / 2;
                Log.Debug($"Attack blocked! BlockRate: {blockRate:F2}");
            }

            // TODO: Buff影响（伪代码）
            // var attackerBuffs = attacker.GetComponent<BuffComponent>();
            // if (attackerBuffs != null)
            // {
            //     result.FinalDamage = attackerBuffs.ModifyOutgoingDamage(result.FinalDamage);
            // }
            // var targetBuffs = target.GetComponent<BuffComponent>();
            // if (targetBuffs != null)
            // {
            //     result.FinalDamage = targetBuffs.ModifyIncomingDamage(result.FinalDamage);
            // }

            Log.Debug($"Damage: Base={result.BaseDamage}, Final={result.FinalDamage}");

            return result;
        }

        // 获取有效目标
        private static List<long> GetValidTargets(this TurnBasedCombatComponent self, long unitId, TargetType targetType)
        {
            var unitInfo = self.UnitInfos[unitId];
            var targets = new List<long>();

            foreach (var kvp in self.UnitInfos)
            {
                if (!kvp.Value.IsAlive) continue;
                if (kvp.Key == unitId) continue;

                bool isValidTarget = targetType switch
                {
                    TargetType.Enemy => kvp.Value.Team != unitInfo.Team,
                    TargetType.Ally => kvp.Value.Team == unitInfo.Team,
                    TargetType.All => true,
                    _ => false
                };

                if (isValidTarget)
                {
                    targets.Add(kvp.Key);
                }
            }

            return targets;
        }

        // 检查战斗结束
        private static bool CheckCombatEnd(this TurnBasedCombatComponent self)
        {
            int aliveTeam0 = 0;
            int aliveTeam1 = 0;

            foreach (var unitInfo in self.UnitInfos.Values)
            {
                if (unitInfo.IsAlive)
                {
                    if (unitInfo.Team == 0) aliveTeam0++;
                    else aliveTeam1++;
                }
            }

            return aliveTeam0 == 0 || aliveTeam1 == 0;
        }

        // 判断战斗结果
        private static CombatEndReason DetermineCombatResult(this TurnBasedCombatComponent self)
        {
            int aliveTeam0 = self.UnitInfos.Values.Count(u => u.IsAlive && u.Team == 0);
            return aliveTeam0 > 0 ? CombatEndReason.Victory : CombatEndReason.Defeat;
        }

        // 结束战斗
        private static async ETTask EndCombat(this TurnBasedCombatComponent self, CombatEndReason reason)
        {
            self.Phase = CombatPhase.Settlement;
            self.InCombat = false;

            var record = self.GetComponent<CombatRecordComponent>();
            record.CombatEndTime = TimeInfo.Instance.ServerNow();
            record.TotalRounds = self.RoundNumber;

            Log.Info($"Combat ended: {reason}, Rounds: {self.RoundNumber}");

            // 发布结束事件
            EventSystem.Instance.Publish(self.Scene(),
                new CombatEndEvent
                {
                    Reason = reason,
                    Statistics = self.Statistics,
                    Duration = TimeInfo.Instance.ServerNow() - self.CombatStartTime
                });

            // 通知等待
            var objectWait = self.GetComponent<ObjectWait>();
            objectWait?.Notify(new Wait_CombatEnd
            {
                Error = WaitTypeError.Success,
                Reason = reason
            });

            self.Phase = CombatPhase.Ended;

            await ETTask.CompletedTask;
        }

        // 获取配置
        private static CombatConfig GetConfig(this TurnBasedCombatComponent self)
        {
            return CombatConfigCategory.Instance.Get(self.CombatConfigId) ?? new CombatConfig();
        }

        // TODO: 断线重连处理（伪代码）
        public static void HandlePlayerDisconnect(this TurnBasedCombatComponent self, long playerId)
        {
            // if (self.UnitInfos.TryGetValue(playerId, out var unitInfo))
            // {
            //     unitInfo.IsDisconnected = true;
            //     unitInfo.DisconnectTime = TimeInfo.Instance.ServerNow();
            //     Log.Warning($"Player {playerId} disconnected");
            // }
        }

        public static async ETTask HandlePlayerReconnect(this TurnBasedCombatComponent self, long playerId)
        {
            // if (self.UnitInfos.TryGetValue(playerId, out var unitInfo))
            // {
            //     unitInfo.IsDisconnected = false;
            //     // 发送当前战斗状态
            //     // SendCombatState(playerId);
            // }
            await ETTask.CompletedTask;
        }
    }
}
```

### 4.2 CombatRandomComponentSystem

```csharp
namespace ET
{
    [EntitySystemOf(typeof(CombatRandomComponent))]
    public static partial class CombatRandomComponentSystem
    {
        [EntitySystem]
        private static void Awake(this CombatRandomComponent self, int seed)
        {
            self.Initialize(seed);
            Log.Debug($"Combat random initialized with seed: {self.Seed}");
        }
    }
}
```

### 4.3 CombatRecordComponentSystem

```csharp
namespace ET
{
    [EntitySystemOf(typeof(CombatRecordComponent))]
    public static partial class CombatRecordComponentSystem
    {
        [EntitySystem]
        private static void Awake(this CombatRecordComponent self)
        {
            self.Actions.Clear();
        }
    }
}
```

## 五、辅助类和事件

### 5.1 枚举和常量

```csharp
// 目标类型
public enum TargetType
{
    Self = 0,
    Enemy = 1,
    Ally = 2,
    All = 3
}

// 等待类型
public class Wait_PlayerAction : IWaitType
{
    public int Error { get; set; }
    public ActionType ActionType { get; set; }
    public long TargetId { get; set; }
}

public class Wait_CombatEnd : IWaitType
{
    public int Error { get; set; }
    public CombatEndReason Reason { get; set; }
    public bool IsWin { get; set; }
}
```

### 5.2 事件定义

```csharp
// 显示玩家操作UI事件
public struct ShowPlayerActionUIEvent
{
    public long UnitId;
    public List<long> ValidTargets;
}

// 战斗攻击事件
public struct CombatAttackEvent
{
    public long AttackerId;
    public long TargetId;
    public DamageResult Damage;
}

// 战斗结束事件
public struct CombatEndEvent
{
    public CombatEndReason Reason;
    public Dictionary<long, CombatStatistics> Statistics;
    public long Duration;
}
```

## 六、使用示例

### 6.1 启动战斗

```csharp
// 在某个Handler或System中启动战斗
public static async ETTask StartBattle(Scene scene)
{
    // 获取参战单位
    var unitComponent = scene.GetComponent<UnitComponent>();
    var participants = new List<Unit>();

    // 添加玩家单位
    Unit player = unitComponent.Get(playerId);
    participants.Add(player);

    // 添加怪物单位
    Unit monster1 = unitComponent.Get(monsterId1);
    Unit monster2 = unitComponent.Get(monsterId2);
    participants.Add(monster1);
    participants.Add(monster2);

    // 创建战斗组件并开始战斗
    var combatComponent = scene.AddComponent<TurnBasedCombatComponent>();
    await combatComponent.StartCombatAsync(participants, CombatType.Normal);
}
```

### 6.2 处理玩家输入

```csharp
// 玩家点击攻击按钮的处理
public static void OnAttackButtonClick(TurnBasedCombatComponent combat, long targetId)
{
    var objectWait = combat.GetComponent<ObjectWait>();
    objectWait?.Notify(new Wait_PlayerAction
    {
        Error = WaitTypeError.Success,
        ActionType = ActionType.Attack,
        TargetId = targetId
    });
}
```

## 七、扩展点说明

### 7.1 技能系统集成点

```csharp
// 在 CalculateDamage 方法中：
// 1. 检查是否使用技能
// 2. 从技能配置获取伤害倍率和效果
// 3. 应用技能特殊效果

// 在 ExecuteAction 方法中：
// 添加 UseSkill 动作类型的处理
// case ActionType.UseSkill:
//     await self.ExecuteSkill(unit, action.SkillId, action.TargetId);
//     break;
```

### 7.2 Buff系统集成点

```csharp
// 在 CalculateDamage 方法末尾：
// 1. 获取攻击者的增益Buff
// 2. 获取目标的减益Buff
// 3. 计算最终伤害修正

// 在回合开始/结束时：
// 1. 处理Buff的持续效果
// 2. 更新Buff持续时间
// 3. 移除过期Buff
```

### 7.3 断线重连完整实现

```csharp
// 需要实现的功能：
// 1. 保存战斗状态到数据库
// 2. 玩家重连时恢复战斗状态
// 3. 同步当前回合信息
// 4. 处理超时自动操作
```

## 八、注意事项

1. **日志使用英文**：所有Log输出必须使用英文
2. **Entity-System分离**：严格遵循ET框架规范
3. **EntityRef使用**：在async/await环境下正确使用EntityRef
4. **数值类型定义**：确保NumericType中定义了所需的数值类型
5. **消息发送**：使用ClientSenderComponent发送消息

## 九、测试建议

1. 创建简单的测试场景，包含1个玩家和2个怪物
2. 测试基础攻击和伤害计算
3. 验证暴击、闪避、格挡机制
4. 测试战斗结束条件
5. 验证随机数种子一致性

这个精简版的回合制战斗系统可以快速实现并测试，同时保留了良好的扩展性，方便后续添加完整的技能和Buff系统。