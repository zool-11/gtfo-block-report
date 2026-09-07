using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;

namespace BlockPlayerStatusReport
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource LogInstance;
        private Detour _invokeDetour;
        private DetourInfo _detourInfo;

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
                    Log.LogError($"[{PluginInfo.Name}] InvokeEvent not found, mod disabled");
                    return;
                }

                MethodInfo hookMethod = typeof(Plugin).GetMethod(nameof(InvokeHook), BindingFlags.NonPublic | BindingFlags.Static);
                _detourInfo = DetourInfo.FromSignature(targetInvokeEvent, hookMethod);
                _invokeDetour = new Detour(_detourInfo);
                _invokeDetour.Apply();

                Log.LogInfo($"[{PluginInfo.Name}] Detour applied success");
            }
            catch (Exception ex)
            {
                Log.LogError($"[{PluginInfo.Name}] Load exception: {ex}");
            }
        }

        /// <summary>
        /// Detour钩子，参数顺序：orig放在第一个，后面跟随原方法全部参数
        /// </summary>
        private static void InvokeHook(Action<object[], object> orig, string eventName, object payload, object target)
        {
            if (eventName == "Localia.ModList.Sync")
            {
                Plugin.LogInstance?.LogInfo("[BlockReport] Blocked Localia.ModList.Sync network send");
                // 直接return，不调用原方法，丢弃网络包
                return;
            }
            orig.Invoke(new object[] { eventName, payload, target }, null);
        }

        public override bool Unload()
        {
            _invokeDetour?.Undo();
            _invokeDetour?.Dispose();
            return true;
        }
    }
}
