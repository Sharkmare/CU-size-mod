using CasualtiesSize.Core;

namespace CasualtiesSize.Tests
{
    public static class RelationalTests
    {
        private static readonly float[] Sizes = { 0.25f, 0.5f, 1f, 2f, 4f };

        public static void Register(TestSuite suite)
        {
            suite.Add("attacker and victim terms multiply to the pair ratio for every pair", () =>
            {
                SizeRules rules = new SizeRules();
                int compared = 0;
                foreach (float attacker in Sizes)
                {
                    foreach (float victim in Sizes)
                    {
                        float pair = SizeMath.RelationalDamageMult(rules, attacker, victim);
                        float product = SizeMath.AttackerDamageMult(rules, attacker) * SizeMath.VictimFragilityMult(rules, victim);
                        bool clamped = pair <= rules.DamageMultMin + 1e-4f || pair >= rules.DamageMultMax - 1e-4f;
                        if (clamped) continue;
                        Check.Near(pair, product, 1e-3f, "pair " + attacker + " vs " + victim);
                        compared++;
                    }
                }
                Check.True(compared >= 15, "too few unclamped pairs were compared: " + compared);
            });

            suite.Add("equal sizes deal normal damage at any size", () =>
            {
                SizeRules rules = new SizeRules();
                foreach (float size in Sizes)
                    Check.Near(1f, SizeMath.RelationalDamageMult(rules, size, size), 1e-4f, "equal pair " + size);
            });

            suite.Add("larger attacker hits harder than the smaller one hits back", () =>
            {
                SizeRules rules = new SizeRules();
                foreach (float big in Sizes)
                    foreach (float small in Sizes)
                        if (big > small)
                            Check.True(SizeMath.RelationalDamageMult(rules, big, small) > SizeMath.RelationalDamageMult(rules, small, big), big + " over " + small);
            });

            suite.Add("damage multiplier respects both clamps", () =>
            {
                SizeRules rules = new SizeRules();
                Check.Near(rules.DamageMultMax, SizeMath.RelationalDamageMult(rules, 10f, 0.1f), 1e-4f, "upper clamp");
                Check.Near(rules.DamageMultMin, SizeMath.RelationalDamageMult(rules, 0.1f, 10f), 1e-4f, "lower clamp");
            });

            suite.Add("stomp needs the ratio, for every ordered pair", () =>
            {
                SizeRules rules = new SizeRules();
                foreach (float actor in Sizes)
                {
                    foreach (float other in Sizes)
                    {
                        bool expected = actor / other >= rules.StompRatio;
                        Check.Equal(expected, SizeMath.CanStomp(rules, actor, other), "stomp " + actor + " on " + other);
                    }
                }
            });

            suite.Add("stomp force is zero below the speed and ratio, and capped above", () =>
            {
                SizeRules rules = new SizeRules();
                Check.Equal(0f, SizeMath.StompForce(rules, 2f, 1f, rules.StompMinSpeed - 0.1f), "too slow");
                Check.Equal(0f, SizeMath.StompForce(rules, 1.2f, 1f, 10f), "ratio too low");
                float atCap = SizeMath.StompForce(rules, 2f, 1f, rules.StompSpeedCap);
                float beyondCap = SizeMath.StompForce(rules, 2f, 1f, rules.StompSpeedCap * 5f);
                Check.Near(atCap, beyondCap, 1e-4f, "speed cap");
                Check.True(SizeMath.StompForce(rules, 4f, 1f, 10f) > SizeMath.StompForce(rules, 2f, 1f, 10f), "bigger ratio, bigger force");
            });

            suite.Add("pickup and eat thresholds work for pairs that are not small versus big", () =>
            {
                SizeRules rules = new SizeRules();
                Check.True(SizeMath.CanPickUp(rules, 1f, 0.5f), "normal lifts small");
                Check.True(SizeMath.CanPickUp(rules, 4f, 2f), "a pair of ratio 2 at larger sizes");
                Check.False(SizeMath.CanPickUp(rules, 1f, 1f), "equals cannot lift");
                Check.True(SizeMath.CanEat(rules, 4f, 2f), "ratio exactly 2 eats");
                Check.False(SizeMath.CanEat(rules, 3f, 2f), "ratio 1.5 does not eat");
            });

            suite.Add("eat is refused when the host rule is off whatever the sizes", () =>
            {
                SizeRules rules = new SizeRules();
                string error;
                Check.True(rules.TrySet("EatEnabled", "0", out error), "set EatEnabled");
                Check.False(SizeMath.CanEat(rules, 10f, 0.1f), "eat off");
            });

            suite.Add("digestion damage grows with the ratio and with time", () =>
            {
                SizeRules rules = new SizeRules();
                float oneSecond = SizeMath.DigestionDamage(rules, 2f, 1f, 1f);
                Check.Near(rules.EatDigestionDps * 2f, oneSecond, 1e-4f, "ratio 2");
                Check.Near(oneSecond * 3f, SizeMath.DigestionDamage(rules, 2f, 1f, 3f), 1e-4f, "three seconds");
            });

            suite.Add("dig, reach, carry and mass move the right way", () =>
            {
                SizeRules rules = new SizeRules();
                Check.True(SizeMath.DigMult(rules, 2f) > 1f && SizeMath.DigMult(rules, 0.5f) < 1f, "dig");
                Check.True(SizeMath.ReachMult(rules, 2f) > 1f, "reach");
                Check.Near(4f, SizeMath.CarryMult(rules, 2f), 1e-4f, "carry follows mass at exponent 2");
                Check.Near(4f, SizeMath.MassMult(2f), 1e-4f, "mass is area");
                Check.True(SizeMath.WorldImpactMult(rules, 0.5f) < 1f, "small ones take less fall damage");
                Check.True(SizeMath.WorldImpactMult(rules, 2f) > 1f, "big ones take more fall damage");
                Check.True(SizeMath.GravityMult(rules, 0.5f) < 1f && SizeMath.GravityMult(rules, 2f) > 1f, "small ones fall more slowly");
                Check.Near(1f, SizeMath.GravityMult(rules, 1f), 1e-6f, "normal gravity at size 1");
            });

            suite.Add("easing reaches the target and never overshoots", () =>
            {
                float value = 1f;
                for (int step = 0; step < 2000; step++)
                {
                    value = SizeMath.Ease(value, 3f, 0.016f, 0.4f);
                    Check.True(value <= 3f, "overshoot");
                }
                Check.Equal(3f, value, "arrived");
                Check.Equal(0.5f, SizeMath.Ease(2f, 0.5f, 1f, 0f), "instant when ease is zero");
            });
        }
    }
}
