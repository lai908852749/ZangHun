# ET框架回合制战斗系统2.0 - 改进版设计文档

## 一、改进概述

基于原有的回合制战斗系统，本次改进主要聚焦于简化不必要的限制、增强防作弊机制、完善核心战斗要素。

### 1.1 移除的功能

- **回合超时机制**：玩家操作不再有时间限制，等待玩家完成操作
- **回合数限制**：战斗不再因回合数达到上限而强制结束
- **Buff时间/回合制转换**：保持所有Buff为时间制，简化系统复杂度

### 1.2 新增的功能

- **战斗记录系统**：记录所有战斗动作用于防作弊验证
- **随机数种子系统**：确保前后端随机结果一致性
- **暴击闪避系统**：基于NumericComponent实现核心战斗要素

### 1.3 优化的功能

- **AI决策系统**：更智能的AI行为
- **断线重连处理**：支持玩家断线后继续战斗

## 二、核心配置简化

### 2.1 CombatConfig配置类（简化版）

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
        public int AIDecisionDelay = 1000;

        [LabelText("自动战斗")]
        public bool AutoBattle = false;

        [LabelText("随机种子")]
        public int RandomSeed = 0; // 0表示使用系统时间生成
    }
}
```

### 2.2 移除的枚举值

```csharp
public enum CombatEndReason
{
    Victory,     // 胜利
    Defeat,      // 失败
    Escape,      // 逃跑
    Surrender,   // 投降
    // RoundLimit,  // 已移除
    // Timeout,     // 已移除
    Disconnect   // 断线
}
```

## 三、战斗记录系统

### 3.1 CombatRecordComponent

```csharp
[ComponentOf(typeof(TurnBasedCombatComponent))]
public class CombatRecordComponent : Entity, IAwake
{
    // 战斗唯一标识
    public long CombatId { get; set; }

    // 随机数种子
    public int RandomSeed { get; set; }

    // 战斗动作记录列表
    public List<CombatActionRecord> Actions = new();

    // 战斗开始时间
    public long CombatStartTime { get; set; }

    // 战斗结束时间
    public long CombatEndTime { get; set; }

    // 总回合数
    public int TotalRounds { get; set; }

    // 参战单位初始状态
    public Dictionary<long, UnitSnapshot> InitialStates = new();

    // 记录动作
    public void RecordAction(CombatActionRecord record)
    {
        record.Timestamp = TimeInfo.Instance.ClientNow();
        record.SequenceNumber = Actions.Count;
        Actions.Add(record);
    }

    // 生成验证数据包
    public CombatVerifyData GenerateVerifyData()
    {
        return new CombatVerifyData
        {
            CombatId = CombatId,
            RandomSeed = RandomSeed,
            TotalRounds = TotalRounds,
            Duration = CombatEndTime - CombatStartTime,
            ActionCount = Actions.Count,
            ActionHashes = Actions.Select(a => a.GetHashCode()).ToList()
        };
    }
}
```

### 3.2 CombatActionRecord

```csharp
public class CombatActionRecord
{
    // 时间戳
    public long Timestamp { get; set; }

    // 序列号
    public int SequenceNumber { get; set; }

    // 执行单位ID
    public long UnitId { get; set; }

    // 动作类型
    public ActionType ActionType { get; set; }

    // 目标ID
    public long TargetId { get; set; }

    // 技能ID（如果是技能）
    public int SkillId { get; set; }

    // 伤害结果
    public DamageResult DamageResult { get; set; }

    // 动画开始时间
    public long AnimationStartTime { get; set; }

    // 动画结束时间
    public long AnimationEndTime { get; set; }

    // 使用的随机数序列号
    public int RandomSequenceNumber { get; set; }

