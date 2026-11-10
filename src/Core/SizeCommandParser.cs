using System;
using System.Collections.Generic;
using System.Globalization;

namespace CasualtiesSize.Core
{
    public enum SizeVerb
    {
        Set,
        Reset,
        Show,
        List,
        RuleShow,
        RuleSet,
        Eat,
        Release,
    }

    public sealed class SizeCommand
    {
        public SizeVerb Verb;
        public string Target;
        public float Factor;
        public string RuleName;
        public string RuleValue;
        public string Secondary;
    }

    public static class SizeCommandParser
    {
        private static readonly HashSet<string> ResetWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "reset", "default", "normal", "off" };

        public static bool TryParseFactor(string text, out float factor, out string error)
        {
            factor = 1f;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "missing size factor";
                return false;
            }
            string trimmed = text.Trim();
            bool percent = trimmed.EndsWith("%", StringComparison.Ordinal);
            if (percent) trimmed = trimmed.Substring(0, trimmed.Length - 1);
            if (trimmed.StartsWith("x", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(1);
            float parsed;
            if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) || float.IsNaN(parsed) || float.IsInfinity(parsed))
            {
                error = "'" + text + "' is not a size factor";
                return false;
            }
            if (percent) parsed /= 100f;
            if (parsed <= 0f)
            {
                error = "size must be above zero";
                return false;
            }
            factor = parsed;
            return true;
        }

        public static bool TryParse(string[] arguments, out SizeCommand command, out string error)
        {
            command = null;
            error = null;
            if (arguments == null || arguments.Length == 0)
            {
                error = "empty command";
                return false;
            }
            string name = arguments[0].ToLowerInvariant();
            switch (name)
            {
                case "sizes":
                    command = new SizeCommand { Verb = SizeVerb.List };
                    return true;
                case "size":
                    return ParseSize(arguments, out command, out error);
                case "sizerule":
                    return ParseRule(arguments, out command, out error);
                case "eat":
                    if (arguments.Length < 3)
                    {
                        error = "usage: eat <eater> <target>";
                        return false;
                    }
                    command = new SizeCommand { Verb = SizeVerb.Eat, Target = arguments[1], Secondary = arguments[2] };
                    return true;
                case "release":
                    if (arguments.Length < 2)
                    {
                        error = "usage: release <target>";
                        return false;
                    }
                    command = new SizeCommand { Verb = SizeVerb.Release, Target = arguments[1] };
                    return true;
                default:
                    error = "unknown command '" + arguments[0] + "'";
                    return false;
            }
        }

        private static bool ParseSize(string[] arguments, out SizeCommand command, out string error)
        {
            command = null;
            error = null;
            if (arguments.Length < 2)
            {
                error = "usage: size <target> <factor|reset>";
                return false;
            }
            string target = arguments[1];
            if (arguments.Length == 2)
            {
                command = new SizeCommand { Verb = SizeVerb.Show, Target = target };
                return true;
            }
            if (ResetWords.Contains(arguments[2]))
            {
                command = new SizeCommand { Verb = SizeVerb.Reset, Target = target, Factor = 1f };
                return true;
            }
            float factor;
            if (!TryParseFactor(arguments[2], out factor, out error)) return false;
            command = new SizeCommand { Verb = SizeVerb.Set, Target = target, Factor = factor };
            return true;
        }

        private static bool ParseRule(string[] arguments, out SizeCommand command, out string error)
        {
            command = null;
            error = null;
            if (arguments.Length == 1)
            {
                command = new SizeCommand { Verb = SizeVerb.RuleShow };
                return true;
            }
            string canonical = SizeRules.CanonicalName(arguments[1]);
            if (canonical == null)
            {
                error = "unknown rule '" + arguments[1] + "'";
                return false;
            }
            if (arguments.Length == 2)
            {
                command = new SizeCommand { Verb = SizeVerb.RuleShow, RuleName = canonical };
                return true;
            }
            command = new SizeCommand { Verb = SizeVerb.RuleSet, RuleName = canonical, RuleValue = arguments[2] };
            return true;
        }
    }
}
