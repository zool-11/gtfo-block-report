using System;
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
            Log.LogInfo($"[{PluginInfo.Name}] Load");

            Type networkApiType = Type.GetType("GTFO.API.NetworkAPI, GTFO‑API");
            if (networkApiType == null)
            {
                Log.LogError($"[{PluginInfo.Name}] GTFO‑API not found, exit.");
                return;
            }

            MethodInfo targetInvoke = null;
            foreach (var m in networkApiType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "InvokeEvent") continue;
                var pms = m.GetParameters();
                if (pms.Length >= 3 && pms[0].ParameterType == typeof(string))
                {
                    targetInvoke = m;
                    break;
                }
            }

            if (targetInvoke == null)
            {
                Log.LogError($"[{PluginInfo.Name}] InvokeEvent not found");
                return;
            }

            var harmony = new Harmony(PluginInfo.GUID);
            MethodInfo prefixMethod = SymbolExtensions.GetMethodInfo(() => Prefix(null, ref null, ref null));
            harmony.Patch(targetInvoke, new HarmonyMethod(prefixMethod));

            Log.LogInfo($"[{PluginInfo.Name}] Patch applied");
        }

        /// <summary>
        /// 签名必须匹配参数ref，修改payload；永远return true，绝不截断泛型函数，规避AOT崩溃
        /// </summary>
        public static bool Prefix(string eventName, ref object payload, ref object target)
        {
            if (eventName == "Localia.ModList.Sync")
            {
                Plugin.LogInstance?.LogInfo("[BlockReport] Clear outgoing ModList.Sync payload");
                payload = Array.Empty<object>();
            }
            return true;
        }
    }
}
