# ET框架回合制战斗模块Demo实现文档

## 一、需求总结

基于ET框架实现一个极简版回合制战斗系统，满足以下核心需求：

### 核心特性
- ✅ 基础回合制流程（按速度排序）
- ✅ 简单伤害计算（仅使用攻击力）
- ✅ 怪物自动攻击玩家（检查玩家HP>0）
- ✅ 战报发送服务器（总回合数）
- ✅ 一方死亡即结束战斗

### 简化要求
- ❌ 不需要防御、暴击、闪避、格挡等复杂属性
- ❌ 不需要AI系统，怪物固定攻击玩家
- ❌ 不需要随机数种子
- ❌ 不需要断线重连
- ❌ 不需要战斗配置类
- ❌ 玩家角色只有一个


## 二、实现架构设计

### 1. Proto消息定义

在`WOW_C_10700.proto`文件末尾添加：

```protobuf
// ============= 回合制战斗相关消息 =============

// ResponseType M2C_StartCombat
message C2M_StartCombat // ILocationRequest
{
    int32 RpcId = 1;
    repeated int64 MonsterIds = 2;  // 要战斗的怪物单位ID列表
}

message M2C_StartCombat // ILocationResponse
{
    int32 RpcId = 1;
    int32 Error = 2;
    string Message = 3;
    repeated int64 TurnOrder = 4;   // 行动顺序（单位ID列表）
}

// 玩家战斗动作（单向消息，无需响应）
message C2M_CombatAction // IMessage
{
    int32 ActionType = 1;  // 1=攻击 2=跳过
    int64 TargetId = 2;    // 目标单位ID
}

// 战斗回合开始通知
message M2C_CombatTurnStart // IMessage
{
    int64 ActorId = 1;      // 当前行动者ID
    bool IsPlayerTurn = 2;  // 是否玩家回合
    repeated int64 ValidTargets = 3;  // 可攻击目标列表
}

// 战斗动作结果
message M2C_CombatActionResult // IMessage
{
    int64 AttackerId = 1;
    int64 TargetId = 2;
    int32 Damage = 3;
    int32 RemainHp = 4;
    bool IsDead = 5;
}

// 战斗结束
message M2C_CombatEnd // IMessage
{
    bool IsWin = 1;
    int32 TotalRounds = 2;
    int64 Duration = 3;     // 战斗持续时间(毫秒)
}
```

### 2. Entity组件设计

#### TurnBasedCombatComponent（主战斗组件）
**位置**: `Packages/cn.etetet.wow/Scripts/Model/Server/Combat/`

```csharp
namespace ET.Server
{
    /// <summary>
    /// 回合制战斗主组件
    /// </summary>
    [ComponentOf(typeof(Unit))]
    public class TurnBasedCombatComponent : Entity, IAwake, IDestroy
    {
        /// <summary>
        /// 是否在战斗中
        /// </summary>
        public bool InCombat;

        /// <summary>
        /// 当前回合数
        /// </summary>
        public int RoundNumber;

        /// <summary>
        /// 行动顺序列表（按速度排序）
        /// </summary>
        public List<long> TurnOrder = new();

        /// <summary>
        /// 当前行动者在TurnOrder中的索引
        /// </summary>
        public int CurrentTurnIndex;

        /// <summary>
        /// 玩家单位ID
        /// </summary>
        public long PlayerUnitId;

        /// <summary>
        /// 怪物单位ID列表
        /// </summary>
        public List<long> MonsterUnitIds = new();

        /// <summary>
        /// 当前行动者单位ID
        /// </summary>
        public long CurrentActorId;

        /// <summary>
        /// 战斗开始时间
        /// </summary>
        public long StartTime;
    }
}
```

#### CombatRecordComponent（战斗记录组件）
**位置**: `Packages/cn.etetet.wow/Scripts/Model/Server/Combat/`

