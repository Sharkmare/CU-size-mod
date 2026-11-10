using System;
using System.Collections.Generic;

namespace CasualtiesSize
{
    public sealed class LocalEnvironment : ISizeEnvironment
    {
        public static readonly LocalEnvironment Instance = new LocalEnvironment();
        public static readonly ICaller LocalCaller = new FixedCaller { Key = SizeBook.SingleplayerKey, IsHost = true, Name = "you" };

        private static readonly HashSet<string> SelfSpecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "@m", "@a", "@r", "@c", "player", "me", "id:0", "body:0",
        };

        public bool ResolveTargets(string spec, ICaller caller, out List<int> keys, out string error)
        {
            keys = new List<int>();
            error = null;
            if (PlayerCamera.main == null || PlayerCamera.main.body == null)
            {
                error = "no world is loaded";
                return false;
            }
            if (spec != null && SelfSpecs.Contains(spec))
            {
                keys.Add(SizeBook.SingleplayerKey);
                return true;
            }
            error = "singleplayer has one body, use @m";
            return false;
        }

        public string NameOf(int key)
        {
            return "you";
        }

        public void AfterSizesChanged(List<int> keys)
        {
        }

        public void AfterRulesChanged()
        {
        }
    }
}