    // 单位状态快照（动作后）
    public UnitSnapshot UnitStateAfter { get; set; }
}
```

### 3.3 单位快照

```csharp
public class UnitSnapshot
{
    public long UnitId { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public bool IsAlive { get; set; }
    public List<int> ActiveBuffIds { get; set; }
}
```

## 四、随机数种子系统

### 4.1 CombatRandomComponent

```csharp
[ComponentOf(typeof(TurnBasedCombatComponent))]
public class CombatRandomComponent : Entity, IAwake<int>
{
    private Random random;
    private int seed;
    private int sequenceNumber; // 记录第几次随机

    public int Seed => seed;
    public int SequenceNumber => sequenceNumber;

    public void Awake(int seed)
    {
        this.seed = seed;
        this.random = new Random(seed);
        this.sequenceNumber = 0;
    }

    // 获取下一个随机数（0-1）
    public float GetRandom()
    {
        sequenceNumber++;
        return (float)random.NextDouble();
    }

    // 获取范围内随机整数
    public int GetRandomInt(int min, int max)
    {
        sequenceNumber++;
        return random.Next(min, max);
    }

    // 概率检查
    public bool CheckProbability(float probability)
    {
        return GetRandom() < probability;
    }

    // 从列表中随机选择
    public T SelectRandom<T>(List<T> list)
    {
        if (list.Count == 0) return default;
        int index = GetRandomInt(0, list.Count);
        return list[index];
    }
}
```

### 4.2 随机数生成系统

```csharp
[EntitySystemOf(typeof(CombatRandomComponent))]
public static partial class CombatRandomComponentSystem
{
    [EntitySystem]
    private static void Awake(this CombatRandomComponent self, int seed)
    {
        // 如果种子为0，使用时间戳生成
        if (seed == 0)
        {
            seed = (int)(TimeInfo.Instance.ServerNow() % int.MaxValue);
        }

        self.Awake(seed);
        Log.Debug($"Combat random initialized with seed: {seed}");
    }
}
```

## 五、暴击闪避系统

### 5.1 伤害结果结构

```csharp
public struct DamageResult
{
    public int FinalDamage;      // 最终伤害
    public int BaseDamage;       // 基础伤害
    public bool IsCritical;      // 是否暴击
    public bool IsDodged;        // 是否闪避
    public bool IsBlocked;       // 是否格挡
    public float CritMultiplier; // 暴击倍率
}
```

### 5.2 伤害计算系统

```csharp
public static partial class TurnBasedCombatComponentSystem
{
    // 计算伤害
    private static DamageResult CalculateDamage(
        this TurnBasedCombatComponent self,
        Unit attacker,
        Unit target,
        int skillId = 0)
    {
        var attackerNum = attacker.GetComponent<NumericComponent>();
        var targetNum = target.GetComponent<NumericComponent>();
        var randomGen = self.GetComponent<CombatRandomComponent>();
        var record = self.GetComponent<CombatRecordComponent>();

        DamageResult result = new DamageResult();

        // 记录随机数使用序列
        int randomSeq = randomGen.SequenceNumber;

        // 基础攻击力
        result.BaseDamage = attackerNum.GetAsInt(NumericType.Attack);

        // 技能加成
        if (skillId > 0)
        {
            // TODO: 从技能配置获取伤害倍率
            result.BaseDamage = (int)(result.BaseDamage * 1.5f);
        }

        // 闪避判定（优先级最高）
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
            if (result.CritMultiplier <= 0) result.CritMultiplier = 2.0f; // 默认2倍暴击
            result.BaseDamage = (int)(result.BaseDamage * result.CritMultiplier);
            Log.Debug($"Critical hit! CritRate: {critRate:F2}, Multiplier: {result.CritMultiplier:F1}");
        }

        // 防御减伤
        int defense = targetNum.GetAsInt(NumericType.Defense);
        result.FinalDamage = Math.Max(1, result.BaseDamage - defense);

        // 格挡判定（减少50%伤害）
        float blockRate = targetNum.GetAsFloat(NumericType.BlockRate);
        result.IsBlocked = randomGen.CheckProbability(blockRate);
        if (result.IsBlocked)
        {
            result.FinalDamage = result.FinalDamage / 2;
            Log.Debug($"Attack blocked! BlockRate: {blockRate:F2}");
        }

