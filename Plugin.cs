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
                // 根据程序集名称找ModList，ModList.dll的AssemblyName就是ModList
                Assembly modListAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(asm => asm.GetName().Name == "ModList");

                if (modListAssembly == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] ModList assembly not found, mod disabled");
                    return;
                }
                Log.LogInfo($"[{PluginInfo.Name}] Found ModList assembly");

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
