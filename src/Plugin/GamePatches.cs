using CasualtiesSize.Core;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesSize
{
    [HarmonyPatch(typeof(Body), nameof(Body.Attack))]
    public static class AttackSizePatch
    {
        private static void Prefix(Body __instance, AttackInfo atk)
        {
            float size = SizedBody.SizeOf(__instance);
            if (Mathf.Approximately(size, 1f)) return;
            SizeRules rules = SizeBook.Rules;
            atk.damage *= SizeMath.AttackerDamageMult(rules, size);
            atk.structuralDamage *= SizeMath.DigMult(rules, size);
            atk.distance *= SizeMath.ReachMult(rules, size);
        }
    }

    [HarmonyPatch(typeof(Limb), nameof(Limb.ImpactDamage))]
    public static class ImpactFragilityPatch
    {
        public static bool Bypass;

        private static void Prefix(Limb __instance, ref float force)
        {
            if (Bypass || __instance.body == null) return;
            float size = SizedBody.SizeOf(__instance.body);
            if (Mathf.Approximately(size, 1f)) return;
            force *= SizeMath.WorldImpactMult(SizeBook.Rules, size);
        }
    }

    [HarmonyPatch(typeof(Body), "Update")]
    public static class CarryCapacityPatch
    {
        private static void Postfix(Body __instance)
        {
            float size = SizedBody.SizeOf(__instance);
            if (Mathf.Approximately(size, 1f)) return;
            float capacity = __instance.maxEncumberance * SizeMath.CarryMult(SizeBook.Rules, size);
            __instance.overEncumberance = Mathf.Clamp01(__instance.totalEncumberance / capacity - 1f);
        }
    }
}
