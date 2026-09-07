using System;
using System.Reflection;
using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;

namespace BlockModListSync
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInDependency("LocaliaCore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource Log;
        private static Harmony _harmony;

        // 预缓存反射元数据
        private static FieldInfo _myChalNumField;
        private static FieldInfo _slotSNetField;
        private static MethodInfo _makeHeaderMethod;
        private static MethodInfo _sendMethod;
        private static MethodInfo _coreVersionMethod;
        private static MethodInfo _arrayGetValueMethod;

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo("========================================");
            Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} 正在加载...");

            try
            {
                // 1. 定位 LocaliaCore 程序集
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
                    Log.LogError("❌ 未找到 LocaliaCore 程序集，补丁加载终止");
                    return;
                }

                // 2. 缓存所有反射元数据，失败则终止
                Type networkType = localiaAssembly.GetType("LocaliaCore.Network_Manager");
                if (networkType == null)
                {
                    Log.LogError("❌ 未找到 Network_Manager 类型");
                    return;
                }

                _myChalNumField = networkType.GetField("myChalNum",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                _slotSNetField = networkType.GetField("slot_SNet",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                _makeHeaderMethod = networkType.GetMethod("MakeHeader",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                _sendMethod = networkType.GetMethod("Send",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                Type apiType = localiaAssembly.GetType("LocaliaCore.API");
                _coreVersionMethod = apiType?.GetMethod("Core_VersionString",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                // 校验核心元数据完整性
                if (_myChalNumField == null || _slotSNetField == null
                    || _makeHeaderMethod == null || _sendMethod == null
                    || _coreVersionMethod == null)
                {
                    Log.LogError("❌ 核心反射元数据不完整，补丁加载终止");
                    return;
                }

                // 预缓存数组取值方法
                object arrayInstance = _slotSNetField.GetValue(null);
                if (arrayInstance != null)
                {
                    _arrayGetValueMethod = arrayInstance.GetType().GetMethod("GetValue", new Type[] { typeof(int) });
                }

                if (_arrayGetValueMethod == null)
                {
                    Log.LogError("❌ 数组取值方法获取失败");
                    return;
                }

                // 3. 挂载补丁
                _harmony = new Harmony(PluginInfo.GUID);

                // 补丁1：拦截模组明细发送
                MethodInfo sendModList = networkType.GetMethod("sendModListData",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (sendModList != null)
                {
                    MethodInfo prefixMod = typeof(Patches).GetMethod(nameof(Patches.Prefix_SendModListData),
                        BindingFlags.Static | BindingFlags.Public);
                    _harmony.Patch(sendModList, prefix: new HarmonyMethod(prefixMod));
                    Log.LogInfo("✅ sendModListData 拦截成功");
                }
                else
                {
                    Log.LogWarning("⚠️ 未找到 sendModListData 方法");
                }

                // 补丁2：核心信息模组数量置0
                MethodInfo sendCoreInfo = networkType.GetMethod("sendCoreInfo",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (sendCoreInfo != null)
                {
                    MethodInfo prefixCore = typeof(Patches).GetMethod(nameof(Patches.Prefix_SendCoreInfo),
                        BindingFlags.Static | BindingFlags.Public);
                    _harmony.Patch(sendCoreInfo, prefix: new HarmonyMethod(prefixCore));
                    Log.LogInfo("✅ sendCoreInfo 模组数量已置0");
                }
                else
                {
                    Log.LogWarning("⚠️ 未找到 sendCoreInfo 方法");
                }

                Log.LogInfo("✅ 模组列表完全隐藏已生效");
                Log.LogInfo("✅ 对方将直接显示 MOD: UNKNOWN");
                Log.LogInfo("========================================");
            }
            catch (Exception ex)
            {
                Log.LogError($"❌ 补丁加载失败: {ex.Message}");
                Log.LogDebug($"❌ 详细堆栈: {ex.StackTrace}");
            }
        }
    }

    internal static class PluginInfo
    {
        public const string GUID = "dev.blockmodlistsync";
        public const string Name = "BlockModListSync";
        public const string Version = "1.0.0";
    }

    public static class Patches
    {
        /// <summary>
        /// 拦截模组列表明细发送，兜底防护
        /// </summary>
        public static bool Prefix_SendModListData()
        {
            return false;
        }

        /// <summary>
        /// 修改核心信息广播：模组数量强制置0
        /// 全链路空值校验，异常自动回退原生逻辑
        /// </summary>
        public static bool Prefix_SendCoreInfo(int slot)
        {
            try
            {
                // 全量空值校验，任何一环失效立即回退
                if (_myChalNumField == null || _slotSNetField == null
                    || _makeHeaderMethod == null || _sendMethod == null
                    || _coreVersionMethod == null || _arrayGetValueMethod == null)
                {
                    return true;
                }

                // 获取本地校验码
                int chalNum = (int)_myChalNumField.GetValue(null);

                // 获取目标连接实例
                object slotArray = _slotSNetField.GetValue(null);
                if (slotArray == null) return true;
                
                object target = _arrayGetValueMethod.Invoke(slotArray, new object[] { slot });
                if (target == null) return true;

                // 构造消息头
                string header = (string)_makeHeaderMethod.Invoke(null, new object[] { 1, false, false });
                if (string.IsNullOrEmpty(header)) return true;

                // 获取核心版本
                string version = (string)_coreVersionMethod.Invoke(null, null);
                if (string.IsNullOrEmpty(version)) return true;

                // 拼接数据包：模组数量强制写0
                string content = $"{header}{chalNum};0;{version}";

                // 发送修改后的数据包
                _sendMethod.Invoke(null, new object[] { target, 1, content, 0u, true });

                // 跳过原生方法
                return false;
            }
            catch
            {
                // 任何异常都回退原生逻辑，优先保证连接稳定
                return true;
            }
        }
    }
}