```csharp
namespace ET.Server
{
    /// <summary>
    /// 战斗记录组件
    /// </summary>
    [ComponentOf(typeof(TurnBasedCombatComponent))]
    public class CombatRecordComponent : Entity, IAwake
    {
        /// <summary>
        /// 总回合数
        /// </summary>
        public int TotalRounds;

        /// <summary>
        /// 战斗动作记录列表
        /// </summary>
        public List<CombatAction> Actions = new();
    }

    /// <summary>
    /// 战斗动作记录
    /// </summary>
    public struct CombatAction
    {
        public long AttackerId;
        public long TargetId;
        public int Damage;
        public long Timestamp;
        public int ActionType; // 1=攻击 2=跳过
    }
}
```

### 3. System逻辑实现

#### TurnBasedCombatComponentSystem（主战斗系统）
**位置**: `Packages/cn.etetet.wow/Scripts/Hotfix/Server/Combat/`

```csharp
namespace ET.Server
{
    [EntitySystemOf(typeof(TurnBasedCombatComponent))]
    public static partial class TurnBasedCombatComponentSystem
    {
        [EntitySystem]
        private static void Awake(this TurnBasedCombatComponent self)
        {
            self.InCombat = false;
            self.RoundNumber = 0;
            self.CurrentTurnIndex = 0;
        }

        [EntitySystem]
        private static void Destroy(this TurnBasedCombatComponent self)
        {
            self.TurnOrder.Clear();
            self.MonsterUnitIds.Clear();
        }

        /// <summary>
        /// 开始战斗
        /// </summary>
        public static async ETTask StartCombatAsync(this TurnBasedCombatComponent self,
            Unit player, List<Unit> monsters)
        {
            self.InCombat = true;
            self.RoundNumber = 1;
            self.PlayerUnitId = player.Id;
            self.StartTime = TimeInfo.Instance.ServerNow();

            // 记录怪物ID
            self.MonsterUnitIds.Clear();
            foreach (var monster in monsters)
            {
                self.MonsterUnitIds.Add(monster.Id);
            }

            // 添加战斗记录组件
            self.AddComponent<CombatRecordComponent>();

            // 计算行动顺序
            self.CalculateTurnOrder();

            Log.Debug($"Combat started, turn order: {string.Join(",", self.TurnOrder)}");

            // 开始战斗循环
            await self.RunCombatLoop();
        }

        /// <summary>
        /// 计算行动顺序（按速度排序）
        /// </summary>
        private static void CalculateTurnOrder(this TurnBasedCombatComponent self)
        {
            self.TurnOrder.Clear();
            var unitComponent = self.Root.GetComponent<UnitComponent>();

            // 收集所有存活单位
            var allUnits = new List<(long id, int speed)>();

            // 添加玩家
            Unit player = unitComponent.Get(self.PlayerUnitId);
            if (player != null && !player.IsDisposed)
            {
                int playerSpeed = player.GetComponent<NumericComponent>()?.GetAsInt(NumericType.Speed) ?? 0;
                allUnits.Add((player.Id, playerSpeed));
            }

            // 添加存活的怪物
            foreach (long monsterId in self.MonsterUnitIds)
            {
                Unit monster = unitComponent.Get(monsterId);
                if (monster != null && !monster.IsDisposed)
                {
                    var monsterNum = monster.GetComponent<NumericComponent>();
                    if (monsterNum?.GetAsInt(NumericType.Hp) > 0)
                    {
                        int monsterSpeed = monsterNum.GetAsInt(NumericType.Speed);
                        allUnits.Add((monster.Id, monsterSpeed));
                    }
                }
            }

            // 按速度降序排序
            allUnits.Sort((a, b) => b.speed.CompareTo(a.speed));

            // 更新行动顺序
            foreach (var (id, _) in allUnits)
            {
                self.TurnOrder.Add(id);
            }

            self.CurrentTurnIndex = 0;
        }

        /// <summary>
        /// 战斗主循环
        /// </summary>
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

                    self.CurrentTurnIndex = i;
                    long actorId = self.TurnOrder[i];
                    self.CurrentActorId = actorId;

                    Unit actor = self.Root.GetComponent<UnitComponent>().Get(actorId);
                    if (actor == null || actor.IsDisposed) continue;

                    // 检查单位是否死亡
                    var actorNum = actor.GetComponent<NumericComponent>();
                    if (actorNum.GetAsInt(NumericType.Hp) <= 0) continue;

                    // 处理回合
                    if (actorId == self.PlayerUnitId)
                    {
                        // 玩家回合
                        await self.ProcessPlayerTurn(actor);
                    }
                    else
                    {
                        // 怪物回合
                        await self.ProcessMonsterTurn(actor);
                    }

                    self = selfRef;

                    // 检查战斗结束
                    if (self.CheckCombatEnd())
                    {
                        await self.EndCombat();
                        return;
                    }
                }

                // 进入下一回合
                self.RoundNumber++;
                self.CalculateTurnOrder();
            }
        }

        /// <summary>
        /// 处理玩家回合
        /// </summary>
        private static async ETTask ProcessPlayerTurn(this TurnBasedCombatComponent self, Unit player)
        {
            EntityRef<TurnBasedCombatComponent> selfRef = self;

            // 获取可攻击的目标列表
            var validTargets = new List<long>();
            var unitComponent = self.Root.GetComponent<UnitComponent>();

            foreach (long monsterId in self.MonsterUnitIds)
            {
                Unit monster = unitComponent.Get(monsterId);
                if (monster != null && !monster.IsDisposed)
                {
                    var monsterNum = monster.GetComponent<NumericComponent>();
                    if (monsterNum.GetAsInt(NumericType.Hp) > 0)
                    {
                        validTargets.Add(monsterId);
                    }
                }
            }

            // 发送回合开始通知
            M2C_CombatTurnStart turnMsg = M2C_CombatTurnStart.Create();
            turnMsg.ActorId = player.Id;
            turnMsg.IsPlayerTurn = true;
            turnMsg.ValidTargets.AddRange(validTargets);
            MapMessageHelper.SendToClient(player, turnMsg);

            // 等待玩家操作
            var objectWait = self.Root.GetComponent<ObjectWait>() ?? self.Root.AddComponent<ObjectWait>();
            Wait_PlayerCombatAction action = await objectWait.Wait<Wait_PlayerCombatAction>();

            self = selfRef;

            // 执行玩家动作
            await self.ExecutePlayerAction(player, action);
        }

        /// <summary>
        /// 处理怪物回合
        /// </summary>
        private static async ETTask ProcessMonsterTurn(this TurnBasedCombatComponent self, Unit monster)
        {
            // 获取玩家单位
            Unit player = self.Root.GetComponent<UnitComponent>().Get(self.PlayerUnitId);

            // 检查玩家是否存活
            if (player != null && !player.IsDisposed)
            {
                var playerNum = player.GetComponent<NumericComponent>();
                int playerHp = playerNum.GetAsInt(NumericType.Hp);

                if (playerHp > 0)
                {
                    // 怪物攻击玩家
                    await self.ExecuteAttack(monster, player);
                }
                else
                {
                    Log.Debug($"Monster {monster.Id} skips turn - player is dead");
                }
            }
            else
            {
                Log.Debug($"Monster {monster.Id} skips turn - player not found");
            }
        }

        /// <summary>
        /// 执行玩家动作
        /// </summary>
        private static async ETTask ExecutePlayerAction(this TurnBasedCombatComponent self,
            Unit player, Wait_PlayerCombatAction action)
        {
            switch (action.ActionType)
            {
                case 1: // 攻击
                    Unit target = self.Root.GetComponent<UnitComponent>().Get(action.TargetId);
                    if (target != null)
                    {
                        await self.ExecuteAttack(player, target);
                    }
                    break;

                case 2: // 跳过
                    Log.Debug($"Player {player.Id} skips turn");
                    self.RecordAction(player.Id, 0, 0, 2);
                    break;

                default:
                    Log.Warning($"Unknown action type: {action.ActionType}");
                    break;
            }
        }

        /// <summary>
        /// 执行攻击（极简伤害计算）
        /// </summary>
        private static async ETTask ExecuteAttack(this TurnBasedCombatComponent self,
            Unit attacker, Unit target)
        {
            // 获取攻击力（仅使用攻击力，无其他计算）
            var attackerNum = attacker.GetComponent<NumericComponent>();
            int damage = attackerNum.GetAsInt(NumericType.Attack);

            // 扣除目标血量
            var targetNum = target.GetComponent<NumericComponent>();
            targetNum.Add(NumericType.Hp, -damage);
            int remainHp = targetNum.GetAsInt(NumericType.Hp);
            bool isDead = remainHp <= 0;

            Log.Debug($"Unit {attacker.Id} attacks {target.Id} for {damage} damage, remain HP: {remainHp}");

            // 记录战斗动作
            self.RecordAction(attacker.Id, target.Id, damage, 1);

            // 发送攻击结果
            M2C_CombatActionResult result = M2C_CombatActionResult.Create();
            result.AttackerId = attacker.Id;
            result.TargetId = target.Id;
            result.Damage = damage;
            result.RemainHp = Math.Max(0, remainHp);
            result.IsDead = isDead;

            Unit player = self.Root.GetComponent<UnitComponent>().Get(self.PlayerUnitId);
            if (player != null)
            {
                MapMessageHelper.SendToClient(player, result);
            }

            if (isDead)
            {
                Log.Debug($"Unit {target.Id} is defeated!");
            }

            await ETTask.CompletedTask;
        }

        /// <summary>
        /// 记录战斗动作
        /// </summary>
        private static void RecordAction(this TurnBasedCombatComponent self,
            long attackerId, long targetId, int damage, int actionType)
        {
            var record = self.GetComponent<CombatRecordComponent>();
            if (record != null)
            {
                var action = new CombatAction
                {
                    AttackerId = attackerId,
                    TargetId = targetId,
                    Damage = damage,
                    ActionType = actionType,
                    Timestamp = TimeInfo.Instance.ServerNow()
                };
                record.Actions.Add(action);
            }
        }

        /// <summary>
        /// 检查战斗结束条件
        /// </summary>
        private static bool CheckCombatEnd(this TurnBasedCombatComponent self)
        {
            var unitComponent = self.Root.GetComponent<UnitComponent>();

            // 检查玩家是否死亡
            Unit player = unitComponent.Get(self.PlayerUnitId);
            if (player == null || player.IsDisposed)
            {
                return true;
            }

            var playerNum = player.GetComponent<NumericComponent>();
            if (playerNum.GetAsInt(NumericType.Hp) <= 0)
            {
                return true; // 玩家死亡，战斗结束
            }

            // 检查是否还有存活的怪物
            bool hasAliveMonster = false;
            foreach (long monsterId in self.MonsterUnitIds)
            {
                Unit monster = unitComponent.Get(monsterId);
                if (monster != null && !monster.IsDisposed)
                {
                    var monsterNum = monster.GetComponent<NumericComponent>();
                    if (monsterNum.GetAsInt(NumericType.Hp) > 0)
                    {
                        hasAliveMonster = true;
                        break;
                    }
                }
            }

            return !hasAliveMonster; // 所有怪物死亡，战斗结束
        }

        /// <summary>
        /// 结束战斗
        /// </summary>
        private static async ETTask EndCombat(this TurnBasedCombatComponent self)
        {
            self.InCombat = false;

            // 判断胜负
            var unitComponent = self.Root.GetComponent<UnitComponent>();
            Unit player = unitComponent.Get(self.PlayerUnitId);
            bool isWin = false;

            if (player != null && !player.IsDisposed)
            {
                var playerNum = player.GetComponent<NumericComponent>();
                isWin = playerNum.GetAsInt(NumericType.Hp) > 0;
            }

            // 更新战斗记录
            var record = self.GetComponent<CombatRecordComponent>();
            if (record != null)
            {
                record.TotalRounds = self.RoundNumber;
            }

            long duration = TimeInfo.Instance.ServerNow() - self.StartTime;

            Log.Debug($"Combat ended: {(isWin ? "Win" : "Defeat")}, Rounds: {self.RoundNumber}, Duration: {duration}ms");

            // 发送战斗结束消息
            M2C_CombatEnd endMsg = M2C_CombatEnd.Create();
            endMsg.IsWin = isWin;
            endMsg.TotalRounds = self.RoundNumber;
            endMsg.Duration = duration;

            if (player != null)
            {
                MapMessageHelper.SendToClient(player, endMsg);
            }

            await ETTask.CompletedTask;
        }
    }
}
```

