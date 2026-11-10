using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CasualtiesSize.Core
{
    public sealed class RuleDefinition
    {
        public readonly string Name;
        public readonly float Default;
        public readonly float Min;
        public readonly float Max;
        public readonly bool IsWhole;
        public readonly string Description;

        public RuleDefinition(string name, float defaultValue, float min, float max, bool isWhole, string description)
        {
            Name = name;
            Default = defaultValue;
            Min = min;
            Max = max;
            IsWhole = isWhole;
            Description = description;
        }
    }

    public sealed class SizeRules
    {
        public const int PermissionAnyone = 0;
        public const int PermissionSelfOnly = 1;
        public const int PermissionHostOnly = 2;

        public static readonly IReadOnlyList<RuleDefinition> Definitions = new List<RuleDefinition>
        {
            new RuleDefinition("SizeMin", 0.1f, 0.01f, 1f, false, "Smallest allowed size factor."),
            new RuleDefinition("SizeMax", 10f, 1f, 100f, false, "Largest allowed size factor."),
            new RuleDefinition("SizePermission", PermissionAnyone, 0, 2, true, "Who may size whom: 0 anyone, 1 self only, 2 host only."),
            new RuleDefinition("SizeEaseSeconds", 0.4f, 0f, 10f, false, "Seconds a size change takes to play out."),
            new RuleDefinition("DamageExponent", 0.75f, 0f, 3f, false, "Melee damage and victim fragility follow (attacker size / victim size) to this power."),
            new RuleDefinition("DamageMultMin", 0.25f, 0.01f, 1f, false, "Lower clamp of a damage multiplier."),
            new RuleDefinition("DamageMultMax", 4f, 1f, 100f, false, "Upper clamp of a damage multiplier."),
            new RuleDefinition("FragilityExponent", 0.5f, 0f, 3f, false, "Impact damage taken from the world follows size to the negative of this power."),
            new RuleDefinition("DigExponent", 1f, 0f, 3f, false, "Block and building damage per swing follows size to this power."),
            new RuleDefinition("DigMultMin", 0.25f, 0.01f, 1f, false, "Lower clamp of the dig multiplier."),
            new RuleDefinition("DigMultMax", 8f, 1f, 100f, false, "Upper clamp of the dig multiplier."),
            new RuleDefinition("ReachExponent", 0.5f, 0f, 3f, false, "Melee reach follows size to this power."),
            new RuleDefinition("CarryExponent", 2f, 0f, 4f, false, "Carry capacity follows size to this power."),
            new RuleDefinition("CameraZoomExponent", 0.5f, 0f, 2f, false, "Camera zoom of the local player follows size to this power."),
            new RuleDefinition("StompRatio", 1.6f, 1f, 100f, false, "Smallest actor/other size ratio at which a falling foot stomps."),
            new RuleDefinition("StompMinSpeed", 4f, 0f, 100f, false, "Smallest downward foot speed relative to the other body that stomps."),
            new RuleDefinition("StompForcePerRatio", 0.9f, 0f, 20f, false, "Impact force of a stomp per unit of size ratio and speed."),
            new RuleDefinition("StompSpeedCap", 20f, 1f, 200f, false, "Relative foot speed is capped here before it becomes force."),
            new RuleDefinition("StompCooldown", 0.6f, 0f, 60f, false, "Seconds before the same pair can stomp again."),
            new RuleDefinition("PickupRatio", 1.5f, 1f, 100f, false, "Smallest holder/held size ratio to pick a body up."),
            new RuleDefinition("EatEnabled", 1f, 0f, 1f, true, "1 allows eating a smaller body."),
            new RuleDefinition("EatRatio", 2f, 1f, 100f, false, "Smallest eater/eaten size ratio to swallow a body."),
            new RuleDefinition("EatDigestionDps", 1.5f, 0f, 100f, false, "Skin and muscle lost per second by a swallowed body, times the size ratio."),
            new RuleDefinition("EatHungerGain", 0.15f, 0f, 10f, false, "Hunger the eater gains per point of digestion damage."),
            new RuleDefinition("EatEscapeStruggles", 10f, 1f, 1000f, true, "Jump presses a held or swallowed body needs to break free."),
            new RuleDefinition("EatMaxSeconds", 90f, 1f, 36000f, false, "A swallowed body is released after this many seconds."),
        };

        private static readonly Dictionary<string, RuleDefinition> DefinitionByName = BuildIndex();

        private readonly Dictionary<string, float> values = new Dictionary<string, float>();

        public SizeRules()
        {
            ResetAll();
        }

        public float SizeMin { get { return this["SizeMin"]; } }
        public float SizeMax { get { return this["SizeMax"]; } }
        public int SizePermission { get { return (int)this["SizePermission"]; } }
        public float SizeEaseSeconds { get { return this["SizeEaseSeconds"]; } }
        public float DamageExponent { get { return this["DamageExponent"]; } }
        public float DamageMultMin { get { return this["DamageMultMin"]; } }
        public float DamageMultMax { get { return this["DamageMultMax"]; } }
        public float FragilityExponent { get { return this["FragilityExponent"]; } }
        public float DigExponent { get { return this["DigExponent"]; } }
        public float DigMultMin { get { return this["DigMultMin"]; } }
        public float DigMultMax { get { return this["DigMultMax"]; } }
        public float ReachExponent { get { return this["ReachExponent"]; } }
        public float CarryExponent { get { return this["CarryExponent"]; } }
        public float CameraZoomExponent { get { return this["CameraZoomExponent"]; } }
        public float StompRatio { get { return this["StompRatio"]; } }
        public float StompMinSpeed { get { return this["StompMinSpeed"]; } }
        public float StompForcePerRatio { get { return this["StompForcePerRatio"]; } }
        public float StompSpeedCap { get { return this["StompSpeedCap"]; } }
        public float StompCooldown { get { return this["StompCooldown"]; } }
        public float PickupRatio { get { return this["PickupRatio"]; } }
        public bool EatEnabled { get { return this["EatEnabled"] >= 0.5f; } }
        public float EatRatio { get { return this["EatRatio"]; } }
        public float EatDigestionDps { get { return this["EatDigestionDps"]; } }
        public float EatHungerGain { get { return this["EatHungerGain"]; } }
        public int EatEscapeStruggles { get { return (int)this["EatEscapeStruggles"]; } }
        public float EatMaxSeconds { get { return this["EatMaxSeconds"]; } }

        public float this[string name]
        {
            get { return values[name]; }
        }

        public static bool IsRuleName(string name)
        {
            return name != null && DefinitionByName.ContainsKey(name);
        }

        public static string CanonicalName(string typed)
        {
            if (typed == null) return null;
            foreach (RuleDefinition definition in Definitions)
            {
                if (string.Equals(definition.Name, typed, StringComparison.OrdinalIgnoreCase)) return definition.Name;
            }
            return null;
        }

        public void ResetAll()
        {
            values.Clear();
            foreach (RuleDefinition definition in Definitions) values[definition.Name] = definition.Default;
        }

        public bool TrySet(string typedName, string text, out string error)
        {
            error = null;
            string name = CanonicalName(typedName);
            if (name == null)
            {
                error = "unknown rule '" + typedName + "'";
                return false;
            }
            float parsed;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) || float.IsNaN(parsed) || float.IsInfinity(parsed))
            {
                error = "'" + text + "' is not a number";
                return false;
            }
            RuleDefinition definition = DefinitionByName[name];
            if (parsed < definition.Min || parsed > definition.Max)
            {
                error = name + " must be between " + Format(definition.Min) + " and " + Format(definition.Max);
                return false;
            }
            values[name] = definition.IsWhole ? (float)Math.Round(parsed) : parsed;
            return true;
        }

        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            foreach (RuleDefinition definition in Definitions)
            {
                text.Append(definition.Name).Append(' ').Append(Format(values[definition.Name]));
                if (Math.Abs(values[definition.Name] - definition.Default) > 1e-6f) text.Append(" (default ").Append(Format(definition.Default)).Append(')');
                text.Append('\n');
            }
            return text.ToString();
        }

        public static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        public Dictionary<string, float> Snapshot()
        {
            return new Dictionary<string, float>(values);
        }

        public void Load(IDictionary<string, float> incoming)
        {
            foreach (KeyValuePair<string, float> pair in incoming)
            {
                RuleDefinition definition;
                if (!DefinitionByName.TryGetValue(pair.Key, out definition)) continue;
                float clamped = Math.Min(definition.Max, Math.Max(definition.Min, pair.Value));
                values[pair.Key] = definition.IsWhole ? (float)Math.Round(clamped) : clamped;
            }
        }

        private static Dictionary<string, RuleDefinition> BuildIndex()
        {
            Dictionary<string, RuleDefinition> index = new Dictionary<string, RuleDefinition>();
            foreach (RuleDefinition definition in Definitions) index[definition.Name] = definition;
            return index;
        }
    }
}