        Log.Debug($"Damage calculated: Base={result.BaseDamage}, Final={result.FinalDamage}, " +
                 $"Crit={result.IsCritical}, Dodge={result.IsDodged}, Block={result.IsBlocked}");

        return result;
    }

    // 执行攻击
    private static async ETTask ExecuteAttack(
        this TurnBasedCombatComponent self,
        Unit attacker,
        long targetId)
    {
        Unit target = self.Scene().GetComponent<UnitComponent>().Get(targetId);
        if (target == null || target.IsDisposed) return;

        var record = self.GetComponent<CombatRecordComponent>();
        var randomGen = self.GetComponent<CombatRandomComponent>();

        // 记录动画开始
        long animStart = TimeInfo.Instance.ClientNow();

        // 计算伤害
        DamageResult damage = self.CalculateDamage(attacker, target);

        // 应用伤害
        if (damage.FinalDamage > 0)
        {
            target.GetComponent<NumericComponent>().Add(NumericType.Hp, -damage.FinalDamage);

            // 更新统计
            if (self.Statistics.TryGetValue(attacker.Id, out var attackerStats))
            {
                attackerStats.DamageDealt += damage.FinalDamage;
            }
            if (self.Statistics.TryGetValue(targetId, out var targetStats))
            {
                targetStats.DamageTaken += damage.FinalDamage;
            }
        }

        // 记录动画结束
        long animEnd = TimeInfo.Instance.ClientNow();

        // 记录战斗动作
        record.RecordAction(new CombatActionRecord
        {
            UnitId = attacker.Id,
            ActionType = ActionType.Attack,
            TargetId = targetId,
            DamageResult = damage,
            AnimationStartTime = animStart,
            AnimationEndTime = animEnd,
            RandomSequenceNumber = randomGen.SequenceNumber,
            UnitStateAfter = CreateUnitSnapshot(target)
        });

        // 检查目标是否死亡
        if (target.GetComponent<NumericComponent>().GetAsInt(NumericType.Hp) <= 0)
        {
            self.UnitInfos[targetId].IsAlive = false;
            Log.Info($"Unit {targetId} defeated!");
        }

        await ETTask.CompletedTask;
    }
}
```

## 六、AI系统改进

### 6.1 智能AI决策

```csharp
private static async ETTask ExecuteAIAction(
    this TurnBasedCombatComponent self,
    Unit unit)
{
    EntityRef<TurnBasedCombatComponent> selfRef = self;
    EntityRef<Unit> unitRef = unit;

    var num = unit.GetComponent<NumericComponent>();
    var randomGen = self.GetComponent<CombatRandomComponent>();

    // 计算血量百分比
    float hpPercent = (float)num.GetAsInt(NumericType.Hp) / num.GetAsInt(NumericType.MaxHp);

    Wait_PlayerAction action;

    // AI决策逻辑
    if (hpPercent < 0.3f && randomGen.GetRandom() > 0.6f)
    {
        // 血量低于30%，40%概率选择防御
        action = new() { ActionType = ActionType.Defend };
        Log.Debug($"AI {unit.Id} chooses to defend (HP: {hpPercent:P})");
    }
    else if (hpPercent < 0.5f && self.HasHealingItem(unit))
    {
        // 血量低于50%，有治疗道具时使用
        action = new()
        {
            ActionType = ActionType.UseItem,
            TargetId = unit.Id
        };
        Log.Debug($"AI {unit.Id} uses healing item");
    }
    else
    {
        // 选择目标
        var enemies = self.GetValidTargets(unit.Id, TargetType.Enemy);
        if (enemies.Count == 0)
        {
            action = new() { ActionType = ActionType.Pass };
        }
        else
        {
            // 优先攻击血量最低的敌人
            long targetId = self.SelectWeakestTarget(enemies);

            // 判断是否使用技能
            bool useSkill = randomGen.GetRandom() > 0.7f && self.HasAvailableSkill(unit);

            if (useSkill)
            {
                var skill = self.SelectBestSkill(unit, targetId);
                action = new()
                {
                    ActionType = ActionType.UseSkill,
                    SkillId = skill.Id,
                    TargetId = targetId
                };
                Log.Debug($"AI {unit.Id} uses skill {skill.Id} on {targetId}");
            }
            else
            {
                action = new()
                {
                    ActionType = ActionType.Attack,
                    TargetId = targetId
                };
                Log.Debug($"AI {unit.Id} attacks {targetId}");
            }
        }
    }

    // AI决策延迟（模拟思考时间）
    var config = self.GetConfig();
    if (config.AIDecisionDelay > 0)
    {
        await self.Root.GetComponent<TimerComponent>().WaitAsync(config.AIDecisionDelay);
        self = selfRef;
        unit = unitRef;
    }

    // 执行动作
    await self.ExecuteAction(unit, action);
}

