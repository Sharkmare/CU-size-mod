using System;
using System.Collections.Generic;
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
        public const string MessagePrefix = "CasualtiesSize.";
        public const float HelloTimeoutSeconds = 5f;
        private const float HelloRetrySeconds = 2f;
        private const int MaxTextLength = 64;

        public static readonly IBodyDirectory Directory = new TogetherDirectory();

        private static readonly HashSet<ushort> HelloedClients = new HashSet<ushort>();
        private static readonly TogetherEnvironment Environment = new TogetherEnvironment();

        public static bool Welcomed;
        public static float FirstHelloTime = -1f;
        public static string RefusalText;

        private static float lastHelloTime = -100f;
        private static bool wasRunning;
        private static bool fallbackLogged;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Start()
        {
            Multiplayer.RegisterCustomServerReceiver(MessagePrefix + "Hello", HostReceiveHello);
            Multiplayer.RegisterCustomServerReceiver(MessagePrefix + "Request", HostReceiveRequest);
            Multiplayer.RegisterCustomClientReceiver(MessagePrefix + "Welcome", ClientReceiveWelcome);
            Multiplayer.RegisterCustomClientReceiver(MessagePrefix + "Sizes", ClientReceiveSizes);
            Multiplayer.RegisterCustomClientReceiver(MessagePrefix + "Rules", ClientReceiveRules);
            Multiplayer.RegisterCustomClientReceiver(MessagePrefix + "Reply", ClientReceiveReply);
            Multiplayer.RegisterCustomServerReceiver(MessagePrefix + "Struggle", HostReceiveStruggle);
            Multiplayer.RegisterCustomClientReceiver(MessagePrefix + "Captures", ClientReceiveCaptures);
            CaptureBook.Broadcast = keys => BroadcastCaptures();
            CaptureBook.HealthChanged = QueueHealthSync;
            ScavPlayer.OnPlayerLeft += HostPlayerLeft;
            Harmony harmony = new Harmony(Plugin.Guid + ".together");
            PvpFragilityPatch.Apply(harmony);
            PiggybackWeightPatch.Apply(harmony);
            Plugin.Instance.gameObject.AddComponent<BridgeTicker>();
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
            if (RefusalText != null) return RefusalText;
            if (!Welcomed) return "waiting for the host to answer, or the host does not run CasualtiesSize";
            NetDataWriter writer = Multiplayer.CreateNamedWriter(MessagePrefix + "Request");
            writer.Put((byte)command.Verb);
            writer.Put(Truncate(command.Target));
            writer.Put(command.Factor);
            writer.Put(Truncate(command.RuleName));
            writer.Put(Truncate(command.RuleValue));
            writer.Put(Truncate(command.Secondary));
            Net.Client_Send(DeliveryMethod.ReliableOrdered, writer);
            return null;
        }

        private static string Truncate(string text)
        {
            if (text == null) return string.Empty;
            return text.Length <= MaxTextLength ? text : text.Substring(0, MaxTextLength);
        }

        private static void HostReceiveHello(ScavPlayer sender, NetDataReader reader)
        {
            if (!Net.IsServer) return;
            ushort clientVersion = reader.GetUShort();
            if (!SizeWire.Compatible(SizeWire.ProtocolVersion, clientVersion))
            {
                Plugin.Debug("hello from " + sender.playerName + " refused, protocol " + clientVersion);
                SendReply(sender, "CasualtiesSize protocol " + clientVersion + " does not match the host's " + SizeWire.ProtocolVersion);
                return;
            }
            HelloedClients.Add((ushort)sender.playerId);
            BroadcastCaptures();
            NetDataWriter writer = Multiplayer.CreateNamedWriter(MessagePrefix + "Welcome");
            writer.Put(SizeWire.ProtocolVersion);
            writer.PutBytesWithLength(SizeWire.EncodeRules(SizeBook.Rules.Snapshot()));
            writer.PutBytesWithLength(SizeWire.EncodeSizes(SizeBook.Copy()));
            Net.Server_SendTo(DeliveryMethod.ReliableOrdered, writer, sender.playerId);
            Plugin.Debug("welcomed " + sender.playerName);
        }

        private static void HostReceiveRequest(ScavPlayer sender, NetDataReader reader)
        {
            if (!Net.IsServer) return;
            byte verbByte = reader.GetByte();
            if (!Enum.IsDefined(typeof(SizeVerb), (int)verbByte)) return;
            SizeCommand command = new SizeCommand
            {
                Verb = (SizeVerb)verbByte,
                Target = reader.GetString(MaxTextLength),
                Factor = reader.GetFloat(),
                RuleName = Blank(reader.GetString(MaxTextLength)),
                RuleValue = Blank(reader.GetString(MaxTextLength)),
                Secondary = Blank(reader.GetString(MaxTextLength)),
            };
            if (float.IsNaN(command.Factor) || float.IsInfinity(command.Factor)) return;
            string reply = SizeService.Execute(command, new PlayerCaller(sender, false), Environment);
            SendReply(sender, reply);
        }

        private static string Blank(string text)
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static void SendReply(ScavPlayer target, string text)
        {
            if (string.IsNullOrEmpty(text) || target == null || target.IsLocal) return;
            NetDataWriter writer = Multiplayer.CreateNamedWriter(MessagePrefix + "Reply");
            writer.Put(Truncate(text.Length > 400 ? text.Substring(0, 400) : text, 400));
            Net.Server_SendTo(DeliveryMethod.ReliableOrdered, writer, target.playerId);
        }

        private static string Truncate(string text, int limit)
        {
            return text.Length <= limit ? text : text.Substring(0, limit);
        }

        private static void ClientReceiveWelcome(NetDataReader reader)
        {
            ushort hostVersion = reader.GetUShort();
            byte[] rulesBytes = reader.GetBytesWithLength();
            byte[] sizesBytes = reader.GetBytesWithLength();
            if (!SizeWire.Compatible(hostVersion, SizeWire.ProtocolVersion))
            {
                RefusalText = "this host runs CasualtiesSize protocol " + hostVersion + ", yours is " + SizeWire.ProtocolVersion + ", size is off for you";
                return;
            }
            Dictionary<string, float> rules;
            Dictionary<int, float> sizes;
            if (!SizeWire.TryDecodeRules(rulesBytes, out rules)) return;
            SizeBook.Rules.Load(rules);
            if (!SizeWire.TryDecodeSizes(sizesBytes, SizeBook.Rules, out sizes)) return;
            SizeBook.ReplaceAll(sizes);
            Welcomed = true;
            RefusalText = null;
            Plugin.Debug("welcome received, " + sizes.Count + " sizes");
        }

        private static void ClientReceiveSizes(NetDataReader reader)
        {
            if (Net.IsServer) return;
            Dictionary<int, float> sizes;
            if (SizeWire.TryDecodeSizes(reader.GetBytesWithLength(), SizeBook.Rules, out sizes)) SizeBook.ReplaceAll(sizes);
        }

        private static void ClientReceiveRules(NetDataReader reader)
        {
            if (Net.IsServer) return;
            Dictionary<string, float> rules;
            if (SizeWire.TryDecodeRules(reader.GetBytesWithLength(), out rules)) SizeBook.Rules.Load(rules);
        }

        private static void ClientReceiveReply(NetDataReader reader)
        {
            ConsoleOutput.Write(reader.GetString(400));
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
            NetDataWriter writer = Multiplayer.CreateNamedWriter(MessagePrefix + "Struggle");
            Net.Client_Send(DeliveryMethod.ReliableOrdered, writer);
        }

        private static void HostReceiveStruggle(ScavPlayer sender, NetDataReader reader)
        {
            if (!Net.IsServer) return;
            CaptureEntry entry;
            if (CaptureBook.ByHeld.TryGetValue(sender.playerId, out entry)) entry.Tracker.Struggle();
        }

        public static void BroadcastCaptures()
        {
            if (!Net.IsServer || HelloedClients.Count == 0) return;
            NetDataWriter writer = Multiplayer.CreateNamedWriter(MessagePrefix + "Captures");
            List<KeyValuePair<int, KeyValuePair<int, byte>>> table = CaptureBook.ToWire();
            writer.Put((ushort)table.Count);
            foreach (KeyValuePair<int, KeyValuePair<int, byte>> row in table)
            {
                writer.Put(row.Key);
                writer.Put(row.Value.Key);
                writer.Put(row.Value.Value);
            }
            Net.Server_SendToClients(DeliveryMethod.ReliableOrdered, writer, HelloedIds());
        }

        private static void ClientReceiveCaptures(NetDataReader reader)
        {
            if (Net.IsServer) return;
            int count = reader.GetUShort();
            if (count > 256) return;
            List<KeyValuePair<int, KeyValuePair<int, byte>>> table = new List<KeyValuePair<int, KeyValuePair<int, byte>>>();
            for (int i = 0; i < count; i++)
            {
                int held = reader.GetInt();
                int holder = reader.GetInt();
                byte mode = reader.GetByte();
                if (mode > 1) return;
                table.Add(new KeyValuePair<int, KeyValuePair<int, byte>>(held, new KeyValuePair<int, byte>(holder, mode)));
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
            if (!Net.IsServer || HelloedClients.Count == 0) return;
            NetDataWriter writer = Multiplayer.CreateNamedWriter(MessagePrefix + "Sizes");
            writer.PutBytesWithLength(SizeWire.EncodeSizes(SizeBook.Copy()));
            Net.Server_SendToClients(DeliveryMethod.ReliableOrdered, writer, HelloedIds());
        }

        public static void BroadcastRules()
        {
            if (!Net.IsServer || HelloedClients.Count == 0) return;
            NetDataWriter writer = Multiplayer.CreateNamedWriter(MessagePrefix + "Rules");
            writer.PutBytesWithLength(SizeWire.EncodeRules(SizeBook.Rules.Snapshot()));
            Net.Server_SendToClients(DeliveryMethod.ReliableOrdered, writer, HelloedIds());
        }

        private static List<knetid> HelloedIds()
        {
            List<knetid> ids = new List<knetid>();
            foreach (ushort id in HelloedClients) ids.Add(new knetid(id));
            return ids;
        }

        private static void HostPlayerLeft(ScavPlayer player)
        {
            if (!Net.IsServer) return;
            HelloedClients.Remove((ushort)player.playerId);
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
                HelloedClients.Clear();
                Welcomed = false;
                RefusalText = null;
                FirstHelloTime = -1f;
                fallbackLogged = false;
                lastHelloTime = -100f;
            }
            if (!running || Net.IsServer || Welcomed || RefusalText != null) return;
            if (!Net.IsConnected || ScavPlayer.LocalPlayer == null) return;
            if (FirstHelloTime >= 0f && !fallbackLogged && Time.unscaledTime - FirstHelloTime > HelloTimeoutSeconds)
            {
                fallbackLogged = true;
                Plugin.Log.LogInfo("The host did not answer, so sizes are off in this game unless the host has CasualtiesSize.");
            }
            if (Time.unscaledTime - lastHelloTime < HelloRetrySeconds) return;
            lastHelloTime = Time.unscaledTime;
            if (FirstHelloTime < 0f) FirstHelloTime = Time.unscaledTime;
            NetDataWriter writer = Multiplayer.CreateNamedWriter(MessagePrefix + "Hello");
            writer.Put(SizeWire.ProtocolVersion);
            Net.Client_Send(DeliveryMethod.ReliableOrdered, writer);
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