#### CombatRecordComponentSystem（战斗记录系统）
**位置**: `Packages/cn.etetet.wow/Scripts/Hotfix/Server/Combat/`

```csharp
namespace ET.Server
{
    [EntitySystemOf(typeof(CombatRecordComponent))]
    public static partial class CombatRecordComponentSystem
    {
        [EntitySystem]
        private static void Awake(this CombatRecordComponent self)
        {
            self.TotalRounds = 0;
            self.Actions.Clear();
        }

        /// <summary>
        /// 获取战报数据
        /// </summary>
        public static string GetCombatReport(this CombatRecordComponent self)
        {
            return $"Total Rounds: {self.TotalRounds}, Total Actions: {self.Actions.Count}";
        }
    }
}
```

### 4. 消息处理Handler

#### C2M_StartCombatHandler
**位置**: `Packages/cn.etetet.wow/Scripts/Hotfix/Server/Combat/Handler/`

```csharp
namespace ET.Server
{
    [MessageHandler(SceneType.Map)]
    public class C2M_StartCombatHandler : MessageLocationHandler<Unit, C2M_StartCombat, M2C_StartCombat>
    {
        protected override async ETTask Run(Unit unit, C2M_StartCombat request, M2C_StartCombat response)
        {
            try
            {
                // 获取怪物单位
                var monsters = new List<Unit>();
                var unitComponent = unit.Root.GetComponent<UnitComponent>();

                foreach (long monsterId in request.MonsterIds)
                {
                    Unit monster = unitComponent.Get(monsterId);
                    if (monster != null)
                    {
                        monsters.Add(monster);
                    }
                }

                if (monsters.Count == 0)
                {
                    response.Error = ErrorCore.ERR_CombatNoMonsters;
                    response.Message = "No valid monsters found";
                    return;
                }

                // 检查是否已在战斗中
                var existingCombat = unit.GetComponent<TurnBasedCombatComponent>();
                if (existingCombat != null && existingCombat.InCombat)
                {
                    response.Error = ErrorCore.ERR_CombatAlreadyInProgress;
                    response.Message = "Already in combat";
                    return;
                }

                // 移除旧的战斗组件
                unit.RemoveComponent<TurnBasedCombatComponent>();

                // 创建新的战斗组件
                var combat = unit.AddComponent<TurnBasedCombatComponent>();

                // 开始战斗
                await combat.StartCombatAsync(unit, monsters);

                // 返回行动顺序
                response.TurnOrder.AddRange(combat.TurnOrder);

                Log.Debug($"Combat started for player {unit.Id}");
            }
            catch (Exception e)
            {
                Log.Error($"Start combat error: {e}");
                response.Error = ErrorCore.ERR_CombatStartFailed;
                response.Message = e.Message;
            }
        }
    }
}
```

