using System;
using System.Collections.Generic;
using CasualtiesSize.Core;

namespace CasualtiesSize
{
    public static class SizeBook
    {
        public const int SingleplayerKey = 0;

        public static readonly SizeRules Rules = new SizeRules();
        private static readonly Dictionary<int, float> Sizes = new Dictionary<int, float>();

        public static event Action<int, float> Changed;

        public static float Get(int key)
        {
            float size;
            return Sizes.TryGetValue(key, out size) ? size : SizeMath.WorldSize;
        }

        public static bool IsSized(int key)
        {
            return Sizes.ContainsKey(key);
        }

        public static IEnumerable<KeyValuePair<int, float>> All()
        {
            return Sizes;
        }

        public static Dictionary<int, float> Copy()
        {
            return new Dictionary<int, float>(Sizes);
        }

        public static float Set(int key, float requested)
        {
            float size = SizeMath.ClampSize(Rules, requested);
            if (Math.Abs(size - SizeMath.WorldSize) < 1e-4f)
            {
                Remove(key);
                return SizeMath.WorldSize;
            }
            Sizes[key] = size;
            RaiseChanged(key, size);
            return size;
        }

        public static void Remove(int key)
        {
            if (Sizes.Remove(key)) RaiseChanged(key, SizeMath.WorldSize);
        }

        public static void ReplaceAll(IDictionary<int, float> incoming)
        {
            List<int> gone = new List<int>(Sizes.Keys);
            Sizes.Clear();
            foreach (KeyValuePair<int, float> pair in incoming)
            {
                if (Math.Abs(pair.Value - SizeMath.WorldSize) < 1e-4f) continue;
                Sizes[pair.Key] = pair.Value;
                gone.Remove(pair.Key);
                RaiseChanged(pair.Key, pair.Value);
            }
            foreach (int key in gone) RaiseChanged(key, SizeMath.WorldSize);
        }

        private static void RaiseChanged(int key, float size)
        {
            Action<int, float> handler = Changed;
            if (handler != null) handler(key, size);
        }
    }
}
