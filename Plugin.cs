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
        internal static ManualLogSource Logger;
        private static Harmony _harmony;
        private static bool _patchesApplied = false;

        // 缓存反射元数据
        internal static FieldInfo _myChalNumField;
        internal static FieldInfo _slotSNetField;
        internal static MethodInfo _makeHeaderMethod;
        internal static MethodInfo _sendMethod;
        internal static MethodInfo _coreVersionMethod;
        internal static MethodInfo _arrayGetValueMethod;

        public override void Load()
        {
            Logger = base.Log;
            Logger.LogInfo("========================================");
            Logger.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} 正在加载...");

            // 先检查是否已经加载了 LocaliaCore
            Assembly existingLocalia = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "LocaliaCore")
                {
                    existingLocalia = asm;
                    break;
                }
            }

            if (existingLocalia != null)
            {
                ApplyPatches(existingLocalia);
            }
            else
            {
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                Logger.LogInfo("⏳ 等待 LocaliaCore 程序集加载...");
            }

            Logger.LogInfo("========================================");
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (_patchesApplied) return;

            if (args.LoadedAssembly.GetName().Name == "LocaliaCore")
            {
                ApplyPatches(args.LoadedAssembly);
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            }
        }

        private static void ApplyPatches(Assembly localiaAssembly)
        {
            try
            {
                Logger.LogInfo("📦 检测到 LocaliaCore 程序集，开始应用补丁...");

                Type networkType = localiaAssembly.GetType("LocaliaCore.Network_Manager");
                if (networkType == null)
                {
                    Logger.LogError("❌ 未找到 Network_Manager 类型");
                    return;
                }

                // 反射同时支持实例+静态，避免成员类型不匹配
                const BindingFlags AllFlags = BindingFlags.Instance | BindingFlags.Static 
                                            | BindingFlags.Public | BindingFlags.NonPublic;

                _myChalNumField = networkType.GetField("myChalNum", AllFlags);
                _slotSNetField = networkType.GetField("slot_SNet", AllFlags);
                _makeHeaderMethod = networkType.GetMethod("MakeHeader", AllFlags);
                _sendMethod = networkType.GetMethod("Send", AllFlags);

                Type apiType = localiaAssembly.GetType("LocaliaCore.API");
                _coreVersionMethod = apiType?.GetMethod("Core_VersionString", AllFlags);

                // 校验核心元数据完整性
                if (_myChalNumField == null || _slotSNetField == null
                    || _makeHeaderMethod == null || _sendMethod == null
                    || _coreVersionMethod == null)
                {
                    Logger.LogError("❌ 核心反射元数据不完整，补丁加载终止");
                    return;
                }

                // 预缓存数组取值方法
                // 这里先不取值，运行时从实例中取，避免静态/实例不匹配
                _arrayGetValueMethod = typeof(Array).GetMethod("GetValue", new[] { typeof(int) });

                if (_arrayGetValueMethod == null)
                {
                    Logger.LogError("❌ 数组取值方法获取失败");
                    return;
                }

                _harmony = new Harmony(PluginInfo.GUID);

                // 补丁1：拦截模组明细发送
                MethodInfo sendModList = networkType.GetMethod("sendModListData", AllFlags);
                if (sendModList != null)
                {
                    MethodInfo prefixMod = typeof(Patches).GetMethod(nameof(Patches.Prefix_SendModListData), AllFlags);
                    _harmony.Patch(sendModList, prefix: new HarmonyMethod(prefixMod));
                    Logger.LogInfo("✅ sendModListData 拦截成功");
                }
                else
                {
                    Logger.LogWarning("⚠️ 未找到 sendModListData 方法");
                }

                // 补丁2：核心信息模组数量置0
                MethodInfo sendCoreInfo = networkType.GetMethod("sendCoreInfo", AllFlags);
                if (sendCoreInfo != null)
                {
                    MethodInfo prefixCore = typeof(Patches).GetMethod(nameof(Patches.Prefix_SendCoreInfo), AllFlags);
                    _harmony.Patch(sendCoreInfo, prefix: new HarmonyMethod(prefixCore));
                    Logger.LogInfo("✅ sendCoreInfo 模组数量已置0");
                }
                else
                {
                    Logger.LogWarning("⚠️ 未找到 sendCoreInfo 方法");
                }

                _patchesApplied = true;
                Logger.LogInfo("✅ 模组列表完全隐藏已生效");
                Logger.LogInfo("✅ 对方将直接显示 MOD: UNKNOWN");
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ 补丁加载失败: {ex.Message}");
                Logger.LogDebug($"❌ 详细堆栈: {ex.StackTrace}");
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
            // 直接拦截，不发送明细
            return false;
        }

        /// <summary>
        /// 修改核心信息广播：模组数量强制置0
        /// __instance 为 Harmony 自动传入的方法所属实例（实例方法时有效）
        /// 全链路空值校验，异常自动回退原生逻辑
        /// </summary>
        public static bool Prefix_SendCoreInfo(object __instance, int slot)
        {
            try
            {
                // 全量空值校验，任何一环失效立即回退
                if (Plugin._myChalNumField == null || Plugin._slotSNetField == null
                    || Plugin._makeHeaderMethod == null || Plugin._sendMethod == null
                    || Plugin._coreVersionMethod == null || Plugin._arrayGetValueMethod == null)
                {
                    return true;
                }

                // 自动适配实例/静态：实例方法用 __instance，静态方法传 null
                object instance = Plugin._myChalNumField.IsStatic ? null : __instance;

                // 获取本地校验码
                int chalNum = (int)Plugin._myChalNumField.GetValue(instance);

                // 获取目标连接实例数组
                object slotArray = Plugin._slotSNetField.GetValue(instance);
                if (slotArray == null) return true;
                
                object target = Plugin._arrayGetValueMethod.Invoke(slotArray, new object[] { slot });
                if (target == null) return true;

                // 构造消息头
                string header = (string)Plugin._makeHeaderMethod.Invoke(instance, new object[] { 1, false, false });
                if (string.IsNullOrEmpty(header)) return true;

                // 获取核心版本（API 一般是静态方法，传 null）
                string version = (string)Plugin._coreVersionMethod.Invoke(null, null);
                if (string.IsNullOrEmpty(version)) return true;

                // 拼接数据包：模组数量强制写0
                string content = $"{header}{chalNum};0;{version}";

                // 发送修改后的数据包
                Plugin._sendMethod.Invoke(instance, new[] { target, 1, content, 0u, true });

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
