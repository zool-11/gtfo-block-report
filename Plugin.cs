using System;
using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;

namespace BlockPlayerStatusReport
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource LogInstance;
        private static Harmony _harmonyInstance;

        public static class PluginInfo
        {
            public const string GUID = "temp.blockreport";
            public const string Name = "BlockModListSync";
            public const string Version = "1.0.0";
        }

        public override void Load()
        {
            LogInstance = Log;
            Log.LogInfo($"[{PluginInfo.Name}] Mod loading start");

            try
            {
                _harmonyInstance = new Harmony(PluginInfo.GUID);
                int successCount = 0;

                // 重载1：全局广播 (string eventName, byte[] payload, SNet_ChannelType channelType)
                try
                {
                    var target = new HarmonyMethod(
                        null,
                        "GTFO.API.NetworkAPI, GTFO-API",
                        "InvokeFreeSizedEvent",
                        new []
                        {
                            "System.String",
                            "System.Byte[]",
                            "SNet_ChannelType"
                        }
                    );
                    var prefix = new HarmonyMethod(typeof(Plugin), nameof(OnBroadcastPrefix));
                    _harmonyInstance.Patch(target, prefix);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Log.LogError($"[{PluginInfo.Name}] Failed to patch broadcast overload: {ex.Message}");
                }

                // 重载2：指定单个玩家 (string eventName, byte[] payload, SNet_Player target, SNet_ChannelType channelType)
                try
                {
                    var target = new HarmonyMethod(
                        null,
                        "GTFO.API.NetworkAPI, GTFO-API",
                        "InvokeFreeSizedEvent",
                        new []
                        {
                            "System.String",
                            "System.Byte[]",
                            "SNet_Player",
                            "SNet_ChannelType"
                        }
                    );
                    var prefix = new HarmonyMethod(typeof(Plugin), nameof(OnTargetPrefix));
                    _harmonyInstance.Patch(target, prefix);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Log.LogError($"[{PluginInfo.Name}] Failed to patch target overload: {ex.Message}");
                }

                // 重载3：指定多个玩家 (string eventName, byte[] payload, IEnumerable<SNet_Player> targets, SNet_ChannelType channelType)
                try
                {
                    var target = new HarmonyMethod(
                        null,
                        "GTFO.API.NetworkAPI, GTFO-API",
                        "InvokeFreeSizedEvent",
                        new []
                        {
                            "System.String",
                            "System.Byte[]",
                            "System.Collections.Generic.IEnumerable`1[SNet_Player]",
                            "SNet_ChannelType"
                        }
                    );
                    var prefix = new HarmonyMethod(typeof(Plugin), nameof(OnMultiTargetPrefix));
                    _harmonyInstance.Patch(target, prefix);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Log.LogError($"[{PluginInfo.Name}] Failed to patch multi-target overload: {ex.Message}");
                }

                Log.LogInfo($"[{PluginInfo.Name}] Patch complete: {successCount}/3 overloads applied");

                if (successCount == 0)
                {
                    Log.LogError($"[{PluginInfo.Name}] All patches failed, mod will not work");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[{PluginInfo.Name}] Fatal load error: {ex}");
            }
        }

        public override bool Unload()
        {
            try
            {
                _harmonyInstance?.UnpatchAll(PluginInfo.GUID);
                Log.LogInfo($"[{PluginInfo.Name}] All patches unloaded");
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region 拦截逻辑
        // 广播场景拦截
        private static bool OnBroadcastPrefix(string eventName, byte[] payload, object channelType)
        {
            if (string.IsNullOrEmpty(eventName))
                return true;

            if (eventName == "Localia.ModList.Sync")
            {
                LogInstance.LogDebug("[BlockReport] Dropped ModList.Sync (broadcast)");
                return false;
            }
            return true;
        }

        // 单发场景拦截
        private static bool OnTargetPrefix(string eventName, byte[] payload, object target, object channelType)
        {
            if (string.IsNullOrEmpty(eventName))
                return true;

            if (eventName == "Localia.ModList.Sync")
            {
                LogInstance.LogDebug("[BlockReport] Dropped ModList.Sync (single target)");
                return false;
            }
            return true;
        }

        // 群发场景拦截
        private static bool OnMultiTargetPrefix(string eventName, byte[] payload, object targets, object channelType)
        {
            if (string.IsNullOrEmpty(eventName))
                return true;

            if (eventName == "Localia.ModList.Sync")
            {
                LogInstance.LogDebug("[BlockReport] Dropped ModList.Sync (multi target)");
                return false;
            }
            return true;
        }
        #endregion
    }
}
