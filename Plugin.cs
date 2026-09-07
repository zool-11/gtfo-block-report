using System;
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
        private static Detour _invokeEventDetour;

        public static class PluginInfo
        {
            public const string GUID = "temp.blockreport";
            public const string Name = "BlockModListSync";
            public const string Version = "1.0.0";
        }

        public override void Load()
        {
            LogInstance = Log;
            Log.LogInfo($"[{PluginInfo.Name}] Loaded: Block Localia.ModList.Sync broadcast");

            // 运行时反射查找GTFO‑API，编译不需要引用GTFO‑API
            Type networkApiType = Type.GetType("GTFO.API.NetworkAPI, GTFO‑API");
            if (networkApiType is null)
            {
                Log.LogError($"[{PluginInfo.Name}] GTFO‑API not found, mod disabled.");
                return;
            }

            MethodInfo targetInvokeEvent = null;
            foreach (var m in networkApiType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "InvokeEvent") continue;
                var p = m.GetParameters();
                if (p.Length >= 1 && p[0].ParameterType == typeof(string))
                {
                    targetInvokeEvent = m;
                    break;
                }
            }

            if (targetInvokeEvent is null)
            {
                Log.LogError($"[{PluginInfo.Name}] Cannot find InvokeEvent, mod disabled.");
                return;
            }

            // MonoMod Detour，内存层面替换函数，完全规避Harmony AOT泛型问题
            _invokeEventDetour = new Detour(targetInvokeEvent, Hook_InvokeEvent);
            _invokeEventDetour.Apply();
            Log.LogInfo($"[{PluginInfo.Name}] Detour applied successfully.");
        }

        /// <summary>
        /// Detour钩子，匹配InvokeEvent<T>签名
        /// 一旦 eventName == Localia.ModList.Sync，直接return，不执行原函数，阻断网络发包
        /// </summary>
        private static void Hook_InvokeEvent(Action<string, object, object> orig, string eventName, object payload, object target)
        {
            if (eventName == "Localia.ModList.Sync")
            {
                LogInstance?.LogInfo($"[BlockReport] Blocked Localia.ModList.Sync network send");
                // 直接return，不调用orig，网络包不会发出
                return;
            }
            // 其余所有网络事件全部放行，调用原始函数
            orig.Invoke(eventName, payload, target);
        }

        public override void Unload()
        {
            _invokeEventDetour?.Undo();
            _invokeEventDetour?.Dispose();
        }
    }
}
