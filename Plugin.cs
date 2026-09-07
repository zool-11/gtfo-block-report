using System;
using System.Collections.Generic;
using System.Reflection;
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
        internal static ManualLogSource Log;
        private static Harmony _harmony;
        private static bool _patched;

        private static class Const
        {
            public const string NetworkApiType = "GTFO.API.NetworkAPI, GTFO-API";
            public const string ChannelType = "SNet_ChannelType";
            public const string PlayerType = "SNet_Player";
            public const string TargetMethod = "InvokeFreeSizedEvent";
            public const string BlockEvent = "Localia.ModList.Sync";
        }

        public static class PluginInfo
        {
            public const string GUID = "dev.blockmodlistsync";
            public const string Name = "BlockModListSync";
            public const string Version = "1.0.0";
        }

        public override void Load()
        {
            Log = Logger;
            Log.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} loading...");

            if (_patched)
            {
                Log.LogWarning("Already patched, skipping load");
                return;
            }

            try
            {
                _harmony = new Harmony(PluginInfo.GUID);

                // 运行时解析类型，编译期零外部依赖
                Type networkApi = AccessTools.TypeByName(Const.NetworkApiType);
                Type channelType = AccessTools.TypeByName(Const.ChannelType);
                Type playerType = AccessTools.TypeByName(Const.PlayerType);

                if (networkApi == null || channelType == null || playerType == null)
                {
                    Log.LogError("Failed to resolve required runtime types, patch aborted");
                    return;
                }

                int success = 0;

                // 重载1：全局广播
                success += TryPatch(networkApi, Const.TargetMethod,
                    new[] { typeof(string), typeof(byte[]), channelType },
                    nameof(Prefix_Broadcast)) ? 1 : 0;

                // 重载2：指定单个玩家
                success += TryPatch(networkApi, Const.TargetMethod,
                    new[] { typeof(string), typeof(byte[]), playerType, channelType },
                    nameof(Prefix_SingleTarget)) ? 1 : 0;

                // 重载3：指定多个玩家
                Type enumerablePlayer = typeof(IEnumerable<>).MakeGenericType(playerType);
                success += TryPatch(networkApi, Const.TargetMethod,
                    new[] { typeof(string), typeof(byte[]), enumerablePlayer, channelType },
                    nameof(Prefix_MultiTarget)) ? 1 : 0;

                Log.LogInfo($"Patch finished: {success}/3 overloads applied");
                _patched = success > 0;

                if (success == 0)
                    Log.LogError("All patches failed, mod will not function");
            }
            catch (Exception ex)
            {
                Log.LogError($"Fatal load error: {ex.Message}");
                Log.LogDebug(ex.StackTrace);
            }
        }

        public override bool Unload()
        {
            try
            {
                if (_harmony != null && _patched)
                {
                    _harmony.UnpatchSelf();
                    _patched = false;
                    Log.LogInfo("All patches removed successfully");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Unload error: {ex.Message}");
                return false;
            }
        }

        #region 补丁辅助方法
        /// <summary>
        /// 尝试挂载单个补丁，成功返回true，失败自动捕获异常
        /// </summary>
        private static bool TryPatch(Type type, string methodName, Type[] paramTypes, string prefixName)
        {
            try
            {
                MethodBase target = AccessTools.Method(type, methodName, paramTypes);
                if (target == null)
                {
                    Log.LogWarning($"Method not found: {type.Name}.{methodName}");
                    return false;
                }

                _harmony.Patch(target, prefix: new HarmonyMethod(typeof(Plugin), prefixName));
                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Patch failed [{methodName}]: {ex.Message}");
                Log.LogDebug(ex.StackTrace);
                return false;
            }
        }
        #endregion

        #region 核心拦截逻辑
        private static bool ShouldBlock(string eventName, string scenario)
        {
            if (string.IsNullOrEmpty(eventName))
                return true;

            if (eventName.Equals(Const.BlockEvent, StringComparison.Ordinal))
            {
                Log.LogDebug($"Blocked ModList.Sync ({scenario})");
                return false; // 丢弃数据包
            }
            return true; // 放行其他事件
        }
        #endregion

        #region Harmony 前缀钩子
        private static bool Prefix_Broadcast(string eventName, byte[] payload, object channelType)
        {
            return ShouldBlock(eventName, "broadcast");
        }

        private static bool Prefix_SingleTarget(string eventName, byte[] payload, object target, object channelType)
        {
            return ShouldBlock(eventName, "single target");
        }

        private static bool Prefix_MultiTarget(string eventName, byte[] payload, object targets, object channelType)
        {
            return ShouldBlock(eventName, "multi target");
        }
        #endregion
    }
}
