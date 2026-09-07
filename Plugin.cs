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
            Log.LogInfo($"[{PluginInfo.Name}] 成功找到InvokeEvent，准备打补丁");

            var patch = new HarmonyMethod(typeof(Patch), nameof(Patch.ManualPrefix));
            _harmony.Patch(targetMethod, prefix: patch);
        }
    }

    public static class Patch
    {
        public static bool ManualPrefix(object[] __args)
        {
            if(__args == null || __args.Length == 0)
            {
                return true;
            }

            string eventName = __args[0] as string;
            if (eventName == "Localia.ModList.Sync")
            {
                Plugin.LogInstance?.LogInfo($"[BlockReport] 拦截 ModList 上报自身mod列表数据包");
                return false;
            }
            return true;
        }
    }
}
