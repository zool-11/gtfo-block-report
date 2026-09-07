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

        // 缓存反射元数据，避免重复查找
        private static Type _networkManagerType;
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

                // 2. 缓存所有需要的反射元数据
                _networkManagerType = localiaAssembly.GetType("LocaliaCore.Network_Manager");
                if (_networkManagerType == null)
                {
                    Log.LogError("❌ 未找到 Network_Manager 类型，补丁加载终止");
                    return;
                }

                _myChalNumField = _networkManagerType.GetField("myChalNum",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                _slotSNetField = _networkManagerType.GetField("slot_SNet",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                _makeHeaderMethod = _networkManagerType.GetMethod("MakeHeader",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                _sendMethod = _networkManagerType.GetMethod("Send",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                Type apiType = localiaAssembly.GetType("LocaliaCore.API");
                _coreVersionMethod = apiType?.GetMethod("Core_VersionString",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                // 3. 挂载补丁
                _harmony = new Harmony(PluginInfo.GUID);

                // 补丁1：拦截模组明细发送
                MethodInfo sendModListMethod = _networkManagerType.GetMethod("sendModListData",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (sendModListMethod != null)
                {
                    MethodInfo prefixModList = typeof(Patches).GetMethod(nameof(Patches.Prefix_SendModListData),
                        BindingFlags.Static | BindingFlags.Public);
                    _harmony.Patch(sendModListMethod, prefix: new HarmonyMethod(prefixModList));
                    Log.LogInfo("✅ sendModListData 拦截成功");
                }
                else
                {
                    Log.LogWarning("⚠️ 未找到 sendModListData 方法");
                }

                // 补丁2：核心信息模组数量置0
                MethodInfo sendCoreInfoMethod = _networkManagerType.GetMethod("sendCoreInfo",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (sendCoreInfoMethod != null)
                {
                    MethodInfo prefixCoreInfo = typeof(Patches).GetMethod(nameof(Patches.Prefix_SendCoreInfo),
                        BindingFlags.Static | BindingFlags.Public);
                    _harmony.Patch(sendCoreInfoMethod, prefix: new HarmonyMethod(prefixCoreInfo));
                    Log.LogInfo("✅ sendCoreInfo 模组数量已置0");
                }
                else
                {
                    Log.LogWarning("⚠️ 未找到 sendCoreInfo 方法");
                }

                // 预缓存数组GetValue方法，运行时直接调用
                if (_slotSNetField != null)
                {
                    object arrayInstance = _slotSNetField.GetValue(null);
                    if (arrayInstance != null)
                    {
                        _arrayGetValueMethod = arrayInstance.GetType().GetMethod("GetValue", new Type[] { typeof(int) });
                    }
                }

                Log.LogInfo("✅ 模组列表完全隐藏已生效");
                Log.LogInfo("✅ 对方将直接显示 MOD: UNKNOWN，无加载过程");
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
        /// 对方收到后直接判定为无模组列表，不发起数据请求
        /// </summary>
        public static bool Prefix_SendCoreInfo(int slot)
        {
            try
            {
                // 所有元数据已预缓存，直接使用，避免重复反射
                if (Plugin._myChalNumField == null
                    || Plugin._slotSNetField == null
                    || Plugin._makeHeaderMethod == null
                    || Plugin._sendMethod == null
                    || Plugin._coreVersionMethod == null
                    || Plugin._arrayGetValueMethod == null)
                {
                    return true;
                }

                // 获取本地校验码
                int myChalNum = (int)Plugin._myChalNumField.GetValue(null);

                // 获取目标玩家连接实例
                object slotSNetArray = Plugin._slotSNetField.GetValue(null);
                object targetPlayer = Plugin._arrayGetValueMethod.Invoke(slotSNetArray, new object[] { slot });

                // 构造消息头
                string header = (string)Plugin._makeHeaderMethod.Invoke(null, new object[] { 1, false, false });

                // 获取核心版本
                string coreVersion = (string)Plugin._coreVersionMethod.Invoke(null, null);

                // 核心修改：模组数量强制写 0
                string content = $"{header}{myChalNum};0;{coreVersion}";

                // 发送修改后的数据包
                Plugin._sendMethod.Invoke(null, new object[] { targetPlayer, 1, content, 0u, true });

                // 跳过原生方法执行
                return false;
            }
            catch
            {
                // 异常自动回退原生逻辑，优先保证连接稳定
                return true;
            }
        }
    }
}
