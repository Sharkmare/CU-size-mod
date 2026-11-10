using System;
using System.Collections.Generic;

namespace CasualtiesSize.Core
{
    public enum PeerState
    {
        Unknown,
        Confirmed,
        NotOurs,
        Incompatible,
    }

    public interface ISizeTransport
    {
        bool IsNetworkRunning { get; }
        bool IsHost { get; }
        void SendToHost(string name, byte[] payload);
        void SendToPlayer(ushort playerId, string name, byte[] payload);
    }

    /// <summary>
    /// Nothing of ours is sent to a peer until that peer has proved it is ours with a compatible hello
    /// (host side) or welcome (client side). The only exception is the hello itself, sent by a client
    /// until the timeout.
    /// </summary>
    public sealed class SizeGate
    {
        public const string Prefix = "CasualtiesSize.";
        public const string HelloName = "Hello";
        public const string WelcomeName = "Welcome";
        public const float HelloRetrySeconds = 2f;
        public const float HelloTimeoutSeconds = 5f;

        private readonly ISizeTransport transport;
        private readonly Func<byte[]> welcomePayload;
        private readonly byte[] helloPayload;
        private readonly Dictionary<ushort, PeerState> peers = new Dictionary<ushort, PeerState>();
        private PeerState hostState = PeerState.Unknown;
        private float firstHelloTime = -1f;
        private float lastHelloTime = -1000f;

        public SizeGate(ISizeTransport transport, Func<byte[]> welcomePayload, byte[] helloPayload)
        {
            this.transport = transport;
            this.welcomePayload = welcomePayload;
            this.helloPayload = helloPayload;
        }

        public PeerState Host { get { return hostState; } }

        public bool SharedFeaturesAllowed
        {
            get { return !transport.IsNetworkRunning || transport.IsHost || hostState == PeerState.Confirmed; }
        }

        public PeerState StateOf(ushort playerId)
        {
            PeerState state;
            return peers.TryGetValue(playerId, out state) ? state : PeerState.Unknown;
        }

        public IEnumerable<ushort> ConfirmedPeers()
        {
            foreach (KeyValuePair<ushort, PeerState> entry in peers)
                if (entry.Value == PeerState.Confirmed) yield return entry.Key;
        }

        public void ClientTick(float now)
        {
            if (!transport.IsNetworkRunning || transport.IsHost) return;
            if (hostState != PeerState.Unknown) return;
            if (firstHelloTime < 0f) firstHelloTime = now;
            if (now - firstHelloTime >= HelloTimeoutSeconds)
            {
                hostState = PeerState.NotOurs;
                return;
            }
            if (now - lastHelloTime >= HelloRetrySeconds)
            {
                lastHelloTime = now;
                transport.SendToHost(Prefix + HelloName, helloPayload);
            }
        }

        public void OnWelcome(bool compatible)
        {
            if (hostState != PeerState.Unknown) return;
            hostState = compatible ? PeerState.Confirmed : PeerState.Incompatible;
        }

        public void OnHello(ushort playerId, bool compatible)
        {
            if (!transport.IsHost) return;
            if (StateOf(playerId) == PeerState.Confirmed) return;
            if (!compatible)
            {
                peers[playerId] = PeerState.Incompatible;
                return;
            }
            peers[playerId] = PeerState.Confirmed;
            transport.SendToPlayer(playerId, Prefix + WelcomeName, welcomePayload());
        }

        public void OnPlayerLeft(ushort playerId)
        {
            peers.Remove(playerId);
        }

        public void Reset()
        {
            peers.Clear();
            hostState = PeerState.Unknown;
            firstHelloTime = -1f;
            lastHelloTime = -1000f;
        }

        public bool SendToPlayer(ushort playerId, string name, byte[] payload)
        {
            if (!transport.IsHost || StateOf(playerId) != PeerState.Confirmed) return false;
            transport.SendToPlayer(playerId, Prefix + name, payload);
            return true;
        }

        public int Broadcast(string name, byte[] payload)
        {
            int sent = 0;
            foreach (ushort playerId in new List<ushort>(ConfirmedPeers()))
            {
                transport.SendToPlayer(playerId, Prefix + name, payload);
                sent++;
            }
            return sent;
        }

        public bool SendToHost(string name, byte[] payload)
        {
            if (transport.IsHost || hostState != PeerState.Confirmed) return false;
            transport.SendToHost(Prefix + name, payload);
            return true;
        }

        public string RefusalText()
        {
            switch (hostState)
            {
                case PeerState.NotOurs:
                    return "size needs the host to run CasualtiesSize. This host does not, so nothing changes.";
                case PeerState.Incompatible:
                    return "the host runs a different CasualtiesSize protocol than yours, so size is off for you.";
                case PeerState.Unknown:
                    return "waiting to find out whether the host runs CasualtiesSize, try again in a few seconds.";
                default:
                    return null;
            }
        }
    }
}
