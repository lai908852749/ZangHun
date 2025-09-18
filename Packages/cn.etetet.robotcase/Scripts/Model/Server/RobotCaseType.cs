namespace ET.Server
{
    [UniqueId(1, 10000)]
    public static class RobotCaseType
    {
        public const int CreateRobot = 1;

        public const int SecondCase = 2;

        public const int QuestTest = 3;

        public const int AchievementTest = 4;

        public const int AchievementProgressTest = 5;

        public const int AchievementTriggerTest = 6;

        public const int AchievementCategoryTest = 7;

        public const int AchievementExceptionTest = 8;

        public const int TurnBasedCombat = 9;

        public const int BuffBasicTurnBased = 10;

        public const int BuffTagSystem = 11;

        public const int BuffMutexGroup = 12;

        public const int BuffImmunitySystem = 13;

        public const int BuffEffectNodes = 14;

        public const int BuffProgressiveEffects = 15;

        public const int BuffSpread = 16;

        public const int DamageAccumulation = 17;

        public const int HealAccumulation = 18;

        public const int ComplexScenario = 19;

        public const int MaxCaseType = 10000;
    }
}