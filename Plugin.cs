using System;
using System.Reflection;
using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;

namespace BlockModListSync
{
    // 强制依赖 LocaliaCore，确保它先加载完成
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInDependency("LocaliaCore", BepInDependency.DependencyFlags.HardDependency)]
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
                // 手动获取 LocaliaCore 程序集里的目标类型
                Assembly localiaAssembly = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "LocaliaCore")
                    {
                        localiaAssembly = asm;
                        break;
                    }
                }

                if (localiaAssembly == null)
                {
                    _log.LogError("❌ 未找到 LocaliaCore 程序集，补丁加载终止");
                    return;
                }

                Type networkManagerType = localiaAssembly.GetType("LocaliaCore.Network_Manager");
                if (networkManagerType == null)
                {
                    _log.LogError("❌ 未找到 Network_Manager 类型，类名可能已变更");
                    return;
                }

                _harmony = new Harmony(PluginInfo.GUID);

                // 手动补丁 sendModListData 方法
                MethodInfo sendModListMethod = networkManagerType.GetMethod("sendModListData",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (sendModListMethod != null)
                {
                    _harmony.Patch(sendModListMethod,
                        prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.Prefix_SendModListData),
                        BindingFlags.Static | BindingFlags.NonPublic)));
                    _log.LogInfo("✅ sendModListData 拦截成功");
                }
                else
                {
                    _log.LogWarning("⚠️ 未找到 sendModListData 方法");
                }

                _log.LogInfo("✅ 模组列表屏蔽已生效");
                _log.LogInfo("✅ 其他玩家无法查看你的模组列表");
                _log.LogInfo("========================================");
            }
            catch (Exception ex)
            {
                _log.LogError($"❌ 补丁加载失败: {ex.Message}");
                _log.LogError($"❌ 详细堆栈: {ex.StackTrace}");
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
        /// </summary>
        private static bool Prefix_SendModListData()
        {
            // 返回 false = 跳过原方法，不发送任何模组数据
            return false;
        }
    }
}