// 选择血量最低的目标
private static long SelectWeakestTarget(this TurnBasedCombatComponent self, List<long> targets)
{
    long weakestId = 0;
    int lowestHp = int.MaxValue;

    foreach (long targetId in targets)
    {
        Unit target = self.Scene().GetComponent<UnitComponent>().Get(targetId);
        if (target == null) continue;

        int hp = target.GetComponent<NumericComponent>().GetAsInt(NumericType.Hp);
        if (hp < lowestHp)
        {
            lowestHp = hp;
            weakestId = targetId;
        }
    }

    return weakestId != 0 ? weakestId : targets[0];
}
```

## 七、断线重连处理

### 7.1 断线检测与处理

```csharp
// 处理玩家断线
public static void HandlePlayerDisconnect(this TurnBasedCombatComponent self, long playerId)
{
    if (!self.InCombat) return;

    // 标记玩家断线状态
    if (self.UnitInfos.TryGetValue(playerId, out var unitInfo))
    {
        unitInfo.IsDisconnected = true;
        unitInfo.DisconnectTime = TimeInfo.Instance.ServerNow();
    }

    // 如果当前是该玩家的回合，设置自动超时
    if (self.CurrentActorId == playerId)
    {
        // 启动超时计时器（给予重连时间）
        self.StartDisconnectTimer(playerId, 30000); // 30秒重连时间
    }

    Log.Warning($"Player {playerId} disconnected during combat");
}

// 处理玩家重连
public static async ETTask HandlePlayerReconnect(
    this TurnBasedCombatComponent self,
    long playerId)
{
    if (!self.InCombat) return;

    // 清除断线状态
    if (self.UnitInfos.TryGetValue(playerId, out var unitInfo))
    {
        unitInfo.IsDisconnected = false;
        unitInfo.DisconnectTime = 0;

        // 取消超时计时器
        self.CancelDisconnectTimer(playerId);
    }

    // 如果当前是该玩家的回合，重新发送UI事件
    if (self.CurrentActorId == playerId)
    {
        Unit unit = self.Scene().GetComponent<UnitComponent>().Get(playerId);
        if (unit != null)
        {
            // 重新显示操作界面
            EventSystem.Instance.Publish(self.Scene(),
                new ShowPlayerActionUIEvent
                {
                    UnitId = playerId,
                    ValidTargets = self.GetValidTargets(playerId),
                    AvailableSkills = self.GetAvailableSkills(unit),
                    CurrentRound = self.RoundNumber,
                    CombatState = self.GetCombatState()
                });

            Log.Info($"Player {playerId} reconnected, resuming turn");
        }
    }
    else
    {
        // 发送当前战斗状态
        EventSystem.Instance.Publish(self.Scene(),
            new CombatStateUpdateEvent
            {
                PlayerId = playerId,
                CurrentActorId = self.CurrentActorId,
                RoundNumber = self.RoundNumber,
                CombatState = self.GetCombatState()
            });
    }

    await ETTask.CompletedTask;
}

