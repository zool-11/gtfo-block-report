using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;

namespace BlockModListSync
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        private static Harmony _harmony;
        private static ManualLogSource _log;

        public override void Load()
        {
            _log = base.Log;
            _log.LogInfo("========================================");
            _log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} 正在加载...");

            try
            {
                _harmony = new Harmony(PluginInfo.GUID);
                _harmony.PatchAll(typeof(Patches));
                
                _log.LogInfo("✅ 补丁加载成功，模组列表已隐藏");
                _log.LogInfo("✅ 其他玩家无法查看你的模组列表");
                _log.LogInfo("========================================");
            }
            catch (System.Exception ex)
            {
                _log.LogError($"❌ 补丁加载失败: {ex.Message}");
            }
        }

        internal static class PluginInfo
        {
            public const string GUID = "dev.blockmodlistsync";
            public const string Name = "BlockModListSync";
            public const string Version = "1.0.0";
        }
    }

    internal static class Patches
    {
        /// <summary>
        /// 拦截模组列表数据发送
        /// 对方请求后收不到数据，超时后自动显示 MOD: UNKNOWN
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("LocaliaCore.Network_Manager", "sendModListData")]
        private static bool Prefix_SendModListData()
        {
            // 跳过原方法，不发送任何模组数据
            return false;
        }
    }
}
