using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using GTFO.API;
using HarmonyLib.Public.Patching;

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

            // 使用ManualPatch，绕开Prefix泛型AOT问题
            var harmony = new Harmony(PluginInfo.GUID);
            var manualPatch = new ManualPatch(targetMethod, PatchLogic.Patch);
            ManualPatchManager.Register(targetMethod, manualPatch);
            harmony.Patch(targetMethod);
            Log.LogInfo($"[{PluginInfo.Name}] ManualPatch补丁挂载成功");
        }
    }

    public static class PatchLogic
    {
        /// <summary>
        /// ManualPatch回调，原始方法被调用时进入这里
        /// </summary>
        public static bool Patch(object[] args)
        {
            if(args == null || args.Length == 0)
                return true;

            string eventName = args[0] as string;
            if (eventName == "Localia.ModList.Sync")
            {
                Plugin.LogInstance?.LogInfo($"[BlockReport] 拦截 ModList 上报自身mod列表数据包");
                // 返回false = 不执行原始函数，阻断发包
                return false;
            }
            // 返回true = 执行原始函数
            return true;
        }
    }
}
