using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using CasualtiesSize.Core;
using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using Together;
using UnityEngine;

namespace CasualtiesSize
{
    internal static class TogetherBridge
    {
        private const int MaxTextLength = 64;
        private const int MaxReplyLength = 400;

        public static readonly IBodyDirectory Directory = new TogetherDirectory();

        private static readonly TogetherEnvironment Environment = new TogetherEnvironment();
        private static readonly SizeGate Gate = new SizeGate(new NetTransport(), WelcomePayload, HelloPayload());

        private static bool wasRunning;
        private static bool fallbackLogged;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Start()
        {
            Register("Hello", true, HostReceiveHello);
            Register("Request", true, HostReceiveRequest);
            Register("Struggle", true, HostReceiveStruggle);
            Register("Welcome", false, ClientReceiveWelcome);
            Register("Sizes", false, ClientReceiveSizes);
            Register("Rules", false, ClientReceiveRules);
            Register("Reply", false, ClientReceiveReply);
            Register("Captures", false, ClientReceiveCaptures);
            CaptureBook.Broadcast = keys => BroadcastCaptures();
            CaptureBook.HealthChanged = QueueHealthSync;
            ScavPlayer.OnPlayerLeft += HostPlayerLeft;
            Harmony harmony = new Harmony(Plugin.Guid + ".together");
            PvpFragilityPatch.Apply(harmony);
            PiggybackWeightPatch.Apply(harmony);
            Plugin.Instance.gameObject.AddComponent<BridgeTicker>();
        }

