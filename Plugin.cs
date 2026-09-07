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
                Assembly modListAssembly = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        // 使用GetExportedTypes，避开Il2Cpp内部无法加载的类型
                        var types = asm.GetExportedTypes();
                        if (types.Any(t => t.FullName != null && t.FullName == "ModList.ModListManager"))
                        {
                            modListAssembly = asm;
                            break;
                        }
                    }
                    catch
                    {
                        // 跳过会抛TypeLoad异常的程序集
                    }
                }

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

                MethodInfo broadcastMethod = modListManagerType.GetMethod("BroadcastMods",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                if (broadcastMethod == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] BroadcastMods method not found");
                    return;
                }
                Log.LogInfo($"[{PluginInfo.Name}] Found BroadcastMods");

                Harmony harmony = new Harmony(PluginInfo.GUID);
                MethodInfo prefixHook = typeof(Plugin).GetMethod(nameof(BroadcastModsPrefix),
                    BindingFlags.Public | BindingFlags.Static);
                harmony.Patch(broadcastMethod, prefix: new HarmonyMethod(prefixHook));

                Log.LogInfo($"[{PluginInfo.Name}] Patch applied success");
            }
            catch (Exception ex)
            {
                Log.LogError($"[{PluginInfo.Name}] Load exception: {ex}");
            }
        }

        public static bool BroadcastModsPrefix()
        {
            Plugin.LogInstance?.LogInfo("[BlockReport] Blocked ModList BroadcastMods, skip sending mod list to others");
            return false;
        }
    }
}