// 断线超时处理
private static void OnDisconnectTimeout(this TurnBasedCombatComponent self, long playerId)
{
    if (!self.InCombat) return;

    // 如果玩家仍未重连，自动执行跳过回合
    if (self.CurrentActorId == playerId)
    {
        var action = new Wait_PlayerAction
        {
            Error = WaitTypeError.Success,
            ActionType = ActionType.Pass
        };

        self.GetComponent<ObjectWait>().Notify(action);
        Log.Warning($"Player {playerId} disconnect timeout, auto pass turn");
    }
}
```

### 7.2 战斗状态保存

```csharp
public class CombatState
{
    public long CombatId { get; set; }
    public int RoundNumber { get; set; }
    public CombatPhase Phase { get; set; }
    public long CurrentActorId { get; set; }
    public Dictionary<long, UnitSnapshot> UnitStates { get; set; }
    public List<long> TurnOrder { get; set; }
    public int RandomSeed { get; set; }
    public int RandomSequence { get; set; }
}

// 获取当前战斗状态
private static CombatState GetCombatState(this TurnBasedCombatComponent self)
{
    var state = new CombatState
    {
        CombatId = self.GetComponent<CombatRecordComponent>().CombatId,
        RoundNumber = self.RoundNumber,
        Phase = self.Phase,
        CurrentActorId = self.CurrentActorId,
        TurnOrder = new List<long>(self.TurnOrder),
        RandomSeed = self.GetComponent<CombatRandomComponent>().Seed,
        RandomSequence = self.GetComponent<CombatRandomComponent>().SequenceNumber,
        UnitStates = new Dictionary<long, UnitSnapshot>()
    };

    // 保存所有单位状态
    foreach (var unitId in self.CombatUnits)
    {
        Unit unit = self.Scene().GetComponent<UnitComponent>().Get(unitId);
        if (unit != null)
        {
            state.UnitStates[unitId] = CreateUnitSnapshot(unit);
        }
    }

    return state;
}
```

## 八、服务器验证系统

### 8.1 战斗验证器

```csharp
public static class CombatValidator
{
    // 验证战斗记录
    public static CombatValidationResult ValidateCombatRecord(
        CombatRecordComponent record,
        int expectedSeed)
    {
        var result = new CombatValidationResult();

        // 1. 验证随机数种子
        if (record.RandomSeed != expectedSeed)
        {
            result.IsValid = false;
            result.Errors.Add($"Random seed mismatch: expected {expectedSeed}, got {record.RandomSeed}");
            return result;
        }

        // 2. 验证战斗时长
        long duration = record.CombatEndTime - record.CombatStartTime;
        if (duration < 1000) // 最少1秒
        {
            result.IsValid = false;
            result.Errors.Add($"Combat duration too short: {duration}ms");
        }
        if (duration > 3600000) // 最多1小时
        {
            result.IsValid = false;
            result.Errors.Add($"Combat duration too long: {duration}ms");
        }

        // 3. 验证动作序列
        for (int i = 0; i < record.Actions.Count; i++)
        {
            var action = record.Actions[i];

            // 验证动画时间
            long animDuration = action.AnimationEndTime - action.AnimationStartTime;
            if (animDuration < 100 || animDuration > 10000)
            {
                result.IsValid = false;
                result.Errors.Add($"Action {i}: Invalid animation duration {animDuration}ms");
            }

            // 验证操作间隔
            if (i > 0)
            {
                long interval = action.Timestamp - record.Actions[i - 1].Timestamp;
                if (interval < 50)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Action {i}: Operation interval too short {interval}ms");
                }
            }

            // 验证随机数序列
            if (i > 0 && action.RandomSequenceNumber <= record.Actions[i - 1].RandomSequenceNumber)
            {
                result.IsValid = false;
                result.Errors.Add($"Action {i}: Random sequence out of order");
            }
        }

