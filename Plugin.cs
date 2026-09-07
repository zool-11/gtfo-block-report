using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using GTFO.API;

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
            public const string Name = "BlockPlayerStatusReport";
            public const string Version = "1.0.0";
        }

        private Harmony _harmony;

        public override void Load()
        {
            LogInstance = Log;
            Log.LogInfo($"[{PluginInfo.Name}] 已加载：拦截ModList向外上报mod列表");
            _harmony = new Harmony(PluginInfo.GUID);

            MethodInfo targetMethod = null;
            // 同时找实例方法 + 静态方法，去掉static过滤
            var methods = typeof(NetworkAPI).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach(var m in methods)
            {
                if(m.Name != "InvokeEvent") continue;
                var pars = m.GetParameters();
                // 参数：第一个string，第二个object，一共2个参数
                if(pars.Length == 2 && pars[0].ParameterType == typeof(string))
                {
                    targetMethod = m;
                    break;
                }
            }

            if (targetMethod == null)
            {
                Log.LogError($"[{PluginInfo.Name}] 找不到NetworkAPI.InvokeEvent，拦截失效！");

                // 打印全部InvokeEvent信息，方便调试
                foreach(var m in typeof(NetworkAPI).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    if(m.Name == "InvokeEvent")
                    {
                        Log.LogError($"[Debug]找到InvokeEvent：IsStatic:{m.IsStatic}, 参数数量:{m.GetParameters().Length}");
                    }
                }
                return;
            }
            Log.LogInfo($"[{PluginInfo.Name}] 成功找到InvokeEvent，IsStatic:{targetMethod.IsStatic}");

            HarmonyMethod prefixPatch = new HarmonyMethod(typeof(Patch), nameof(Patch.Prefix));
            _harmony.Patch(targetMethod, prefixPatch);
        }
    }

    public static class Patch
    {
        // 实例方法的prefix第一个参数必须是 __instance
        public static bool Prefix(object __instance, string eventName, object data)
        {
            if (eventName == "Localia.ModList.Sync")
            {
                Plugin.LogInstance?.LogInfo($"[BlockReport] 拦截 ModList 上报自身mod列表数据包");
                return false;
            }
            return true;
        }
    }
}