#### C2M_CombatActionHandler
**位置**: `Packages/cn.etetet.wow/Scripts/Hotfix/Server/Combat/Handler/`

```csharp
namespace ET.Server
{
    [MessageHandler(SceneType.Map)]
    public class C2M_CombatActionHandler : MessageLocationHandler<Unit, C2M_CombatAction>
    {
        protected override async ETTask Run(Unit unit, C2M_CombatAction message)
        {
            try
            {
                var combat = unit.GetComponent<TurnBasedCombatComponent>();
                if (combat == null || !combat.InCombat)
                {
                    Log.Warning($"Player {unit.Id} not in combat");
                    return;
                }

                // 检查是否是玩家回合
                if (combat.CurrentActorId != unit.Id)
                {
                    Log.Warning($"Not player {unit.Id} turn, current: {combat.CurrentActorId}");
                    return;
                }

                // 通知等待的PlayerTurn方法
                var objectWait = unit.Root.GetComponent<ObjectWait>();
                if (objectWait != null)
                {
                    var action = new Wait_PlayerCombatAction
                    {
                        Error = WaitTypeError.Success,
                        ActionType = message.ActionType,
                        TargetId = message.TargetId
                    };

                    objectWait.Notify(action);
                }

                Log.Debug($"Player {unit.Id} action: {message.ActionType}, target: {message.TargetId}");
            }
            catch (Exception e)
            {
                Log.Error($"Combat action error: {e}");
            }

            await ETTask.CompletedTask;
        }
    }
}
```

