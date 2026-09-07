using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;

namespace BlockPlayerStatusReport
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource LogInstance;

        public static class PluginInfo
        {
            public const string GUID = "temp.blockreport";
            public const string Name = "BlockModListSync";
            public const string Version = "1.0.0";
        }

        public override void Load()
        {
            LogInstance = Log;
            Log.LogInfo($"[{PluginInfo.Name}] Load() start");
            try
            {
                // 在已加载程序集查找ModList，不硬编码程序集名称
                Assembly modListAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(asm => asm.GetTypes().Any(t => t.FullName != null && t.FullName.Contains("ModList.ModListManager")));

                if (modListAssembly == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] ModList assembly not found, mod disabled");
                    return;
                }
                Type modListManagerType = modListAssembly.GetType("ModList.ModListManager");
                if (modListManagerType == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] ModListManager type not found");
                    return;
                }
                Log.LogInfo($"[{PluginInfo.Name}] Found ModListManager");

                // 获取向外广播Mod列表的方法 BroadcastMods
                MethodInfo broadcastMethod = modListManagerType.GetMethod("BroadcastMods", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (broadcastMethod == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] BroadcastMods method not found");
                    return;
                }
                Log.LogInfo($"[{PluginInfo.Name}] Found BroadcastMods");

                Harmony harmony = new Harmony(PluginInfo.GUID);
                MethodInfo prefixHook = typeof(Plugin).GetMethod(nameof(BroadcastModsPrefix), BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(broadcastMethod, prefix: new HarmonyMethod(prefixHook));

                Log.LogInfo($"[{PluginInfo.Name}] Patch applied success");
            }
            catch (Exception ex)
            {
                Log.LogError($"[{PluginInfo.Name}] Load exception: {ex}");
            }
        }

        /// <summary>
        /// Harmony Prefix钩子
        /// return false：阻止原始BroadcastMods执行，不会发出Localia.ModList.Sync网络包
        /// </summary>
        public static bool BroadcastModsPrefix()
        {
            Plugin.LogInstance?.LogInfo("[BlockReport] Blocked ModList BroadcastMods, skip sending mod list to others");
            return false;
        }
    }
}
