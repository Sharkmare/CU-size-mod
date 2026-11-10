using System.Collections.Generic;
using CasualtiesSize.Core;

namespace CasualtiesSize.Tests
{
    public sealed class FakeTransport : ISizeTransport
    {
        public bool IsNetworkRunning { get; set; }
        public bool IsHost { get; set; }
        public readonly List<KeyValuePair<string, ushort>> ToPlayers = new List<KeyValuePair<string, ushort>>();
        public readonly List<string> ToHost = new List<string>();

        public void SendToHost(string name, byte[] payload)
        {
            ToHost.Add(name);
        }

        public void SendToPlayer(ushort playerId, string name, byte[] payload)
        {
            ToPlayers.Add(new KeyValuePair<string, ushort>(name, playerId));
        }

        public int CountTo(ushort playerId)
        {
            int count = 0;
            foreach (KeyValuePair<string, ushort> sent in ToPlayers) if (sent.Value == playerId) count++;
            return count;
        }
    }

    public static class GateTests
    {
        private const string Hello = SizeGate.Prefix + SizeGate.HelloName;
        private const string Welcome = SizeGate.Prefix + SizeGate.WelcomeName;

        private static SizeGate Make(FakeTransport transport)
        {
            return new SizeGate(transport, () => new byte[] { 1 }, new byte[] { 1 });
        }

        public static void Register(TestSuite suite)
        {
            suite.Add("gate: singleplayer sends nothing whatever is asked", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = false };
                SizeGate gate = Make(transport);
                gate.ClientTick(1f);
                Check.False(gate.SendToHost("Request", new byte[0]), "host send");
                Check.False(gate.SendToPlayer(3, "Reply", new byte[0]), "player send");
                Check.Equal(0, gate.Broadcast("Sizes", new byte[0]), "broadcast");
                Check.Equal(0, transport.ToHost.Count + transport.ToPlayers.Count, "messages");
                Check.True(gate.SharedFeaturesAllowed, "shared features local");
            });

            suite.Add("gate: a client against a silent host sends only hellos, at the retry rate, and stops at the timeout", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = true, IsHost = false };
                SizeGate gate = Make(transport);
                for (float time = 0f; time < 60f; time += 0.1f)
                {
                    gate.ClientTick(time);
                    gate.SendToHost("Request", new byte[0]);
                }
                foreach (string name in transport.ToHost) Check.Equal(Hello, name, "only hello may be sent");
                Check.True(transport.ToHost.Count <= 3, "hello count " + transport.ToHost.Count);
                Check.Equal(PeerState.NotOurs, gate.Host, "treated as stock host");
                Check.False(gate.SharedFeaturesAllowed, "shared features off");
                Check.True(gate.RefusalText().Contains("does not"), "refusal text");
            });

            suite.Add("gate: a welcome confirms the host and unlocks sends", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = true };
                SizeGate gate = Make(transport);
                gate.ClientTick(0f);
                gate.OnWelcome(true);
                Check.True(gate.SendToHost("Request", new byte[0]), "request after welcome");
                Check.Equal(SizeGate.Prefix + "Request", transport.ToHost[transport.ToHost.Count - 1], "name prefixed");
            });

            suite.Add("gate: an incompatible welcome stops all sends", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = true };
                SizeGate gate = Make(transport);
                gate.ClientTick(0f);
                int before = transport.ToHost.Count;
                gate.OnWelcome(false);
                gate.ClientTick(10f);
                Check.False(gate.SendToHost("Request", new byte[0]), "request refused");
                Check.Equal(before, transport.ToHost.Count, "nothing more sent");
                Check.Equal(PeerState.Incompatible, gate.Host, "state");
            });

            suite.Add("gate: a host sends a stock peer zero messages over 150 attempts and a confirmed peer all of them", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = true, IsHost = true };
                SizeGate gate = Make(transport);
                gate.OnHello(2, true);
                for (int attempt = 0; attempt < 150; attempt++)
                {
                    gate.SendToPlayer(2, "Sizes", new byte[0]);
                    gate.SendToPlayer(5, "Sizes", new byte[0]);
                    gate.Broadcast("Rules", new byte[0]);
                }
                Check.Equal(0, transport.CountTo(5), "stock peer");
                Check.Equal(1 + 150 + 150, transport.CountTo(2), "confirmed peer gets welcome plus everything");
                Check.Equal(Welcome, transport.ToPlayers[0].Key, "welcome is first");
            });

            suite.Add("gate: a hello with a different protocol gets no reply and is never sent to", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = true, IsHost = true };
                SizeGate gate = Make(transport);
                gate.OnHello(4, false);
                gate.Broadcast("Sizes", new byte[0]);
                gate.SendToPlayer(4, "Reply", new byte[0]);
                Check.Equal(0, transport.ToPlayers.Count, "messages to an incompatible peer");
                Check.Equal(PeerState.Incompatible, gate.StateOf(4), "state");
            });

            suite.Add("gate: broadcasts reach exactly the confirmed set", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = true, IsHost = true };
                SizeGate gate = Make(transport);
                gate.OnHello(1, true);
                gate.OnHello(2, true);
                gate.OnHello(3, false);
                transport.ToPlayers.Clear();
                Check.Equal(2, gate.Broadcast("Sizes", new byte[0]), "recipients");
                Check.Equal(1, transport.CountTo(1), "player 1");
                Check.Equal(1, transport.CountTo(2), "player 2");
                Check.Equal(0, transport.CountTo(3), "player 3");
            });

            suite.Add("gate: a player who left is forgotten and a reused id starts unconfirmed", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = true, IsHost = true };
                SizeGate gate = Make(transport);
                gate.OnHello(2, true);
                gate.OnPlayerLeft(2);
                Check.Equal(PeerState.Unknown, gate.StateOf(2), "forgotten");
                Check.False(gate.SendToPlayer(2, "Sizes", new byte[0]), "no send to the reused id");
            });

            suite.Add("gate: reset starts the next session from nothing", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = true };
                SizeGate gate = Make(transport);
                gate.ClientTick(0f);
                gate.OnWelcome(true);
                gate.Reset();
                Check.Equal(PeerState.Unknown, gate.Host, "host state");
                Check.False(gate.SendToHost("Request", new byte[0]), "no send before a new welcome");
                gate.ClientTick(100f);
                Check.Equal(Hello, transport.ToHost[transport.ToHost.Count - 1], "probes again");
            });

            suite.Add("gate: a host ignores a repeated hello from a confirmed peer", () =>
            {
                FakeTransport transport = new FakeTransport { IsNetworkRunning = true, IsHost = true };
                SizeGate gate = Make(transport);
                gate.OnHello(2, true);
                gate.OnHello(2, true);
                Check.Equal(1, transport.CountTo(2), "one welcome");
            });
        }
    }
}
