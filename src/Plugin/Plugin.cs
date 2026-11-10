using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using CasualtiesSize.Core;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesSize
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(TogetherGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "hive.casualties.size";
        public const string Name = "CasualtiesSize";
        public const string Version = "0.2.0";
        public const string TogetherGuid = "CasualtiesMP";

        public static ManualLogSource Log;
        public static Plugin Instance;
        public static bool DebugOutput;
        public static bool TogetherLoaded;

        private static bool networkedCache;
        private static float networkedCacheTime = -1f;

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            ConfigEntry<bool> debug = Config.Bind("Debug", "Enabled", false, "Log size changes and sync traffic. Off for normal play.");
            DebugOutput = debug.Value;
            TogetherLoaded = Chainloader.PluginInfos.ContainsKey(TogetherGuid);
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            gameObject.AddComponent<SizeApplier>();
            gameObject.AddComponent<StompRunner>();
            gameObject.AddComponent<CaptureRunner>();
            if (TogetherLoaded) StartTogetherBridge();
            Log.LogInfo(Name + " " + Version + " loaded, Together " + (TogetherLoaded ? "present" : "absent") + ", protocol " + SizeWire.ProtocolVersion);
        }

        public static void Debug(string message)
        {
            if (DebugOutput) Log.LogInfo(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StartTogetherBridge()
        {
            try
            {
                TogetherBridge.Start();
            }
            catch (Exception exception)
            {
                TogetherLoaded = false;
                Log.LogWarning("Together is present but the bridge could not start, running as singleplayer only: " + exception.Message);
            }
        }

        public static bool Networked()
        {
            if (!TogetherLoaded) return false;
            if (Time.unscaledTime - networkedCacheTime < 0.2f) return networkedCache;
            networkedCache = AskBridgeNetworked();
            networkedCacheTime = Time.unscaledTime;
            return networkedCache;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool AskBridgeNetworked()
        {
            try
            {
                return TogetherBridge.NetworkRunning();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static IBodyDirectory CurrentDirectory()
        {
            return Networked() ? NetworkDirectory() : SingleplayerDirectory.Instance;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IBodyDirectory NetworkDirectory()
        {
            return TogetherBridge.Directory;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RegisterCommandsWithTogether(System.Collections.Generic.List<Command> commands)
        {
            try
            {
                TogetherBridge.RegisterCommands(commands);
            }
            catch (Exception exception)
            {
                Log.LogWarning("could not register commands with Together: " + exception.Message);
            }
        }

        public static bool IsAuthority()
        {
            return !Networked() || AskBridgeIsServer();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool AskBridgeIsServer()
        {
            return TogetherBridge.IsServer();
        }

        public static int LocalKey()
        {
            return Networked() ? AskBridgeLocalKey() : SizeBook.SingleplayerKey;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int AskBridgeLocalKey()
        {
            return TogetherBridge.LocalKey();
        }

        public static void SendStruggle()
        {
            if (Networked()) SendStruggleViaBridge();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SendStruggleViaBridge()
        {
            TogetherBridge.SendStruggle();
        }

        public static string Dispatch(SizeCommand command)
        {
            if (Networked()) return DispatchNetworked(command);
            if (command.Verb == SizeVerb.Eat || command.Verb == SizeVerb.Pickup || command.Verb == SizeVerb.Release) return "there is no other body to use that on in singleplayer";
            return SizeService.Execute(command, LocalEnvironment.LocalCaller, LocalEnvironment.Instance);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string DispatchNetworked(SizeCommand command)
        {
            return TogetherBridge.Dispatch(command);
        }
    }
}
