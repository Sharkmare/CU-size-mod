using System.Collections.Generic;
using System.Text;
using CasualtiesSize.Core;
using UnityEngine;

namespace CasualtiesSize
{
    public interface ICaller
    {
        int Key { get; }
        bool IsHost { get; }
        string Name { get; }
    }

    public interface ISizeEnvironment
    {
        bool ResolveTargets(string spec, ICaller caller, out List<int> keys, out string error);
        string NameOf(int key);
        void AfterSizesChanged(List<int> keys);
        void AfterRulesChanged();
    }

    public sealed class FixedCaller : ICaller
    {
        public int Key { get; set; }
        public bool IsHost { get; set; }
        public string Name { get; set; }
    }

    public static class SizeService
    {
        public static string Execute(SizeCommand command, ICaller caller, ISizeEnvironment environment)
        {
            switch (command.Verb)
            {
                case SizeVerb.Set:
                case SizeVerb.Reset:
                    return ApplySize(command, caller, environment);
                case SizeVerb.Show:
                    return ShowOne(command, caller, environment);
                case SizeVerb.List:
                    return ListAll(environment);
                case SizeVerb.RuleShow:
                    return ShowRules(command);
                case SizeVerb.RuleSet:
                    return SetRule(command, caller, environment);
                case SizeVerb.Eat:
                case SizeVerb.Pickup:
                case SizeVerb.Release:
                    return CaptureCommands.Execute(command, caller, environment);
                default:
                    return "not handled here: " + command.Verb;
            }
        }

        private static string ApplySize(SizeCommand command, ICaller caller, ISizeEnvironment environment)
        {
            List<int> keys;
            string error;
            if (!environment.ResolveTargets(command.Target, caller, out keys, out error)) return error;
            StringBuilder reply = new StringBuilder();
            List<int> changed = new List<int>();
            foreach (int key in keys)
            {
                if (!SizePolicy.MayResize(SizeBook.Rules, caller.IsHost, caller.Key, key))
                {
                    reply.Append("not allowed to size ").Append(environment.NameOf(key)).Append(" (SizePermission ").Append(SizeBook.Rules.SizePermission).Append(")\n");
                    continue;
                }
                float applied = command.Verb == SizeVerb.Reset ? SizeMath.WorldSize : SizeBook.Set(key, command.Factor);
                if (command.Verb == SizeVerb.Reset) SizeBook.Remove(key);
                changed.Add(key);
                reply.Append(environment.NameOf(key)).Append(" is now size ").Append(SizeRules.Format(applied));
                if (command.Verb == SizeVerb.Set && applied != command.Factor) reply.Append(" (clamped from ").Append(SizeRules.Format(command.Factor)).Append(')');
                reply.Append('\n');
            }
            if (changed.Count > 0)
            {
                CheatFlag.Raise();
                environment.AfterSizesChanged(changed);
            }
            return reply.ToString().TrimEnd('\n');
        }

        private static string ShowOne(SizeCommand command, ICaller caller, ISizeEnvironment environment)
        {
            List<int> keys;
            string error;
            if (!environment.ResolveTargets(command.Target, caller, out keys, out error)) return error;
            StringBuilder reply = new StringBuilder();
            foreach (int key in keys)
                reply.Append(environment.NameOf(key)).Append(" size ").Append(SizeRules.Format(SizeBook.Get(key))).Append('\n');
            return reply.ToString().TrimEnd('\n');
        }

        private static string ListAll(ISizeEnvironment environment)
        {
            StringBuilder reply = new StringBuilder();
            int listed = 0;
            foreach (KeyValuePair<int, float> pair in SizeBook.All())
            {
                reply.Append(environment.NameOf(pair.Key)).Append(" (").Append(pair.Key).Append(") ").Append(SizeRules.Format(pair.Value)).Append('\n');
                listed++;
            }
            if (listed == 0) reply.Append("every body is size 1\n");
            return reply.ToString().TrimEnd('\n');
        }

        private static string ShowRules(SizeCommand command)
        {
            if (command.RuleName == null) return SizeBook.Rules.Describe().TrimEnd('\n');
            return command.RuleName + " " + SizeRules.Format(SizeBook.Rules[command.RuleName]);
        }

        private static string SetRule(SizeCommand command, ICaller caller, ISizeEnvironment environment)
        {
            if (!SizePolicy.MayChangeRules(caller.IsHost)) return "only the host can change size rules";
            string error;
            if (!SizeBook.Rules.TrySet(command.RuleName, command.RuleValue, out error)) return error;
            environment.AfterRulesChanged();
            return command.RuleName + " is now " + SizeRules.Format(SizeBook.Rules[command.RuleName]);
        }
    }

    public static class CheatFlag
    {
        public static void Raise()
        {
            if (SteamManager.disableAchievements) return;
            SteamManager.disableAchievements = true;
            Debug.Log("CasualtiesSize: size changed, achievements are off until restart");
        }
    }
}
