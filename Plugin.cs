using System;
using System.Reflection;
using UnityEngine;
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
            _log = Logger;
            _log.LogInfo("========================================");
            _log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} 正在加载...");

            try
            {
                _harmony = new Harmony(PluginInfo.GUID);
                var patchedMethods = _harmony.PatchAll(typeof(Patches));
                
                _log.LogInfo($"✅ 补丁加载成功，共拦截 {patchedMethods.Count} 个方法");
                _log.LogInfo("✅ 模组列表广播已屏蔽，其他玩家将无法查看你的模组列表");
                _log.LogInfo("========================================");

                // 游戏内顶部通知
                GameNotification.Show("模组列表屏蔽已生效", Color.green);
            }
            catch (Exception ex)
            {
                _log.LogError($"❌ 补丁加载失败: {ex.Message}");
                _log.LogError($"❌ 详细堆栈: {ex.StackTrace}");
                GameNotification.Show("模组列表屏蔽加载失败", Color.red);
            }
        }

        internal static class PluginInfo
        {
            public const string GUID = "dev.blockmodlistsync";
            public const string Name = "BlockModListSync";
            public const string Version = "1.0.0";
        }
    }

    /// <summary>
    /// 核心补丁：拦截 LocaliaCore 的两个模组发送入口
    /// </summary>
    internal static class Patches
    {
        /// <summary>
        /// 拦截模组数据发送：直接阻止发送具体的模组名称列表
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("LocaliaCore.Network_Manager", "sendModListData")]
        private static bool Prefix_SendModListData()
        {
            // 返回 false = 跳过原方法执行，不发送任何模组数据
            return false;
        }

        /// <summary>
        /// 修改核心信息广播：把模组列表总数量强制改为0，让对方认为你没有模组列表
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("LocaliaCore.Network_Manager", "sendCoreInfo")]
        private static bool Prefix_SendCoreInfo(int slot)
        {
            try
            {
                // 通过反射获取 Network_Manager 内部类型
                Type networkType = Type.GetType("LocaliaCore.Network_Manager, LocaliaCore");
                if (networkType == null)
                    return true;

                // 获取本地挑战码
                FieldInfo myChalNumField = networkType.GetField("myChalNum",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                int myChalNum = (int)myChalNumField.GetValue(null);

                // 获取目标玩家的网络实例
                FieldInfo slotSNetField = networkType.GetField("slot_SNet",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Array slotSNet = (Array)slotSNetField.GetValue(null);
                object targetPlayer = slotSNet.GetValue(slot);

                // 调用内部方法构造数据包头
                MethodInfo makeHeaderMethod = networkType.GetMethod("MakeHeader",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                // 枚举值 1 对应 CORE_INFO
                string header = (string)makeHeaderMethod.Invoke(null, new object[] { 1, false, false });

                // 获取 LocaliaCore 版本号
                string coreVersion = LocaliaCore.API.Core_VersionString();

                // 构造修改后的内容：模组数量强制写 0
                string content = $"{header}{myChalNum};0;{coreVersion}";

                // 调用底层发送方法
                MethodInfo sendMethod = networkType.GetMethod("Send",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                sendMethod.Invoke(null, new object[] { targetPlayer, 1, content, 0u, true });

                // 跳过原方法，用我们修改后的内容替代
                return false;
            }
            catch
            {
                // 异常时回退到原方法，避免游戏崩溃
                return true;
            }
        }
    }

    /// <summary>
    /// 游戏内原生通知辅助类
    /// </summary>
    internal static class GameNotification
    {
        public static void Show(string message, Color color)
        {
            try
            {
                // 通过反射获取 GTFO 原生 GUI 管理层
                var guiManagerType = Type.GetType("GTFO.GuiManager, Assembly-CSharp");
                if (guiManagerType == null) return;

                var currentProp = guiManagerType.GetProperty("Current",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (currentProp == null) return;

                var guiManager = currentProp.GetValue(null);
                if (guiManager == null) return;

                // 调用系统消息通知方法
                var addMsgMethod = guiManagerType.GetMethod("AddSystemMessage",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(string), typeof(Color), typeof(float) },
                    null);

                if (addMsgMethod != null)
                {
                    // 3秒后自动消失
                    addMsgMethod.Invoke(guiManager, new object[] { message, color, 3f });
                }
            }
            catch
            {
                // 通知失败不影响核心功能，静默忽略
            }
        }
    }
}
