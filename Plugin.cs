using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;

namespace BlockModListSync
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource Log;
        private static Harmony _harmony;

        public static class PluginInfo
        {
            public const string GUID = "dev.blockmodlistsync";
            public const string Name = "BlockModListSync";
            public const string Version = "1.0.0-debug2";
        }

        public override void Load()
        {
            Log = Logger;
            Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loading...");

            try
            {
                _harmony = new Harmony(PluginInfo.GUID);

                Type networkApi = AccessTools.TypeByName("GTFO.API.NetworkAPI, GTFO-API");
                Type channelType = AccessTools.TypeByName("SNet_ChannelType");
                Type playerType = AccessTools.TypeByName("SNet_Player");
                Type enumerablePlayer = typeof(IEnumerable<>).MakeGenericType(playerType);

                if (networkApi == null)
                {
                    Log.LogError("NetworkAPI type not found, abort patch");
                    return;
                }

                // 拦截 FreeSized 全部三个重载
                PatchAll(networkApi, "InvokeFreeSizedEvent",
                    new[] { typeof(string), typeof(byte[]), channelType },
                    nameof(Prefix_Free_Broadcast));

                PatchAll(networkApi, "InvokeFreeSizedEvent",
                    new[] { typeof(string), typeof(byte[]), playerType, channelType },
                    nameof(Prefix_Free_Single));

                PatchAll(networkApi, "InvokeFreeSizedEvent",
                    new[] { typeof(string), typeof(byte[]), enumerablePlayer, channelType },
                    nameof(Prefix_Free_Multi));

                // 拦截 Sized 全部三个重载
                PatchAll(networkApi, "InvokeSizedEvent",
                    new[] { typeof(string), typeof(byte[]), channelType },
                    nameof(Prefix_Sized_Broadcast));

                PatchAll(networkApi, "InvokeSizedEvent",
                    new[] { typeof(string), typeof(byte[]), playerType, channelType },
                    nameof(Prefix_Sized_Single));

                PatchAll(networkApi, "InvokeSizedEvent",
                    new[] { typeof(string), typeof(byte[]), enumerablePlayer, channelType },
                    nameof(Prefix_Sized_Multi));

                Log.LogInfo("Debug patch applied: ALL network events will be logged");
            }
            catch (Exception ex)
            {
                Log.LogError($"Load error: {ex.Message}");
                Log.LogDebug(ex.StackTrace);
            }
        }

        public override bool Unload()
        {
            try
            {
                _harmony?.UnpatchSelf();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void PatchAll(Type type, string methodName, Type[] paramTypes, string prefixName)
        {
            try
            {
                MethodBase target = AccessTools.Method(type, methodName, paramTypes);
                if (target == null)
                {
                    Log.LogWarning($"Method not found: {methodName} [{paramTypes.Length} params]");
                    return;
                }
                _harmony.Patch(target, prefix: new HarmonyMethod(GetType(), prefixName));
                Log.LogInfo($"Patched: {methodName} [{paramTypes.Length} params]");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Patch failed {methodName}: {ex.Message}");
            }
        }

        #region FreeSized 调试钩子
        private static bool Prefix_Free_Broadcast(string eventName, byte[] payload, object channelType)
        {
            Log.LogInfo($"[FREE] [Broadcast] {eventName} | size: {payload?.Length ?? 0}");
            return true; // 放行，仅打印
        }

        private static bool Prefix_Free_Single(string eventName, byte[] payload, object target, object channelType)
        {
            Log.LogInfo($"[FREE] [Single] {eventName} | size: {payload?.Length ?? 0}");
            return true;
        }

        private static bool Prefix_Free_Multi(string eventName, byte[] payload, object targets, object channelType)
        {
            Log.LogInfo($"[FREE] [Multi] {eventName} | size: {payload?.Length ?? 0}");
            return true;
        }
        #endregion

        #region Sized 调试钩子
        private static bool Prefix_Sized_Broadcast(string eventName, byte[] payload, object channelType)
        {
            Log.LogInfo($"[SIZED] [Broadcast] {eventName} | size: {payload?.Length ?? 0}");
            return true;
        }

        private static bool Prefix_Sized_Single(string eventName, byte[] payload, object target, object channelType)
        {
            Log.LogInfo($"[SIZED] [Single] {eventName} | size: {payload?.Length ?? 0}");
            return true;
        }

        private static bool Prefix_Sized_Multi(string eventName, byte[] payload, object targets, object channelType)
        {
            Log.LogInfo($"[SIZED] [Multi] {eventName} | size: {payload?.Length ?? 0}");
            return true;
        }
        #endregion
    }
}
