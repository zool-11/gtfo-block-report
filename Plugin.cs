using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;

namespace BlockModListSync
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    [BepInDependency("localia.core", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource Logger;
        private static Harmony _harmony;
        private static bool _patched = false;

        // 反射缓存
        private static FieldInfo _myChalNum;
        private static FieldInfo _slotSNet;
        private static MethodInfo _makeHeader;
        private static MethodInfo _send;
        private static MethodInfo _coreVer;
        private static MethodInfo _arrGet;

        public override void Load()
        {
            Logger = base.Log;
            Logger.LogInfo("========================================");
            Logger.LogInfo("  BlockModListSync 最终编译版");
            Logger.LogInfo("========================================");

            _harmony = new Harmony(PluginInfo.GUID);
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                TryPatch(asm);
        }

        private static void OnAssemblyLoad(object s, AssemblyLoadEventArgs e)
            => TryPatch(e.LoadedAssembly);

        private static void TryPatch(Assembly asm)
        {
            if (_patched) return;
            if (asm.GetName().Name != "LocaliaCore") return;

            DoFullPatch(asm);
            _patched = true;
            Logger.LogInfo("========================================");
            Logger.LogInfo("  ✅ 全链路伪装补丁应用完成");
            Logger.LogInfo("  对方视角：MOD: UNKNOWN（与纯原版一致）");
            Logger.LogInfo("========================================");
        }

        private static void DoFullPatch(Assembly asm)
        {
            const BindingFlags ALL = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type netType = asm.GetType("LocaliaCore.Network_Manager");
            Type monType = asm.GetType("LocaliaCore.LocaliaCore_Moniter");

            #region ========== 反射自检 ==========
            Logger.LogInfo("🔍 反射成员自检...");

            _myChalNum = netType.GetField("myChalNum", ALL);
            _slotSNet = netType.GetField("slot_SNet", ALL);
            _makeHeader = netType.GetMethod("MakeHeader", ALL);
            _send = netType.GetMethod("Send", ALL);
            _arrGet = typeof(Array).GetMethod("GetValue", new[] { typeof(int) });
            Type apiType = asm.GetType("LocaliaCore.API");
            _coreVer = apiType.GetMethod("Core_VersionString", ALL);

            Logger.LogInfo(_myChalNum != null ? "  ✅ myChalNum" : "  ❌ myChalNum 丢失");
            Logger.LogInfo(_slotSNet != null ? "  ✅ slot_SNet" : "  ❌ slot_SNet 丢失");
            Logger.LogInfo(_makeHeader != null ? "  ✅ MakeHeader" : "  ❌ MakeHeader 丢失");
            Logger.LogInfo(_send != null ? "  ✅ Send" : "  ❌ Send 丢失");
            Logger.LogInfo(_coreVer != null ? "  ✅ Core_VersionString" : "  ❌ Core_VersionString 丢失");
            #endregion

            #region ========== 第1层：核心探测彻底屏蔽 ==========
            Logger.LogInfo("--- 第1层：核心探测屏蔽 ---");

            // 1.1 拦截所有A2s信号发送（单发+广播+重试全场景）
            MethodInfo sendA2s = netType.GetMethod("sendA2s_Info", ALL);
            if (sendA2s != null)
            {
                _harmony.Patch(sendA2s, prefix: new HarmonyMethod(typeof(Patches).GetMethod("Block_SendA2s", ALL)));
                Logger.LogInfo("  ✅ sendA2s_Info 全场景拦截");
            }
            else Logger.LogWarning("  ⚠️ sendA2s_Info 未找到");

            // 1.2 进房后置清零广播计数，彻底终止广播循环
            MethodInfo addSlot = netType.GetMethod("addSlotLookup", ALL);
            if (addSlot != null)
            {
                _harmony.Patch(addSlot, postfix: new HarmonyMethod(typeof(Patches).GetMethod("Postfix_AddSlot", ALL)));
                Logger.LogInfo("  ✅ addSlotLookup 后置清零广播");
            }
            else Logger.LogWarning("  ⚠️ addSlotLookup 未找到");

            // 1.3 初始清零兜底
            FieldInfo broadcast = monType.GetField("boardcastAvaliable", ALL);
            if (broadcast != null)
            {
                broadcast.SetValue(null, 0);
                Logger.LogInfo("  ✅ 初始广播计数清零");
            }
            else Logger.LogWarning("  ⚠️ boardcastAvaliable 未找到");
            #endregion

            #region ========== 第2层：核心信息数量伪装 ==========
            Logger.LogInfo("--- 第2层：核心数量伪装 ---");

            MethodInfo sendCore = netType.GetMethod("sendCoreInfo", ALL);
            if (sendCore != null)
            {
                _harmony.Patch(sendCore, prefix: new HarmonyMethod(typeof(Patches).GetMethod("Prefix_SendCoreInfo", ALL)));
                Logger.LogInfo("  ✅ sendCoreInfo 强制数量为0");
            }
            else Logger.LogWarning("  ⚠️ sendCoreInfo 未找到");
            #endregion

            #region ========== 第3层：列表请求接收层拦截 ==========
            Logger.LogInfo("--- 第3层：请求接收屏蔽 ---");

            MethodInfo onRecvGet = netType.GetMethod("onRecv_GetModList", ALL);
            if (onRecvGet != null)
            {
                _harmony.Patch(onRecvGet, prefix: new HarmonyMethod(typeof(Patches).GetMethod("Block_RecvRequest", ALL)));
                Logger.LogInfo("  ✅ onRecv_GetModList 丢弃请求");
            }
            else Logger.LogWarning("  ⚠️ onRecv_GetModList 未找到");
            #endregion

            #region ========== 第4层：模组明细发送拦截 ==========
            Logger.LogInfo("--- 第4层：明细发送拦截 ---");

            MethodInfo sendModList = netType.GetMethod("sendModListData", ALL);
            if (sendModList != null)
            {
                _harmony.Patch(sendModList, prefix: new HarmonyMethod(typeof(Patches).GetMethod("Block_SendModList", ALL)));
                Logger.LogInfo("  ✅ sendModListData 拦截发包");
            }
            else Logger.LogWarning("  ⚠️ sendModListData 未找到");
            #endregion

            #region ========== 第5层：发送缓冲根源清空 ==========
            Logger.LogInfo("--- 第5层：发送缓冲清空 ---");

            FieldInfo buf = netType.GetField("myModListBuffer", ALL);
            if (buf != null)
            {
                buf.SetValue(null, new Dictionary<uint, string>());
                Logger.LogInfo("  ✅ 压缩发送缓冲已清空");
            }

            FieldInfo bufRaw = netType.GetField("myModListBuffer_raw", ALL);
            if (bufRaw != null)
            {
                bufRaw.SetValue(null, new Dictionary<uint, string>());
                Logger.LogInfo("  ✅ 原始发送缓冲已清空");
            }
            #endregion
        }
    }

    internal static class PluginInfo
    {
        public const string GUID = "dev.blockmodlistsync";
        public const string Name = "BlockModListSync";
        public const string Version = "4.2.6";
    }

    public static class Patches
    {
        // 第1层：拦截所有A2s核心探测信号
        public static bool Block_SendA2s(object[] __args)
        {
            Plugin.Logger.LogDebug("[拦截] 阻止发送 A2s 核心探测信号");
            return false;
        }

        // 第1层：进房后置清零广播计数
        public static void Postfix_AddSlot()
        {
            Type monType = Type.GetType("LocaliaCore.LocaliaCore_Moniter, LocaliaCore");
            if (monType == null) return;

            FieldInfo broadcast = monType.GetField("boardcastAvaliable",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (broadcast != null)
            {
                broadcast.SetValue(null, 0);
                Plugin.Logger.LogDebug("[后置] 槽位更新，广播计数已清零");
            }
        }

        // 第2层：核心信息伪装，强制返回0个模组
        public static bool Prefix_SendCoreInfo(int slot)
        {
            try
            {
                object instance = null;
                int chalNum = (int)Plugin._myChalNum.GetValue(instance);
                object slotArray = Plugin._slotSNet.GetValue(instance);
                object target = Plugin._arrGet.Invoke(slotArray, new object[] { slot });
                
                string header = (string)Plugin._makeHeader.Invoke(instance, new object[] { 1, false, false });
                string version = (string)Plugin._coreVer.Invoke(null, null);
                
                string content = $"{header}{chalNum};0;{version}";
                Plugin._send.Invoke(instance, new[] { target, 1, content, 0u, true });
                
                Plugin.Logger.LogDebug($"[伪装] 槽位{slot} 核心信息已发送（数量强制为0）");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[异常] sendCoreInfo 拦截失败，执行原生: {ex.Message}");
                return true;
            }
        }

        // 第3层：收到对方模组列表请求，直接丢弃
        public static bool Block_RecvRequest(object[] __args)
        {
            Plugin.Logger.LogDebug("[拦截] 丢弃对方的模组列表请求");
            return false;
        }

        // 第4层：拦截模组明细分片发送
        public static bool Block_SendModList(object[] __args)
        {
            Plugin.Logger.LogDebug("[拦截] 阻止发送模组明细分片");
            return false;
        }
    }
}
