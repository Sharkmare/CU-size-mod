using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CasualtiesSize.Core
{
    public static class SizeWire
    {
        public const ushort ProtocolVersion = 1;
        private const int MaxEntries = 4096;

        public static bool Compatible(ushort hostVersion, ushort clientVersion)
        {
            return hostVersion == clientVersion;
        }

        public static byte[] EncodeRules(IDictionary<string, float> rules)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write((ushort)rules.Count);
                foreach (KeyValuePair<string, float> pair in rules)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static bool TryDecodeRules(byte[] bytes, out Dictionary<string, float> rules)
        {
            rules = new Dictionary<string, float>();
            try
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    int count = reader.ReadUInt16();
                    if (count > MaxEntries) return false;
                    for (int i = 0; i < count; i++)
                    {
                        string name = reader.ReadString();
                        float value = reader.ReadSingle();
                        if (float.IsNaN(value) || float.IsInfinity(value)) return false;
                        rules[name] = value;
                    }
                    return stream.Position == stream.Length;
                }
            }
            catch (Exception)
            {
                rules = new Dictionary<string, float>();
                return false;
            }
        }

        public static byte[] EncodeSizes(IDictionary<int, float> sizes)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)sizes.Count);
                foreach (KeyValuePair<int, float> pair in sizes)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static bool TryDecodeSizes(byte[] bytes, SizeRules rules, out Dictionary<int, float> sizes)
        {
            sizes = new Dictionary<int, float>();
            try
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    int count = reader.ReadUInt16();
                    if (count > MaxEntries) return false;
                    for (int i = 0; i < count; i++)
                    {
                        int key = reader.ReadInt32();
                        float value = reader.ReadSingle();
                        sizes[key] = SizeMath.ClampSize(rules, value);
                    }
                    return stream.Position == stream.Length;
                }
            }
            catch (Exception)
            {
                sizes = new Dictionary<int, float>();
                return false;
            }
        }
    }
}