        private static void Register(string name, bool toHost, Action<ScavPlayer, byte[]> handler)
        {
            string full = SizeGate.Prefix + name;
            if (toHost) Multiplayer.RegisterCustomServerReceiver(full, (sender, reader) => handler(sender, reader.GetBytesWithLength()));
            else Multiplayer.RegisterCustomClientReceiver(full, reader => handler(null, reader.GetBytesWithLength()));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RegisterCommands(List<Command> commands)
        {
            foreach (Command command in commands) Con.RegisterCommand_LocalOnly(command);
            Con.UpdatePlayerListAutocomplete();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool NetworkRunning()
        {
            return Net.IsRunning;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Dispatch(SizeCommand command)
        {
            if (Net.IsServer)
            {
                ICaller caller = new PlayerCaller(ScavPlayer.LocalPlayer, true);
                return SizeService.Execute(command, caller, Environment);
            }
            if (Gate.Host != PeerState.Confirmed) return DispatchWithoutHost(command);
            Gate.SendToHost("Request", Pack(writer =>
            {
                writer.Write((byte)command.Verb);
                writer.Write(Truncate(command.Target));
                writer.Write(command.Factor);
                writer.Write(Truncate(command.RuleName));
                writer.Write(Truncate(command.RuleValue));
                writer.Write(Truncate(command.Secondary));
            }));
            return null;
        }

        private static string DispatchWithoutHost(SizeCommand command)
        {
            bool hostAnswered = Gate.Host == PeerState.NotOurs || Gate.Host == PeerState.Incompatible;
            if (!hostAnswered || ScavPlayer.LocalPlayer == null) return Gate.RefusalText();
            switch (command.Verb)
            {
                case SizeVerb.Set:
                case SizeVerb.Reset:
                case SizeVerb.Show:
                case SizeVerb.List:
                    ICaller caller = new PlayerCaller(ScavPlayer.LocalPlayer, false);
                    return SizeService.Execute(command, caller, SelfOnlyEnvironment.Instance);
                default:
                    return Gate.RefusalText();
            }
        }

        private static byte[] HelloPayload()
        {
            return Pack(writer => writer.Write(SizeWire.ProtocolVersion));
        }

        private static byte[] WelcomePayload()
        {
            return Pack(writer =>
            {
                writer.Write(SizeWire.ProtocolVersion);
                byte[] rules = SizeWire.EncodeRules(SizeBook.Rules.Snapshot());
                writer.Write(rules.Length);
                writer.Write(rules);
                byte[] sizes = SizeWire.EncodeSizes(SizeBook.Copy());
                writer.Write(sizes.Length);
                writer.Write(sizes);
            });
        }

        private static byte[] Pack(Action<BinaryWriter> fill)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                fill(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static string Truncate(string text)
        {
            if (text == null) return string.Empty;
            return text.Length <= MaxTextLength ? text : text.Substring(0, MaxTextLength);
        }

        private static string Blank(string text)
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static void HostReceiveHello(ScavPlayer sender, byte[] payload)
        {
            if (!Net.IsServer) return;
            ushort clientVersion;
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(payload))) clientVersion = reader.ReadUInt16();
            }
            catch (Exception)
            {
                return;
            }
            bool compatible = SizeWire.Compatible(SizeWire.ProtocolVersion, clientVersion);
            Gate.OnHello((ushort)sender.playerId, compatible);
            Plugin.Debug("hello from " + sender.playerName + ", protocol " + clientVersion + (compatible ? " welcomed" : " refused"));
            if (compatible) BroadcastCaptures();
        }

        private static void HostReceiveRequest(ScavPlayer sender, byte[] payload)
        {
            if (!Net.IsServer || Gate.StateOf((ushort)sender.playerId) != PeerState.Confirmed) return;
            SizeCommand command;
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(payload)))
                {
                    byte verbByte = reader.ReadByte();
                    if (!Enum.IsDefined(typeof(SizeVerb), (int)verbByte)) return;
                    command = new SizeCommand
                    {
                        Verb = (SizeVerb)verbByte,
                        Target = reader.ReadString(),
                        Factor = reader.ReadSingle(),
                        RuleName = Blank(reader.ReadString()),
                        RuleValue = Blank(reader.ReadString()),
                        Secondary = Blank(reader.ReadString()),
                    };
                }
            }
            catch (Exception)
            {
                return;
            }
            if (float.IsNaN(command.Factor) || float.IsInfinity(command.Factor)) return;
            string reply = SizeService.Execute(command, new PlayerCaller(sender, false), Environment);
            SendReply(sender, reply);
        }

        private static void SendReply(ScavPlayer target, string text)
        {
            if (string.IsNullOrEmpty(text) || target == null || target.IsLocal) return;
            if (text.Length > MaxReplyLength) text = text.Substring(0, MaxReplyLength);
            Gate.SendToPlayer((ushort)target.playerId, "Reply", Pack(writer => writer.Write(text)));
        }

        private static void ClientReceiveWelcome(ScavPlayer unused, byte[] payload)
        {
            if (Net.IsServer || Gate.Host != PeerState.Unknown) return;
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(payload)))
                {
                    ushort hostVersion = reader.ReadUInt16();
                    bool compatible = SizeWire.Compatible(hostVersion, SizeWire.ProtocolVersion);
                    if (!compatible)
                    {
                        Gate.OnWelcome(false);
                        Plugin.Log.LogInfo("This host runs CasualtiesSize protocol " + hostVersion + ", yours is " + SizeWire.ProtocolVersion + ", so sizes are off for you.");
                        return;
                    }
                    byte[] rulesBytes = reader.ReadBytes(reader.ReadInt32());
                    byte[] sizesBytes = reader.ReadBytes(reader.ReadInt32());
                    Dictionary<string, float> rules;
                    Dictionary<int, float> sizes;
                    if (!SizeWire.TryDecodeRules(rulesBytes, out rules)) return;
                    SizeBook.Rules.Load(rules);
                    if (!SizeWire.TryDecodeSizes(sizesBytes, SizeBook.Rules, out sizes)) return;
                    SizeBook.ReplaceAll(sizes);
                    Gate.OnWelcome(true);
                    Plugin.Debug("welcome received, " + sizes.Count + " sizes");
                }
            }
            catch (Exception)
            {
            }
        }

        private static void ClientReceiveSizes(ScavPlayer unused, byte[] payload)
        {
            if (Net.IsServer || Gate.Host != PeerState.Confirmed) return;
            Dictionary<int, float> sizes;
            if (SizeWire.TryDecodeSizes(payload, SizeBook.Rules, out sizes)) SizeBook.ReplaceAll(sizes);
        }

        private static void ClientReceiveRules(ScavPlayer unused, byte[] payload)
        {
            if (Net.IsServer || Gate.Host != PeerState.Confirmed) return;
            Dictionary<string, float> rules;
            if (SizeWire.TryDecodeRules(payload, out rules)) SizeBook.Rules.Load(rules);
        }

        private static void ClientReceiveReply(ScavPlayer unused, byte[] payload)
        {
            if (Net.IsServer || Gate.Host != PeerState.Confirmed) return;
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(payload))) ConsoleOutput.Write(reader.ReadString());
            }
            catch (Exception)
            {
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsServer()
        {
            return Net.IsServer;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int LocalKey()
        {
            return ScavPlayer.LocalPlayer == null ? -1 : ScavPlayer.LocalPlayer.playerId;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SendStruggle()
        {
            Gate.SendToHost("Struggle", new byte[0]);
        }

        private static void HostReceiveStruggle(ScavPlayer sender, byte[] payload)
        {
            if (!Net.IsServer || Gate.StateOf((ushort)sender.playerId) != PeerState.Confirmed) return;
            CaptureEntry entry;
            if (CaptureBook.ByHeld.TryGetValue(sender.playerId, out entry)) entry.Tracker.Struggle();
        }

        public static void BroadcastCaptures()
        {
            if (!Net.IsServer) return;
            List<KeyValuePair<int, KeyValuePair<int, byte>>> table = CaptureBook.ToWire();
            Gate.Broadcast("Captures", Pack(writer =>
            {
                writer.Write((ushort)table.Count);
                foreach (KeyValuePair<int, KeyValuePair<int, byte>> row in table)
                {
                    writer.Write(row.Key);
                    writer.Write(row.Value.Key);
                    writer.Write(row.Value.Value);
                }
            }));
        }

        private static void ClientReceiveCaptures(ScavPlayer unused, byte[] payload)
        {
            if (Net.IsServer || Gate.Host != PeerState.Confirmed) return;
            List<KeyValuePair<int, KeyValuePair<int, byte>>> table = new List<KeyValuePair<int, KeyValuePair<int, byte>>>();
            try
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(payload)))
                {
                    int count = reader.ReadUInt16();
                    if (count > 256) return;
                    for (int i = 0; i < count; i++)
                    {
                        int held = reader.ReadInt32();
                        int holder = reader.ReadInt32();
                        byte mode = reader.ReadByte();
                        if (mode > 1) return;
                        table.Add(new KeyValuePair<int, KeyValuePair<int, byte>>(held, new KeyValuePair<int, byte>(holder, mode)));
                    }
                }
            }
            catch (Exception)
            {
                return;
            }
            CaptureBook.ReplaceFromWire(table);
        }

        private static readonly System.Reflection.MethodInfo HealthQueue = AccessTools.Method("Together.MedicalSync:Server_QueueSendCharacterHealth");

        private static void QueueHealthSync(Body body)
        {
            if (HealthQueue == null || body == null) return;
            NetBody netBody = body.GetComponent<NetBody>();
            if (netBody != null) HealthQueue.Invoke(null, new object[] { netBody, true });
        }

        public static void BroadcastSizes()
        {
            if (!Net.IsServer) return;
            Gate.Broadcast("Sizes", SizeWire.EncodeSizes(SizeBook.Copy()));
        }

        public static void BroadcastRules()
        {
            if (!Net.IsServer) return;
            Gate.Broadcast("Rules", SizeWire.EncodeRules(SizeBook.Rules.Snapshot()));
        }

        private static void HostPlayerLeft(ScavPlayer player)
        {
            if (!Net.IsServer) return;
            Gate.OnPlayerLeft((ushort)player.playerId);
            CaptureBook.End(player.playerId);
            foreach (CaptureEntry held in new List<CaptureEntry>(CaptureBook.ByHeld.Values))
                if (held.HolderKey == player.playerId) CaptureBook.End(held.HeldKey);
            if (SizeBook.IsSized(player.playerId))
            {
                SizeBook.Remove(player.playerId);
                BroadcastSizes();
            }
        }

        public static void Tick()
        {
            bool running = Net.IsRunning;
            if (running != wasRunning)
            {
                wasRunning = running;
                SizeBook.ReplaceAll(new Dictionary<int, float>());
                SizeBook.Rules.ResetAll();
                CaptureBook.ClearAll();
                Gate.Reset();
                fallbackLogged = false;
            }
            if (!running || Net.IsServer || !Net.IsConnected || ScavPlayer.LocalPlayer == null) return;
            Gate.ClientTick(Time.unscaledTime);
            if (Gate.Host == PeerState.NotOurs && !fallbackLogged)
            {
                fallbackLogged = true;
                Plugin.Log.LogInfo("The host did not answer, so sizes are off in this game unless the host has CasualtiesSize.");
            }
        }
    }

    internal sealed class SelfOnlyEnvironment : ISizeEnvironment
    {
        public static readonly SelfOnlyEnvironment Instance = new SelfOnlyEnvironment();

        public bool ResolveTargets(string spec, ICaller caller, out List<int> keys, out string error)
        {
            keys = new List<int>();
            error = null;
            ScavPlayer me = ScavPlayer.LocalPlayer;
            string lowered = spec == null ? string.Empty : spec.ToLowerInvariant();
            bool isMe = me != null && (lowered == "@m" || lowered == "me" || lowered == "id:" + me.playerId
                || string.Equals(spec, me.playerName, StringComparison.OrdinalIgnoreCase));
            if (!isMe)
            {
                error = "sizing other players needs the host to run CasualtiesSize. This host does not, so only your own size works.";
                return false;
            }
            keys.Add(me.playerId);
            return true;
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

    internal sealed class NetTransport : ISizeTransport
    {
        public bool IsNetworkRunning { get { return Net.IsRunning; } }
        public bool IsHost { get { return Net.IsServer; } }

        public void SendToHost(string name, byte[] payload)
        {
            NetDataWriter writer = Multiplayer.CreateNamedWriter(name);
            writer.PutBytesWithLength(payload);
            Net.Client_Send(DeliveryMethod.ReliableOrdered, writer);
        }

        public void SendToPlayer(ushort playerId, string name, byte[] payload)
        {
            NetDataWriter writer = Multiplayer.CreateNamedWriter(name);
            writer.PutBytesWithLength(payload);
            Net.Server_SendTo(DeliveryMethod.ReliableOrdered, writer, new knetid(playerId));
        }
    }

    internal sealed class BridgeTicker : MonoBehaviour
    {
        private void Update()
        {
            TogetherBridge.Tick();
        }
    }

    internal sealed class PlayerCaller : ICaller
    {
        private readonly ScavPlayer player;
        private readonly bool isHost;

        public PlayerCaller(ScavPlayer player, bool isHost)
        {
            this.player = player;
            this.isHost = isHost;
        }

        public ScavPlayer Player { get { return player; } }
        public int Key { get { return player == null ? -1 : player.playerId; } }
        public bool IsHost { get { return isHost; } }
        public string Name { get { return player == null ? "host" : player.playerName; } }
    }

    internal sealed class TogetherEnvironment : ISizeEnvironment
    {
        public bool ResolveTargets(string spec, ICaller caller, out List<int> keys, out string error)
        {
            keys = new List<int>();
            error = null;
            ScavPlayer callerPlayer = ((PlayerCaller)caller).Player;
            List<ScavPlayer> found;
            try
            {
                found = Con.GetPlayersForCommand(spec, callerPlayer, false, true);
            }
            catch (Exception)
            {
                error = "nobody matches '" + spec + "'";
                return false;
            }
            if (found == null || found.Count == 0)
            {
                error = "no player matches '" + spec + "'";
                return false;
            }
            foreach (ScavPlayer player in found)
                if (player != null) keys.Add(player.playerId);
            if (keys.Count == 0)
            {
                error = "no player matches '" + spec + "'";
                return false;
            }
            return true;
        }

        public string NameOf(int key)
        {
            ScavPlayer player;
            return ScavPlayer.ClientIdToPlayerDict.TryGetValue(new knetid((ushort)key), out player) ? player.playerName : "player " + key;
        }

        public void AfterSizesChanged(List<int> keys)
        {
            TogetherBridge.BroadcastSizes();
        }

        public void AfterRulesChanged()
        {
            TogetherBridge.BroadcastRules();
        }
    }

    internal sealed class TogetherDirectory : IBodyDirectory
    {
        public IEnumerable<BodyHandle> Bodies()
        {
            foreach (ScavPlayer player in ScavPlayer.ClientIdToPlayerDict.Values)
            {
                if (player == null || player.body == null) continue;
                yield return new BodyHandle { Key = player.playerId, Body = player.body, IsLocal = player.IsLocal };
            }
        }
    }

    internal static class PvpFragilityPatch
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method("Together.Body_Attack_MultiplayerPatch:DoPVPMeleeDamage");
            if (target == null)
            {
                Plugin.Log.LogWarning("Body_Attack_MultiplayerPatch.DoPVPMeleeDamage not found, victim size will not change melee damage in multiplayer");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(PvpFragilityPatch), nameof(Prefix)));
        }

        private static void Prefix(Limb target, ref float armstrength)
        {
            float victimSize = SizedBody.SizeOf(target.body);
            if (Mathf.Approximately(victimSize, 1f)) return;
            armstrength *= SizeMath.VictimFragilityMult(SizeBook.Rules, victimSize);
        }
    }
}

namespace CasualtiesSize
{
    internal static class PiggybackWeightPatch
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(NetBody), nameof(NetBody.GetRealFullEncumberanceOfBody));
            if (target == null)
            {
                Plugin.Log.LogWarning("NetBody.GetRealFullEncumberanceOfBody not found, a carried player keeps full weight");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(PiggybackWeightPatch), nameof(Postfix)));
        }

        private static void Postfix(NetBody __instance, ref float __result)
        {
            float carriedSize = SizedBody.SizeOf(__instance.body);
            if (Mathf.Approximately(carriedSize, 1f)) return;
            __result *= SizeMath.CarryMult(SizeBook.Rules, carriedSize);
        }
    }
}
