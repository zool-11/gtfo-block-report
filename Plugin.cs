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
                // 遍历已加载程序集查找GTFO‑API类型，不硬编码程序集名
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

                // MonoMod Detour劫持，绕过Harmony IL泛型AOT限制
                _invokeDetour = new Detour(targetInvokeEvent, Hook_InvokeEvent);
                _invokeDetour.Apply();
                Log.LogInfo($"[{PluginInfo.Name}] Detour applied success");
            }
            catch (Exception ex)
            {
                Log.LogError($"[{PluginInfo.Name}] Load exception: {ex}");
            }
        }

        /// <summary>
        /// Detour钩子
        /// eventName == Localia.ModList.Sync → 直接return，丢弃发包，不调用orig
        /// 其它所有网络事件：原样调用orig，完全不干涉
        /// </summary>
        private static void Hook_InvokeEvent(Action<string, object, object> orig, string eventName, object payload, object target)
        {
            if (eventName == "Localia.ModList.Sync")
            {
                Plugin.LogInstance?.LogInfo("[BlockReport] Blocked Localia.ModList.Sync network send");
                // 直接返回，不执行原函数，网络包直接丢弃
                return;
            }
            orig.Invoke(eventName, payload, target);
        }

        public override bool Unload()
        {
            _invokeDetour?.Undo();
            _invokeDetour?.Dispose();
            return true;
        }
    }
}
