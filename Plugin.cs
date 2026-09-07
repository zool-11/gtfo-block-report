using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using GTFO.API;
using Il2CppSystem;

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
            Log.LogInfo($"[{PluginInfo.Name}] 已加载：拦截ModList向外上报mod列表");
            _harmony = new Harmony(PluginInfo.GUID);

            MethodInfo targetMethod = AccessTools.Method(
                typeof(NetworkAPI),
                "InvokeEvent",
                new[] { typeof(string), typeof(Object) }
            );

            if (targetMethod == null)
            {
                Log.LogError($"[{PluginInfo.Name}] 找不到NetworkAPI.InvokeEvent，拦截失效！");
                return;
            }

            HarmonyMethod prefixPatch = new HarmonyMethod(typeof(Patch), nameof(Patch.Prefix));
            _harmony.Patch(targetMethod, prefixPatch);
        }
    }

    public static class Patch
    {
        public static bool Prefix(string eventName, Object data)
        {
            if (eventName == "Localia.ModList.Sync")
            {
                Log.LogInfo($"[BlockReport] 拦截 ModList 上报自身mod列表数据包");
                return false;
            }
            return true;
        }
    }
}