### 5. 等待类型定义

**位置**: `Packages/cn.etetet.wow/Scripts/Model/Server/Combat/`

```csharp
namespace ET.Server
{
    /// <summary>
    /// 等待玩家战斗动作
    /// </summary>
    public class Wait_PlayerCombatAction : IWaitType
    {
        public int Error { get; set; }
        public int ActionType { get; set; } // 1=攻击 2=跳过
        public long TargetId { get; set; }
    }
}
```

## 三、测试用例设计

### RobotCase_TurnBasedCombat_Handler
**位置**: `Packages/cn.etetet.robotcase/Scripts/Hotfix/Server/`

```csharp
namespace ET
{
    [RobotCaseHandler(RobotCaseType.TurnBasedCombat)]
    public class RobotCase_TurnBasedCombat_Handler : ARobotCaseHandler
    {
        protected override async ETTask Run(RobotCase robotCase, ETTask waitetype)
        {
            using Robot robot = new Robot(robotCase.Fiber, robotCase.Zone, "TurnBasedCombatRobot");

            try
            {
                // 登录机器人
                await robot.LoginAsync();
                Log.Console("Robot logged in successfully");

                // 进入地图
                await robot.EnterMapAsync();
                Log.Console("Robot entered map successfully");

                // 创建测试怪物
                var monsters = await CreateTestMonsters(robot);
                Log.Console($"Created {monsters.Count} test monsters");

                // 开始战斗
                var combatResult = await StartCombat(robot, monsters);
                if (combatResult.Error != ErrorCore.OK)
                {
                    throw new Exception($"Start combat failed: {combatResult.Message}");
                }

                Log.Console($"Combat started, turn order: {string.Join(",", combatResult.TurnOrder)}");

                // 模拟战斗流程
                await SimulateCombat(robot);

                Log.Console("Turn-based combat test completed successfully");
            }
            catch (Exception e)
            {
                Log.Error($"Turn-based combat test failed: {e}");
                throw;
            }
        }

        private async ETTask<List<long>> CreateTestMonsters(Robot robot)
        {
            var monsters = new List<long>();

            // 创建2个测试怪物
            // 这里应该调用创建怪物的相关API
            // 暂时模拟怪物ID
            monsters.Add(100001);
            monsters.Add(100002);

            return monsters;
        }

        private async ETTask<M2C_StartCombat> StartCombat(Robot robot, List<long> monsters)
        {
            C2M_StartCombat startMsg = C2M_StartCombat.Create();
            startMsg.MonsterIds.AddRange(monsters);

            var response = await robot.Fiber.Root.GetComponent<ClientSenderComponent>()
                .Call(startMsg) as M2C_StartCombat;

            return response;
        }

        private async ETTask SimulateCombat(Robot robot)
        {
            // 监听战斗消息
            bool combatEnded = false;
            bool isWin = false;
            int totalRounds = 0;

            EventSystem.Instance.RegisterCallback<M2C_CombatTurnStart>(robot.Fiber, OnCombatTurnStart);
            EventSystem.Instance.RegisterCallback<M2C_CombatActionResult>(robot.Fiber, OnCombatActionResult);
            EventSystem.Instance.RegisterCallback<M2C_CombatEnd>(robot.Fiber, OnCombatEnd);

            // 等待战斗结束
            while (!combatEnded)
            {
                await robot.Fiber.Root.GetComponent<TimerComponent>().WaitAsync(100);
            }

            // 验证战斗结果
            if (!isWin)
            {
                throw new Exception("Combat should result in victory");
            }

            if (totalRounds <= 0)
            {
                throw new Exception("Total rounds should be greater than 0");
            }

            Log.Console($"Combat ended: Win={isWin}, Rounds={totalRounds}");

            // 本地函数：处理回合开始
            void OnCombatTurnStart(M2C_CombatTurnStart msg)
            {
                Log.Console($"Turn start: Actor={msg.ActorId}, IsPlayer={msg.IsPlayerTurn}");

                if (msg.IsPlayerTurn && msg.ValidTargets.Count > 0)
                {
                    // 玩家回合：随机攻击一个目标
                    long targetId = msg.ValidTargets[0];

                    C2M_CombatAction action = C2M_CombatAction.Create();
                    action.ActionType = 1; // 攻击
                    action.TargetId = targetId;

                    robot.Fiber.Root.GetComponent<ClientSenderComponent>().Send(action);
                    Log.Console($"Player attacks target {targetId}");
                }
            }

            // 本地函数：处理攻击结果
            void OnCombatActionResult(M2C_CombatActionResult msg)
            {
                Log.Console($"Attack result: {msg.AttackerId} -> {msg.TargetId}, " +
                           $"Damage={msg.Damage}, RemainHP={msg.RemainHp}, Dead={msg.IsDead}");
            }

            // 本地函数：处理战斗结束
            void OnCombatEnd(M2C_CombatEnd msg)
            {
                combatEnded = true;
                isWin = msg.IsWin;
                totalRounds = msg.TotalRounds;

                Log.Console($"Combat ended: Win={msg.IsWin}, Rounds={msg.TotalRounds}, Duration={msg.Duration}ms");
            }
        }
    }
}
```

