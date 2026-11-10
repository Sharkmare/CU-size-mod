using System.Collections.Generic;
using CasualtiesSize.Core;

namespace CasualtiesSize.Tests
{
    public static class RuleTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("every rule default sits inside its own bounds", () =>
            {
                foreach (RuleDefinition definition in SizeRules.Definitions)
                    Check.True(definition.Default >= definition.Min && definition.Default <= definition.Max, definition.Name);
            });

            suite.Add("rule names are unique and described", () =>
            {
                HashSet<string> seen = new HashSet<string>();
                foreach (RuleDefinition definition in SizeRules.Definitions)
                {
                    Check.True(seen.Add(definition.Name), "duplicate " + definition.Name);
                    Check.True(definition.Description.Length > 10, "undescribed " + definition.Name);
                }
            });

            suite.Add("size is clamped to the rule range and bad numbers fall back to world size", () =>
            {
                SizeRules rules = new SizeRules();
                Check.Equal(rules.SizeMax, SizeMath.ClampSize(rules, 500f), "max");
                Check.Equal(rules.SizeMin, SizeMath.ClampSize(rules, 0.0001f), "min");
                Check.Equal(1f, SizeMath.ClampSize(rules, float.NaN), "nan");
                Check.Equal(1f, SizeMath.ClampSize(rules, float.PositiveInfinity), "inf");
            });

            suite.Add("setting a rule validates name, number and range", () =>
            {
                SizeRules rules = new SizeRules();
                string error;
                Check.False(rules.TrySet("Nope", "1", out error), "unknown name");
                Check.False(rules.TrySet("StompRatio", "abc", out error), "not a number");
                Check.False(rules.TrySet("StompRatio", "0.2", out error), "below min");
                Check.False(rules.TrySet("StompRatio", "NaN", out error), "nan");
                Check.True(rules.TrySet("stompratio", "3", out error), "case insensitive");
                Check.Equal(3f, rules.StompRatio, "stored");
                Check.True(rules.TrySet("EatEscapeStruggles", "7.6", out error), "whole rule");
                Check.Equal(8, rules.EatEscapeStruggles, "rounded");
            });

            suite.Add("permission policy covers anyone, self only and host only", () =>
            {
                SizeRules rules = new SizeRules();
                string error;
                Check.True(SizePolicy.MayResize(rules, false, 3, 5), "default lets anyone size anyone");
                rules.TrySet("SizePermission", "1", out error);
                Check.True(SizePolicy.MayResize(rules, false, 3, 3), "self allowed");
                Check.False(SizePolicy.MayResize(rules, false, 3, 5), "other refused");
                Check.True(SizePolicy.MayResize(rules, true, 0, 5), "host always");
                rules.TrySet("SizePermission", "2", out error);
                Check.False(SizePolicy.MayResize(rules, false, 3, 3), "host only refuses self");
                Check.True(SizePolicy.MayResize(rules, true, 0, 3), "host only allows host");
                Check.False(SizePolicy.MayChangeRules(false), "clients cannot change rules");
            });
        }
    }
}
