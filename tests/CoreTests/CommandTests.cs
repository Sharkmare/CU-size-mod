using System;
using CasualtiesSize.Core;

namespace CasualtiesSize.Tests
{
    public static class CommandTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("factor grammar", () =>
            {
                float factor;
                string error;
                Check.True(SizeCommandParser.TryParseFactor("2", out factor, out error) && factor == 2f, "plain");
                Check.True(SizeCommandParser.TryParseFactor("x0.5", out factor, out error) && factor == 0.5f, "x prefix");
                Check.True(SizeCommandParser.TryParseFactor("150%", out factor, out error) && Math.Abs(factor - 1.5f) < 1e-5f, "percent");
                Check.False(SizeCommandParser.TryParseFactor("0,5", out factor, out error), "comma decimal refused");
                Check.False(SizeCommandParser.TryParseFactor("0", out factor, out error), "zero");
                Check.False(SizeCommandParser.TryParseFactor("-2", out factor, out error), "negative");
                Check.False(SizeCommandParser.TryParseFactor("inf", out factor, out error), "inf");
                Check.False(SizeCommandParser.TryParseFactor("", out factor, out error), "empty");
            });

            suite.Add("size command forms", () =>
            {
                SizeCommand command;
                string error;
                Check.True(SizeCommandParser.TryParse(new[] { "size", "@m", "2" }, out command, out error), "set");
                Check.Equal(SizeVerb.Set, command.Verb, "verb");
                Check.Equal("@m", command.Target, "target");
                Check.Equal(2f, command.Factor, "factor");
                Check.True(SizeCommandParser.TryParse(new[] { "size", "id:3", "reset" }, out command, out error), "reset");
                Check.Equal(SizeVerb.Reset, command.Verb, "reset verb");
                Check.True(SizeCommandParser.TryParse(new[] { "size", "Bob" }, out command, out error), "show");
                Check.Equal(SizeVerb.Show, command.Verb, "show verb");
                Check.False(SizeCommandParser.TryParse(new[] { "size" }, out command, out error), "no target");
                Check.False(SizeCommandParser.TryParse(new[] { "size", "@a", "huge" }, out command, out error), "bad factor");
                Check.True(SizeCommandParser.TryParse(new[] { "sizes" }, out command, out error) && command.Verb == SizeVerb.List, "list");
            });

            suite.Add("rule, eat and release command forms", () =>
            {
                SizeCommand command;
                string error;
                Check.True(SizeCommandParser.TryParse(new[] { "sizerule" }, out command, out error) && command.Verb == SizeVerb.RuleShow, "show all");
                Check.True(SizeCommandParser.TryParse(new[] { "sizerule", "stompratio" }, out command, out error) && command.RuleName == "StompRatio", "show one canonical");
                Check.True(SizeCommandParser.TryParse(new[] { "sizerule", "StompRatio", "2" }, out command, out error) && command.Verb == SizeVerb.RuleSet && command.RuleValue == "2", "set");
                Check.False(SizeCommandParser.TryParse(new[] { "sizerule", "Bogus", "2" }, out command, out error), "unknown rule");
                Check.True(SizeCommandParser.TryParse(new[] { "eat", "@m", "@c" }, out command, out error) && command.Target == "@m" && command.Secondary == "@c", "eat");
                Check.False(SizeCommandParser.TryParse(new[] { "eat", "@m" }, out command, out error), "eat needs two");
                Check.True(SizeCommandParser.TryParse(new[] { "release", "@a" }, out command, out error), "release");
            });
        }
    }
}