## 四、实现步骤

### 步骤1：添加Proto消息定义
1. 在`WOW_C_10700.proto`文件末尾添加战斗相关消息
2. 运行`dotnet build ET.sln`编译生成C#文件

### 步骤2：创建Entity组件
1. 创建`TurnBasedCombatComponent`
2. 创建`CombatRecordComponent`
3. 创建等待类型`Wait_PlayerCombatAction`

### 步骤3：实现System逻辑
1. 实现`TurnBasedCombatComponentSystem`的核心方法
2. 实现`CombatRecordComponentSystem`

### 步骤4：创建消息Handler
1. 实现`C2M_StartCombatHandler`
2. 实现`C2M_CombatActionHandler`

### 步骤5：创建测试用例
1. 实现`RobotCase_TurnBasedCombat_Handler`
2. 运行测试验证功能

### 步骤6：测试和调试
1. 编译整个项目
2. 运行机器人测试用例
3. 检查日志，确保流程正常

## 五、核心简化点

1. **伤害计算极简**：`damage = attacker.Attack`，无其他计算
2. **怪物AI极简**：直接攻击玩家（HP>0时），无复杂决策
3. **战报极简**：仅发送总回合数和胜负结果
4. **战斗结束条件**：玩家死亡或所有怪物死亡
5. **无配置依赖**：直接读取单位的Attack、HP、Speed属性

## 六、扩展点

未来可以在以下地方扩展功能：
1. `ExecuteAttack`方法中添加复杂伤害计算
2. `ProcessMonsterTurn`方法中添加AI决策
3. `CombatRecordComponent`中添加更详细的战报数据
4. 添加技能系统、Buff系统等

这个极简版本确保了回合制战斗的核心流程完整，易于实现和测试，同时为后续扩展预留了清晰的接口。