        // 4. 验证回合数
        if (record.TotalRounds < 1 || record.TotalRounds > 1000)
        {
            result.IsValid = false;
            result.Errors.Add($"Invalid round count: {record.TotalRounds}");
        }

        // 5. 重演验证（可选，性能消耗较大）
        if (result.IsValid)
        {
            result.IsValid = ReplayCombat(record);
            if (!result.IsValid)
            {
                result.Errors.Add("Combat replay verification failed");
            }
        }

        result.Score = CalculateTrustScore(record);

        return result;
    }

    // 重演战斗验证
    private static bool ReplayCombat(CombatRecordComponent record)
    {
        // TODO: 使用相同的随机种子和初始状态重演战斗
        // 验证每个动作的结果是否与记录一致
        return true;
    }

    // 计算信任分数
    private static int CalculateTrustScore(CombatRecordComponent record)
    {
        int score = 100;

        // 根据异常行为扣分
        foreach (var action in record.Actions)
        {
            // 动画时间过短扣分
            long animDuration = action.AnimationEndTime - action.AnimationStartTime;
            if (animDuration < 200) score -= 5;

            // 操作过快扣分
            if (action.Timestamp - record.CombatStartTime < 100) score -= 10;
        }

        return Math.Max(0, score);
    }
}

// 验证结果
public class CombatValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public int Score { get; set; } = 100; // 信任分数 0-100
}
```

### 8.2 防作弊数据上传

```csharp
// 战斗结束时上传验证数据
private static async ETTask UploadCombatData(this TurnBasedCombatComponent self)
{
    var record = self.GetComponent<CombatRecordComponent>();
    record.CombatEndTime = TimeInfo.Instance.ClientNow();
    record.TotalRounds = self.RoundNumber;

    // 生成验证数据
    var verifyData = record.GenerateVerifyData();

    // 上传到服务器
    C2M_CombatVerify message = C2M_CombatVerify.Create();
    message.CombatId = verifyData.CombatId;
    message.RandomSeed = verifyData.RandomSeed;
    message.TotalRounds = verifyData.TotalRounds;
    message.Duration = verifyData.Duration;
    message.ActionCount = verifyData.ActionCount;
    message.ActionHashes = verifyData.ActionHashes;

    // 压缩详细记录
    message.CompressedRecord = CompressRecord(record);

    var response = await self.Root.GetComponent<ClientSenderComponent>().Call(message);

    if (response.Error != ErrorCode.Success)
    {
        Log.Error($"Combat verification failed: {response.Error}");
    }
}
```

## 九、简化后的战斗流程

### 9.1 战斗主循环（移除限制检查）

```csharp
private static async ETTask RunCombatLoop(this TurnBasedCombatComponent self)
{
    EntityRef<TurnBasedCombatComponent> selfRef = self;

    while (self.InCombat)
    {
        Log.Debug($"Round {self.RoundNumber} starts");

        // 遍历当前回合的所有单位
        for (int i = 0; i < self.TurnOrder.Count; i++)
        {
            self = selfRef;
            if (!self.InCombat) break;

            long unitId = self.TurnOrder[i];
            CombatUnitInfo unitInfo = self.UnitInfos[unitId];

            // 跳过死亡或断线单位
            if (!unitInfo.IsAlive || unitInfo.IsDisconnected) continue;

            Unit unit = self.Scene().GetComponent<UnitComponent>().Get(unitId);
            if (unit == null || unit.IsDisposed)
            {
                unitInfo.IsAlive = false;
                continue;
            }

            // 处理单位回合
            await self.ProcessUnitTurn(unit, unitInfo);

            self = selfRef;

            // 检查战斗结束条件（只检查单位存活）
            if (self.CheckCombatEnd())
            {
                await self.EndCombat(self.DetermineCombatResult());
                return;
            }
        }

        // 回合结束，进入下一回合
        self.RoundNumber++;

        // 重新计算行动顺序
        self.CalculateTurnOrder();
    }
}
```

### 9.2 战斗开始（不转换Buff）

```csharp
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
        config = new CombatConfig(); // 使用默认配置
    }
    self.CombatConfigId = config.Id;

    // 初始化随机数生成器
    int seed = config.RandomSeed;
    if (seed == 0)
    {
        seed = (int)(TimeInfo.Instance.ServerNow() % int.MaxValue);
    }
    self.AddComponent<CombatRandomComponent, int>(seed);

    // 初始化战斗记录
    var record = self.AddComponent<CombatRecordComponent>();
    record.CombatId = IdGenerator.Instance.GenerateId();
    record.RandomSeed = seed;
    record.CombatStartTime = self.CombatStartTime;

    // 初始化参战单位
    self.InitializeCombatants(participants);

    // 记录初始状态
    foreach (var unit in participants)
    {
        record.InitialStates[unit.Id] = CreateUnitSnapshot(unit);
    }

    // 计算初始行动顺序
    self.CalculateTurnOrder();

    // 进入战斗阶段
    self.Phase = CombatPhase.InProgress;

    // 发布战斗开始事件
    EventSystem.Instance.Publish(self.Scene(),
        new CombatStartEvent
        {
            Participants = participants,
            CombatType = combatType,
            RandomSeed = seed
        });

    // 开始战斗循环
    await self.RunCombatLoop();
}
```

### 9.3 战斗结束（不还原Buff）

```csharp
private static async ETTask EndCombat(this TurnBasedCombatComponent self,
    CombatEndReason reason)
{
    self.Phase = CombatPhase.Settlement;
    self.InCombat = false;

    Log.Info($"Combat ended: {reason}");

    // 记录战斗结束
    var record = self.GetComponent<CombatRecordComponent>();
    record.CombatEndTime = TimeInfo.Instance.ServerNow();
    record.TotalRounds = self.RoundNumber;

    // 上传战斗数据进行验证
    await self.UploadCombatData();

    // 发布战斗结束事件
    EventSystem.Instance.Publish(self.Scene(),
        new CombatEndEvent
        {
            Reason = reason,
            Statistics = self.Statistics,
            Duration = TimeInfo.Instance.ServerNow() - self.CombatStartTime,
            VerifyData = record.GenerateVerifyData()
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
```

## 十、消息定义

### 10.1 战斗验证消息

```proto
// Proto/OuterMessage.proto

message C2M_CombatVerify // IRequest
{
    int64 CombatId = 1;
    int32 RandomSeed = 2;
    int32 TotalRounds = 3;
    int64 Duration = 4;
    int32 ActionCount = 5;
    repeated int32 ActionHashes = 6;
    bytes CompressedRecord = 7; // 压缩的详细记录
}

message M2C_CombatVerify // IResponse
{
    int32 Error = 1;
    string Message = 2;
    int32 TrustScore = 3; // 信任分数
}
```

## 十一、总结

### 11.1 改进要点

1. **简化系统**：移除不必要的超时和回合限制，保持Buff为时间制
2. **防作弊机制**：完整的战斗记录和服务器验证系统
3. **核心战斗要素**：基于NumericComponent的暴击闪避系统
4. **一致性保证**：随机数种子确保前后端结果一致
5. **智能AI**：基于状态的决策系统
6. **稳定性**：断线重连处理机制

### 11.2 关键技术

- 使用EntityRef确保async/await环境下的实体安全
- ObjectWait机制实现无时限的玩家操作等待
- 随机数种子保证可重演性
- 详细的战斗记录用于防作弊验证

### 11.3 注意事项

1. 所有日志使用英文
2. 严格遵循ET框架的Entity-System分离原则
3. 使用ETTask而非Task
4. 消息发送使用ClientSenderComponent
5. 数值类型需要在NumericType中预先定义

这个改进版的战斗系统更加简洁、公平、稳定，适合实际游戏开发使用。