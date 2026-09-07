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
                // 遍历已经加载的程序集，找GTFO.API.NetworkAPI，不硬编码程序集名称
                Type networkApiType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(asm => asm.GetType("GTFO.API.NetworkAPI"))
                    .FirstOrDefault(t => t != null);

                if (networkApiType == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] Cannot find GTFO.API.NetworkAPI, mod disabled");
                    return;
                }
                Log.LogInfo($"[{PluginInfo.Name}] Got NetworkAPI type");

                MethodInfo targetInvokeEvent = null;
                foreach (var m in networkApiType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "InvokeEvent") continue;
                    ParameterInfo[] pars = m.GetParameters();
                    if (pars.Length >= 3 && pars[0].ParameterType == typeof(string))
                    {
                        targetInvokeEvent = m;
                        Log.LogInfo($"[{PluginInfo.Name}] Found InvokeEvent, param count:{pars.Length}");
                        break;
                    }
                }

                if (targetInvokeEvent == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] InvokeEvent method not found, mod disabled");
                    return;
                }

                MethodInfo prefixMethod = typeof(Plugin).GetMethod(nameof(InvokeEventPrefix), BindingFlags.Public | BindingFlags.Static);
                HarmonyMethod harmonyPrefix = new HarmonyMethod(prefixMethod);

                Harmony harmony = new Harmony(PluginInfo.GUID);
                harmony.Patch(targetInvokeEvent, prefix: harmonyPrefix);

                Log.LogInfo($"[{PluginInfo.Name}] Patch apply success");
            }
            catch (Exception ex)
            {
                Log.LogError($"[{PluginInfo.Name}] Load exception: {ex}");
            }
        }

        public static bool InvokeEventPrefix(string eventName, object payload, object target)
        {
            if (eventName == "Localia.ModList.Sync")
            {
                Plugin.LogInstance?.LogInfo("[BlockReport] DETECTED Localia.ModList.Sync is firing!");
            }
            return true;
        }
    }
}
