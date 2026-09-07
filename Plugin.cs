using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;

namespace BlockPlayerStatusReport
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource Logger;
        private static Harmony _harmony;
        private static bool _patched;

        // 所有硬编码字符串集中定义，符合社区常量规范
        private static class Types
        {
            public const string NetworkAPI = "GTFO.API.NetworkAPI, GTFO-API";
            public const string ChannelType = "SNet_ChannelType";
            public const string Player = "SNet_Player";
        }

        private static class Methods
        {
            public const string InvokeFreeSized = "InvokeFreeSizedEvent";
        }

        private static class Events
        {
            public const string ModListSync = "Localia.ModList.Sync";
        }

        public static class PluginInfo
        {
            public const string GUID = "dev.blockmodlistsync";
            public const string Name = "BlockModListSync";
            public const string Version = "1.0.0";
        }

        public override void Load()
        {
            Logger = base.Logger;
            Logger.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} loading...");

            if (_patched)
            {
                Logger.LogWarning("Already patched, skipping");
                return;
            }

            try
            {
                _harmony = new Harmony(PluginInfo.GUID);

                // 运行时解析类型 - 社区标准兼容写法，无需编译期引用
                Type networkApi = AccessTools.TypeByName(Types.NetworkAPI);
                Type channelType = AccessTools.TypeByName(Types.ChannelType);
                Type playerType = AccessTools.TypeByName(Types.Player);

                if (networkApi == null || channelType == null || playerType == null)
                {
                    Logger.LogError("Failed to resolve required runtime types, abort patch");
                    return;
                }

                int successCount = 0;

                // 重载1：全局广播
                if (TryPatch(networkApi, Methods.InvokeFreeSized,
                    new[] { typeof(string), typeof(byte[]), channelType },
                    nameof(Prefix_Broadcast)))
                    successCount++;

                // 重载2：指定单个玩家
                if (TryPatch(networkApi, Methods.InvokeFreeSized,
                    new[] { typeof(string), typeof(byte[]), playerType, channelType },
                    nameof(Prefix_TargetPlayer)))
                    successCount++;

                // 重载3：指定多个玩家
                Type enumerablePlayer = typeof(IEnumerable<>).MakeGenericType(playerType);
                if (TryPatch(networkApi, Methods.InvokeFreeSized,
                    new[] { typeof(string), typeof(byte[]), enumerablePlayer, channelType },
                    nameof(Prefix_MultiPlayer)))
                    successCount++;

                Logger.LogInfo($"Patch finished: {successCount}/3 overloads applied");
                _patched = successCount > 0;

                if (successCount == 0)
                    Logger.LogError("All patches failed, mod will not function");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Fatal load error: {ex.Message}");
                Logger.LogDebug(ex.StackTrace);
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
                    Logger.LogInfo("All patches removed");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Unload error: {ex.Message}");
                return false;
            }
        }

        #region 补丁辅助 - 社区标准封装模式
        /// <summary>
        /// 尝试挂载单个补丁，成功返回true，失败自动捕获并输出日志
        /// </summary>
        private static bool TryPatch(Type type, string methodName, Type[] paramTypes, string prefixName)
        {
            try
            {
                MethodBase target = AccessTools.Method(type, methodName, paramTypes);
                if (target == null)
                {
                    Logger.LogWarning($"Method not found: {type.Name}.{methodName}");
                    return false;
                }

                _harmony.Patch(target, prefix: new HarmonyMethod(typeof(Plugin), prefixName));
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Patch failed [{methodName}]: {ex.Message}");
                Logger.LogDebug(ex.StackTrace);
                return false;
            }
        }
        #endregion

        #region 核心拦截逻辑 - 统一判断入口，消除重复代码
        /// <summary>
        /// 统一事件拦截判断，三个重载钩子共用
        /// </summary>
        private static bool ShouldBlock(string eventName, string scenario)
        {
            if (string.IsNullOrEmpty(eventName))
                return true;

            if (eventName.Equals(Events.ModListSync, StringComparison.Ordinal))
            {
                Logger.LogDebug($"Blocked ModList.Sync ({scenario})");
                return false; // 终止原方法，丢弃数据包
            }
            return true; // 放行其他事件
        }
        #endregion

        #region Harmony 前缀钩子
        private static bool Prefix_Broadcast(string eventName, byte[] payload, object channelType)
        {
            return ShouldBlock(eventName, "broadcast");
        }

        private static bool Prefix_TargetPlayer(string eventName, byte[] payload, object target, object channelType)
        {
            return ShouldBlock(eventName, "single target");
        }

        private static bool Prefix_MultiPlayer(string eventName, byte[] payload, object targets, object channelType)
        {
            return ShouldBlock(eventName, "multi target");
        }
        #endregion
    }
}
