using System;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using GTFO.API;

namespace BlockPlayerStatusReport
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource LogInstance;
        private static IDetour _detour;

        public static class PluginInfo
        {
            public const string GUID = "temp.blockreport";
            public const string Name = "BlockPlayerStatusReport";
            public const string Version = "1.0.0";
        }

        public override void Load()
        {
            LogInstance = Log;
            Log.LogInfo($"[{PluginInfo.Name}] 已加载：拦截ModList向外上报mod列表");

            MethodInfo targetMethod = null;
            var methods = typeof(NetworkAPI).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach(var m in methods)
            {
                if(m.Name != "InvokeEvent") continue;
                var pars = m.GetParameters();
                if(pars.Length >= 1 && pars[0].ParameterType == typeof(string))
                {
                    targetMethod = m;
                    Log.LogInfo($"[Debug]选中InvokeEvent，参数个数:{pars.Length}");
                    break;
                }
            }

            if (targetMethod == null)
            {
                Log.LogError($"[{PluginInfo.Name}] 找不到NetworkAPI.InvokeEvent，拦截失效！");
                return;
            }
            Log.LogInfo($"[{PluginInfo.Name}] 准备MonoMod detour挂钩");

            _detour = new Detour(
                target: targetMethod,
                hook: Hook_InvokeEvent
            );
            _detour.Apply();
            Log.LogInfo($"[{PluginInfo.Name}] Detour挂钩成功");
        }

        private static void Hook_InvokeEvent(Action<string, object, object> orig, string eventName, object payload, object target)
        {
            if(eventName == "Localia.ModList.Sync")
            {
                LogInstance?.LogInfo($"[BlockReport] 拦截 ModList 上报自身mod列表数据包");
                return;
            }
            orig.Invoke(eventName, payload, target);
        }

        public override bool Unload()
        {
            _detour?.Undo();
            _detour?.Dispose();
            return true;
        }
    }
}
