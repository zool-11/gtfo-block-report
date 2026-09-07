using System;
using System.Reflection;
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
                _log.LogInfo("✅ 其他玩家将看到 MOD: UNKNOWN");
                _log.LogInfo("========================================");
            }
            catch (Exception ex)
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
        /// 拦截模组数据发送：阻止发送具体模组名称列表
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("LocaliaCore.Network_Manager", "sendModListData")]
        private static bool Prefix_SendModListData()
        {
            return false;
        }

        /// <summary>
        /// 修改核心信息广播：将模组数量强制置为0
        /// 对方收到后直接显示 MOD: UNKNOWN，不会发起请求、不会一直加载
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("LocaliaCore.Network_Manager", "sendCoreInfo")]
        private static bool Prefix_SendCoreInfo(int slot)
        {
            try
            {
                Type networkType = Type.GetType("LocaliaCore.Network_Manager, LocaliaCore");
                if (networkType == null)
                    return true;

                // 获取本地校验码
                FieldInfo myChalNumField = networkType.GetField("myChalNum",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                int myChalNum = (int)myChalNumField.GetValue(null);

                // 获取玩家网络实例数组（用 object 接收，避免跨程序集类型冲突）
                FieldInfo slotSNetField = networkType.GetField("slot_SNet",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object slotSNetArray = slotSNetField.GetValue(null);
                
                // 纯反射调用数组的 GetValue 方法，不转换为 Array 类型
                MethodInfo getValueMethod = slotSNetArray.GetType().GetMethod("GetValue", new Type[] { typeof(int) });
                object targetPlayer = getValueMethod.Invoke(slotSNetArray, new object[] { slot });

                // 构造数据包头
                MethodInfo makeHeaderMethod = networkType.GetMethod("MakeHeader",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                string header = (string)makeHeaderMethod.Invoke(null, new object[] { 1, false, false });

                // 获取 LocaliaCore 版本号
                string coreVersion = LocaliaCore.API.Core_VersionString();

                // 核心修改：模组数量强制写 0
                string content = $"{header}{myChalNum};0;{coreVersion}";

                // 发送修改后的数据包
                MethodInfo sendMethod = networkType.GetMethod("Send",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                sendMethod.Invoke(null, new object[] { targetPlayer, 1, content, 0u, true });

                return false;
            }
            catch
            {
                // 异常时回退原方法，保证游戏不崩溃
                return true;
            }
        }
    }
}
