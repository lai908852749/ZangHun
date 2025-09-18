namespace ET
{
    public static partial class ErrorCode
    {
        public const int ERR_NotFoundUnit = ErrorCode.ERR_WithException + PackageType.WOW * 1000 + 1;
        public const int ERR_Exception = ErrorCode.ERR_WithException + PackageType.WOW * 1000 + 2;

        // 战斗相关错误码
        public const int ERR_CombatNoMonsters = ErrorCode.ERR_WithException + PackageType.WOW * 1000 + 3;
        public const int ERR_CombatAlreadyInProgress = ErrorCode.ERR_WithException + PackageType.WOW * 1000 + 4;
        public const int ERR_CombatStartFailed = ErrorCode.ERR_WithException + PackageType.WOW * 1000 + 5;
    }
}