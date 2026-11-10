namespace CasualtiesSize.Core
{
    public static class SizePolicy
    {
        public static bool MayResize(SizeRules rules, bool callerIsHost, int callerKey, int targetKey)
        {
            switch (rules.SizePermission)
            {
                case SizeRules.PermissionAnyone:
                    return true;
                case SizeRules.PermissionSelfOnly:
                    return callerIsHost || callerKey == targetKey;
                default:
                    return callerIsHost;
            }
        }

        public static bool MayChangeRules(bool callerIsHost)
        {
            return callerIsHost;
        }
    }
}
