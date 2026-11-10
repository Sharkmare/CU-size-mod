using System;
using System.Collections.Generic;
using CasualtiesSize.Core;

namespace CasualtiesSize.Tests
{
    public static class WireTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("rules survive a round trip and keep non default values", () =>
            {
                SizeRules host = new SizeRules();
                string error;
                host.TrySet("StompRatio", "2.5", out error);
                host.TrySet("SizePermission", "2", out error);
                byte[] bytes = SizeWire.EncodeRules(host.Snapshot());
                Dictionary<string, float> decoded;
                Check.True(SizeWire.TryDecodeRules(bytes, out decoded), "decode");
                SizeRules client = new SizeRules();
                client.Load(decoded);
                Check.Equal(2.5f, client.StompRatio, "stomp ratio");
                Check.Equal(2, client.SizePermission, "permission");
            });

            suite.Add("size table survives a round trip and is clamped on arrival", () =>
            {
                SizeRules rules = new SizeRules();
                Dictionary<int, float> sent = new Dictionary<int, float> { { 0, 2f }, { 1, 0.5f }, { 7, 9000f } };
                Dictionary<int, float> received;
                Check.True(SizeWire.TryDecodeSizes(SizeWire.EncodeSizes(sent), rules, out received), "decode");
                Check.Equal(2f, received[0], "host");
                Check.Equal(0.5f, received[1], "client");
                Check.Equal(rules.SizeMax, received[7], "absurd value clamped");
            });

            suite.Add("truncated, padded and hostile payloads are refused without throwing", () =>
            {
                SizeRules rules = new SizeRules();
                byte[] good = SizeWire.EncodeSizes(new Dictionary<int, float> { { 1, 2f } });
                Dictionary<int, float> sizes;
                Dictionary<string, float> ruleValues;
                Check.False(SizeWire.TryDecodeSizes(new byte[] { 0xFF, 0xFF }, rules, out sizes), "count past data");
                byte[] truncated = new byte[good.Length - 1];
                Array.Copy(good, truncated, truncated.Length);
                Check.False(SizeWire.TryDecodeSizes(truncated, rules, out sizes), "truncated");
                byte[] padded = new byte[good.Length + 3];
                good.CopyTo(padded, 0);
                Check.False(SizeWire.TryDecodeSizes(padded, rules, out sizes), "trailing bytes");
                Check.False(SizeWire.TryDecodeRules(new byte[] { 1, 0, 200 }, out ruleValues), "bad string length");
                Check.False(SizeWire.TryDecodeRules(new byte[0], out ruleValues), "empty");
            });

            suite.Add("rule values loaded from the wire are clamped to the bounds", () =>
            {
                SizeRules rules = new SizeRules();
                rules.Load(new Dictionary<string, float> { { "StompRatio", 0f }, { "NotARule", 5f }, { "EatEnabled", 7f } });
                Check.Equal(1f, rules.StompRatio, "min bound");
                Check.True(rules.EatEnabled, "whole rule clamped to 1");
            });

            suite.Add("only equal protocol versions are compatible", () =>
            {
                Check.True(SizeWire.Compatible(SizeWire.ProtocolVersion, SizeWire.ProtocolVersion), "same");
                Check.False(SizeWire.Compatible(SizeWire.ProtocolVersion, (ushort)(SizeWire.ProtocolVersion + 1)), "different");
            });
        }
    }
}
