using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using GTFO.API;

namespace BlockPlayerStatusReport
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        public static class PluginInfo
        {
            public const string GUID = "temp.blockreport";
            public const string Name = "BlockPlayerStatusReport";
            public const string Version = "1.0.0";
        }

        private Harmony _harmony;

        public override void Load()
        {
            Log.LogInfo($"[{PluginInfo.Name}] 已加载，拦截 PlayerStatusUpdate 网络事件");
            _harmony = new Harmony(PluginInfo.GUID);

            MethodInfo targetMethod = AccessTools.Method(
                typeof(NetworkAPI),
                "InvokeEvent",
                new[] { typeof(string), typeof(object) }
            );

            HarmonyMethod prefixPatch = new HarmonyMethod(typeof(Patch), nameof(Patch.Prefix));
            _harmony.Patch(targetMethod, prefixPatch);
        }
    }

    public static class Patch
    {
        public static bool Prefix(string eventName, object data)
        {
            // 返回 false = 阻止原函数执行；true = 放行事件
            if (eventName == "PlayerStatusUpdate")
            {
                return false;
            }
            return true;
        }
    }
}
