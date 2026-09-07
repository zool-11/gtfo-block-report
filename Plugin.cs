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
            Log.LogInfo($"[{PluginInfo.Name}] Load() start");
            try
            {
                // 运行时查找GTFO‑API，编译不引用
                Type networkApiType = Type.GetType("GTFO.API.NetworkAPI, GTFO‑API");
                if (networkApiType == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] Cannot find GTFO.API.NetworkAPI, mod disabled");
                    return;
                }

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

                // 获取我们写好的Prefix方法，不再用SymbolExtensions带ref null的非法委托
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

        /// <summary>
        /// Prefix签名：不使用ref修改参数（规避IL2CPP泛型+harmony参数改写双重坑）
        /// 关键：绝不 return false 截断泛型方法！！！永远return true，规避AOT实例化报错
        /// 限制：我们不再修改payload，改为【日志打印检测目标事件】，先过编译+日志排查阶段
        /// 先确认：我们能不能捕获到 Localia.ModList.Sync 事件
        /// </summary>
        public static bool InvokeEventPrefix(string eventName, object payload, object target)
        {
            if (eventName == "Localia.ModList.Sync")
            {
                Plugin.LogInstance?.LogInfo("[BlockReport] DETECTED Localia.ModList.Sync is firing!");
                // 现阶段只打印日志，不做阻断、不修改参数，优先确认钩子能不能命中事件
            }
            return true;
        }
    }
}
