using System;

namespace CasualtiesSize.Core
{
    public static class SizeMath
    {
        public const float WorldSize = 1f;
        public const float EaseSnapDistance = 0.001f;

        public static float ClampSize(SizeRules rules, float size)
        {
            if (float.IsNaN(size) || float.IsInfinity(size)) return WorldSize;
            return Math.Min(rules.SizeMax, Math.Max(rules.SizeMin, size));
        }

        public static float Ratio(float actor, float other)
        {
            return actor / other;
        }

        public static float AttackerDamageMult(SizeRules rules, float attackerSize)
        {
            return Clamp(Pow(attackerSize, rules.DamageExponent), rules.DamageMultMin, rules.DamageMultMax);
        }

        public static float VictimFragilityMult(SizeRules rules, float victimSize)
        {
            return Clamp(Pow(victimSize, -rules.DamageExponent), rules.DamageMultMin, rules.DamageMultMax);
        }

        public static float RelationalDamageMult(SizeRules rules, float attackerSize, float victimSize)
        {
            return Clamp(Pow(Ratio(attackerSize, victimSize), rules.DamageExponent), rules.DamageMultMin, rules.DamageMultMax);
        }

        public static float WorldImpactMult(SizeRules rules, float victimSize)
        {
            return Clamp(Pow(victimSize, -rules.FragilityExponent), rules.DamageMultMin, rules.DamageMultMax);
        }

        public static float DigMult(SizeRules rules, float size)
        {
            return Clamp(Pow(size, rules.DigExponent), rules.DigMultMin, rules.DigMultMax);
        }

        public static float ReachMult(SizeRules rules, float size)
        {
            return Pow(size, rules.ReachExponent);
        }

        public static float CarryMult(SizeRules rules, float size)
        {
            return Pow(size, rules.CarryExponent);
        }

        public static float CameraZoomMult(SizeRules rules, float size)
        {
            return Pow(size, rules.CameraZoomExponent);
        }

        public static float MassMult(float size)
        {
            return size * size;
        }

        public static bool CanStomp(SizeRules rules, float actorSize, float otherSize)
        {
            return Ratio(actorSize, otherSize) >= rules.StompRatio;
        }

        public static float StompForce(SizeRules rules, float actorSize, float otherSize, float downwardSpeedRelative)
        {
            if (!CanStomp(rules, actorSize, otherSize)) return 0f;
            if (downwardSpeedRelative < rules.StompMinSpeed) return 0f;
            float speed = Math.Min(downwardSpeedRelative, rules.StompSpeedCap);
            return speed * rules.StompForcePerRatio * Ratio(actorSize, otherSize);
        }

        public static bool CanPickUp(SizeRules rules, float holderSize, float heldSize)
        {
            return Ratio(holderSize, heldSize) >= rules.PickupRatio;
        }

        public static bool CanEat(SizeRules rules, float eaterSize, float eatenSize)
        {
            return rules.EatEnabled && Ratio(eaterSize, eatenSize) >= rules.EatRatio;
        }

        public static float DigestionDamage(SizeRules rules, float eaterSize, float eatenSize, float seconds)
        {
            return rules.EatDigestionDps * Ratio(eaterSize, eatenSize) * seconds;
        }

        public static float Ease(float current, float target, float deltaSeconds, float easeSeconds)
        {
            float remaining = Math.Abs(target - current);
            if (easeSeconds <= 0f || deltaSeconds >= easeSeconds || remaining <= EaseSnapDistance) return target;
            float step = remaining * (deltaSeconds / easeSeconds);
            return current + Math.Sign(target - current) * step;
        }

        private static float Pow(float value, float exponent)
        {
            return (float)Math.Pow(value, exponent);
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Min(max, Math.Max(min, value));
        }
    }
}
