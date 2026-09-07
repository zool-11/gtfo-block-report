using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using Localia.ModList;

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

            var harmony = new Harmony(PluginInfo.GUID);
            harmony.PatchAll();
            Log.LogInfo($"[{PluginInfo.Name}] Harmony补丁全部挂载完成");
        }
    }

    /// <summary>
    /// 直接拦截 ModList 的广播方法，不碰GTFO‑API泛型InvokeEvent
    /// ModList内部：ModListNetworkManager.BroadcastModList()，该方法触发就会向外发送Localia.ModList.Sync网络包
    /// </summary>
    [HarmonyPatch(typeof(ModListNetworkManager), nameof(ModListNetworkManager.BroadcastModList))]
    public static class Patch_ModListNetworkManager_BroadcastModList
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            Plugin.LogInstance?.LogInfo($"[BlockReport] 拦截 ModList 上报自身mod列表数据包");
            // return false：阻止执行BroadcastModList，直接不发送Sync网络包
            return false;
        }
    }
}
