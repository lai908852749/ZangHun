using System;
using System.Collections.Generic;

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

            Log.Debug($"[战斗开始] 回合数: {self.RoundNumber}, 行动顺序: {string.Join(",", self.TurnOrder)}");

            // 开始战斗循环
            await self.RunCombatLoop();
        }

        /// <summary>
        /// 计算行动顺序（按速度排序）
        /// </summary>
        private static void CalculateTurnOrder(this TurnBasedCombatComponent self)
        {
            self.TurnOrder.Clear();
            var unitComponent = self.Root().GetComponent<UnitComponent>();

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
                    if (monsterNum?.GetAsInt(NumericType.HP) > 0)
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
                Log.Debug($"[战斗] ========== 第 {self.RoundNumber} 回合开始 ==========");

                // 处理每个单位的回合
                for (int i = 0; i < self.TurnOrder.Count; i++)
                {
                    self = selfRef;
                    if (!self.InCombat) break;

                    self.CurrentTurnIndex = i;
                    long actorId = self.TurnOrder[i];
                    self.CurrentActorId = actorId;

                    Unit actor = self.Root().GetComponent<UnitComponent>().Get(actorId);
                    if (actor == null || actor.IsDisposed) continue;

                    // 检查单位是否死亡
                    var actorNum = actor.GetComponent<NumericComponent>();
                    if (actorNum.GetAsInt(NumericType.HP) <= 0) continue;

                    // 处理回合
                    if (actorId == self.PlayerUnitId)
                    {
                        // 玩家回合
                        Log.Debug($"[战斗] 玩家 {actorId} 的回合开始");
                        await self.ProcessPlayerTurn(actor);
                    }
                    else
                    {
                        // 怪物回合
                        Log.Debug($"[战斗] 怪物 {actorId} 的回合开始");
                        await self.ProcessMonsterTurn(actor);
                    }

                    self = selfRef;

                    // 检查战斗结束
                    if (self.CheckCombatEnd())
                    {
                        Log.Debug($"[战斗] 检测到战斗结束条件");
                        await self.EndCombat();
                        return;
                    }
                }

                // 进入下一回合
                self.RoundNumber++;
                Log.Debug($"[战斗] 第 {self.RoundNumber - 1} 回合结束，准备进入第 {self.RoundNumber} 回合");
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
            var unitComponent = self.Root().GetComponent<UnitComponent>();

            foreach (long monsterId in self.MonsterUnitIds)
            {
                Unit monster = unitComponent.Get(monsterId);
                if (monster != null && !monster.IsDisposed)
                {
                    var monsterNum = monster.GetComponent<NumericComponent>();
                    if (monsterNum.GetAsInt(NumericType.HP) > 0)
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
            MapMessageHelper.NoticeClient(player, turnMsg, NoticeType.Self);

            // 等待玩家操作
            long playerId = player.Id;
            Log.Debug($"[战斗] 等待玩家 {playerId} 输入指令...");

            Wait_PlayerCombatAction action;

            // 测试模式：暂时自动攻击，后续需要改回等待玩家输入
            bool isTestMode = true; // TODO: 后续改为 false，恢复等待玩家输入

            if (isTestMode)
            {
                // 测试模式：自动选择攻击目标
                if (validTargets.Count > 0)
                {
                    action = new Wait_PlayerCombatAction()
                    {
                        Error = 0,
                        ActionType = 1, // 攻击
                        TargetId = validTargets[0]
                    };
                    Log.Debug($"[战斗] 测试模式：自动攻击目标 {validTargets[0]}");
                }
                else
                {
                    action = new Wait_PlayerCombatAction()
                    {
                        Error = 0,
                        ActionType = 2, // 跳过
                        TargetId = 0
                    };
                    Log.Debug($"[战斗] 测试模式：无有效目标，跳过回合");
                }

                // 等待一小段时间模拟思考
                await self.Root().GetComponent<TimerComponent>().WaitAsync(100);
            }
            else
            {
                // 正常模式：等待玩家输入（保留原代码）
                var objectWait = self.Root().GetComponent<ObjectWait>() ?? self.Root().AddComponent<ObjectWait>();
                action = await objectWait.Wait<Wait_PlayerCombatAction>();
                Log.Debug($"[战斗] 收到玩家 {playerId} 的指令");
            }

            // 原始的等待玩家输入代码（注释保留）：
            // var objectWait = self.Root().GetComponent<ObjectWait>() ?? self.Root().AddComponent<ObjectWait>();
            // Wait_PlayerCombatAction action = await objectWait.Wait<Wait_PlayerCombatAction>();
            // Log.Debug($"[战斗] 收到玩家 {playerId} 的指令");

            self = selfRef;

            // 检查self是否还有效
            if (self == null || self.IsDisposed)
            {
                Log.Debug($"[战斗] 战斗组件已失效，结束战斗");
                return;
            }

            // 重新获取player引用
            var root = self.Root();
            if (root == null)
            {
                Log.Debug($"[战斗] 无法获取Root，结束战斗");
                return;
            }

            var unitComp = root.GetComponent<UnitComponent>();
            if (unitComp == null)
            {
                Log.Debug($"[战斗] 无法获取UnitComponent，结束战斗");
                return;
            }

            player = unitComp.Get(self.PlayerUnitId);
            if (player == null || player.IsDisposed)
            {
                Log.Debug($"[战斗] 玩家单位已失效，结束战斗");
                return;
            }

            // 执行玩家动作
            await self.ExecutePlayerAction(player, action);
        }

        /// <summary>
        /// 处理怪物回合
        /// </summary>
        private static async ETTask ProcessMonsterTurn(this TurnBasedCombatComponent self, Unit monster)
        {
            // 检查self是否还有效
            if (self == null || self.IsDisposed)
            {
                Log.Debug($"[战斗] 战斗组件已失效，结束怪物回合");
                return;
            }

            // 获取玩家单位
            var root = self.Root();
            if (root == null)
            {
                Log.Debug($"[战斗] 无法获取Root，结束怪物回合");
                return;
            }

            var unitComp = root.GetComponent<UnitComponent>();
            if (unitComp == null)
            {
                Log.Debug($"[战斗] 无法获取UnitComponent，结束怪物回合");
                return;
            }

            Unit player = unitComp.Get(self.PlayerUnitId);

            // 检查玩家是否存活
            if (player != null && !player.IsDisposed)
            {
                var playerNum = player.GetComponent<NumericComponent>();
                int playerHp = playerNum.GetAsInt(NumericType.HP);

                if (playerHp > 0)
                {
                    // 怪物攻击玩家
                    Log.Debug($"[战斗] 怪物 {monster.Id} 准备攻击玩家 {player.Id}");
                    await self.ExecuteAttack(monster, player);
                }
                else
                {
                    Log.Debug($"[战斗] 怪物 {monster.Id} 跳过回合 - 玩家已死亡");
                }
            }
            else
            {
                Log.Debug($"[战斗] 怪物 {monster.Id} 跳过回合 - 玩家未找到");
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
                    Unit target = self.Root().GetComponent<UnitComponent>().Get(action.TargetId);
                    if (target != null)
                    {
                        await self.ExecuteAttack(player, target);
                    }
                    break;

                case 2: // 跳过
                    Log.Debug($"[战斗] 玩家 {player.Id} 跳过回合");
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
            targetNum.Set(NumericType.HP, targetNum.GetAsInt(NumericType.HP) - damage);
            int remainHp = targetNum.GetAsInt(NumericType.HP);
            bool isDead = remainHp <= 0;

            string attackerType = attacker.Id == self.PlayerUnitId ? "玩家" : "怪物";
            string targetType = target.Id == self.PlayerUnitId ? "玩家" : "怪物";
            Log.Debug($"[战斗] {attackerType} {attacker.Id} 攻击 {targetType} {target.Id}，造成 {damage} 点伤害，剩余生命值: {remainHp}");

            // 记录战斗动作
            self.RecordAction(attacker.Id, target.Id, damage, 1);

            // 发送攻击结果
            M2C_CombatActionResult result = M2C_CombatActionResult.Create();
            result.AttackerId = attacker.Id;
            result.TargetId = target.Id;
            result.Damage = damage;
            result.RemainHp = Math.Max(0, remainHp);
            result.IsDead = isDead;

            Unit player = self.Root().GetComponent<UnitComponent>().Get(self.PlayerUnitId);
            if (player != null)
            {
                MapMessageHelper.NoticeClient(player, result, NoticeType.Self);
            }

            if (isDead)
            {
                string defeatedType = target.Id == self.PlayerUnitId ? "玩家" : "怪物";
                Log.Debug($"[战斗] {defeatedType} {target.Id} 被击败！");
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
            var unitComponent = self.Root().GetComponent<UnitComponent>();

            // 检查玩家是否死亡
            Unit player = unitComponent.Get(self.PlayerUnitId);
            if (player == null || player.IsDisposed)
            {
                return true;
            }

            var playerNum = player.GetComponent<NumericComponent>();
            if (playerNum.GetAsInt(NumericType.HP) <= 0)
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
                    if (monsterNum.GetAsInt(NumericType.HP) > 0)
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
            var unitComponent = self.Root().GetComponent<UnitComponent>();
            Unit player = unitComponent.Get(self.PlayerUnitId);
            bool isWin = false;

            if (player != null && !player.IsDisposed)
            {
                var playerNum = player.GetComponent<NumericComponent>();
                isWin = playerNum.GetAsInt(NumericType.HP) > 0;
            }

            // 更新战斗记录
            var record = self.GetComponent<CombatRecordComponent>();
            if (record != null)
            {
                record.TotalRounds = self.RoundNumber;
            }

            long duration = TimeInfo.Instance.ServerNow() - self.StartTime;

            Log.Debug($"[战斗结束] 结果: {(isWin ? "胜利" : "失败")}, 总回合数: {self.RoundNumber}, 持续时间: {duration}毫秒");

            // 发送战斗结束消息
            M2C_CombatEnd endMsg = M2C_CombatEnd.Create();
            endMsg.IsWin = isWin;
            endMsg.TotalRounds = self.RoundNumber;
            endMsg.Duration = duration;

            if (player != null)
            {
                MapMessageHelper.NoticeClient(player, endMsg, NoticeType.Self);
            }

            await ETTask.CompletedTask;
        }
    }
}