using System.Collections.Generic;
using CasualtiesSize.Core;

namespace CasualtiesSize
{
    public static class CaptureCommands
    {
        public static string Execute(SizeCommand command, ICaller caller, ISizeEnvironment environment)
        {
            if (command.Verb == SizeVerb.Release) return Release(command, caller, environment);
            return Capture(command, caller, environment);
        }

        private static string Capture(SizeCommand command, ICaller caller, ISizeEnvironment environment)
        {
            int holderKey;
            int heldKey;
            string error;
            if (!ResolveOne(command.Target, caller, environment, out holderKey, out error)) return error;
            if (!ResolveOne(command.Secondary, caller, environment, out heldKey, out error)) return error;
            if (!SizePolicy.MayResize(SizeBook.Rules, caller.IsHost, caller.Key, holderKey))
                return "not allowed to act for " + environment.NameOf(holderKey) + " (SizePermission " + SizeBook.Rules.SizePermission + ")";
            float holderSize = SizeBook.Get(holderKey);
            float heldSize = SizeBook.Get(heldKey);
            CaptureMode mode = command.Verb == SizeVerb.Eat ? CaptureMode.Swallowed : CaptureMode.Held;
            bool allowed = mode == CaptureMode.Swallowed ? SizeMath.CanEat(SizeBook.Rules, holderSize, heldSize) : SizeMath.CanPickUp(SizeBook.Rules, holderSize, heldSize);
            if (!allowed) return Refusal(mode, holderSize, heldSize);
            string failure = CaptureBook.Start(holderKey, heldKey, mode);
            if (failure != null) return failure;
            CaptureBook.Broadcast(new List<int> { heldKey });
            return environment.NameOf(holderKey) + (mode == CaptureMode.Swallowed ? " swallowed " : " picked up ") + environment.NameOf(heldKey);
        }

        private static string Refusal(CaptureMode mode, float holderSize, float heldSize)
        {
            SizeRules rules = SizeBook.Rules;
            if (mode == CaptureMode.Swallowed && !rules.EatEnabled) return "eating is switched off (EatEnabled 0)";
            float needed = mode == CaptureMode.Swallowed ? rules.EatRatio : rules.PickupRatio;
            return "size ratio " + SizeRules.Format(holderSize / heldSize) + " is below the " + SizeRules.Format(needed) + " needed";
        }

        private static string Release(SizeCommand command, ICaller caller, ISizeEnvironment environment)
        {
            List<int> keys;
            string error;
            if (!environment.ResolveTargets(command.Target, caller, out keys, out error)) return error;
            int released = 0;
            foreach (int key in keys)
            {
                CaptureEntry entry;
                if (!CaptureBook.ByHeld.TryGetValue(key, out entry)) continue;
                if (!SizePolicy.MayResize(SizeBook.Rules, caller.IsHost, caller.Key, entry.HolderKey) && caller.Key != key) continue;
                CaptureBook.End(key);
                released++;
            }
            return released == 0 ? "nobody to release" : "released " + released;
        }

        private static bool ResolveOne(string spec, ICaller caller, ISizeEnvironment environment, out int key, out string error)
        {
            key = -1;
            List<int> keys;
            if (!environment.ResolveTargets(spec, caller, out keys, out error)) return false;
            if (keys.Count != 1)
            {
                error = "'" + spec + "' matches " + keys.Count + " bodies, need exactly one";
                return false;
            }
            key = keys[0];
            return true;
        }
    }
}
