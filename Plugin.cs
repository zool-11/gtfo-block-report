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
    public class Plugin : BasePlugin
    {
        internal static ManualLogSource Logger;
        private static Harmony _harmony;
        private static bool _patched = false;

        // 反射缓存
        internal static FieldInfo _myChalNum;
        internal static FieldInfo _slotSNet;
        internal static MethodInfo _makeHeader;
        internal static MethodInfo _send;
        internal static MethodInfo _coreVer;
        internal static MethodInfo _arrGet;

        public override void Load()
        {
            Logger = base.Log;
            Logger.LogInfo("========================================");
            Logger.LogInfo("  BlockModListSync 方案二 功能兼容版");
            Logger.LogInfo("  己方：可正常查看所有玩家模组列表");
            Logger.LogInfo("  对方：检测到核心，但永远加载不出列表");
            Logger.LogInfo("========================================");

            _harmony = new Harmony(PluginInfo.GUID);
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

            // 扫描已加载程序集，兼容 LocaliaCore 先加载的情况
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

            #region ========== 第1层：广播计数控制 ==========
            Logger.LogInfo("--- 第1层：广播计数控制 ---");

            // 进房后置清零广播计数，减少冗余广播
            MethodInfo addSlot = netType.GetMethod("addSlotLookup", ALL);
            if (addSlot != null)
            {
                _harmony.Patch(addSlot, postfix: new HarmonyMethod(typeof(Patches).GetMethod("Postfix_AddSlot", ALL)));
                Logger.LogInfo("  ✅ addSlotLookup 后置清零广播");
            }
            else Logger.LogWarning("  ⚠️ addSlotLookup 未找到");

            // 初始广播计数清零兜底
            FieldInfo broadcast = monType.GetField("boardcastAvaliable", ALL);
            if (broadcast != null)
            {
                broadcast.SetValue(null, 0);
                Logger.LogInfo("  ✅ 初始广播计数清零");
            }
            else Logger.LogWarning("  ⚠️ boardcastAvaliable 未找到");
            #endregion

            #region ========== 第2层：列表请求接收拦截 ==========
            Logger.LogInfo("--- 第2层：列表请求拦截 ---");

            MethodInfo onRecvGet = netType.GetMethod("onRecv_GetModList", ALL);
            if (onRecvGet != null)
            {
                _harmony.Patch(onRecvGet, prefix: new HarmonyMethod(typeof(Patches).GetMethod("Block_RecvRequest", ALL)));
                Logger.LogInfo("  ✅ onRecv_GetModList 丢弃请求");
            }
            else Logger.LogWarning("  ⚠️ onRecv_GetModList 未找到");
            #endregion

            #region ========== 第3层：模组明细发送拦截 ==========
            Logger.LogInfo("--- 第3层：明细发送拦截 ---");

            MethodInfo sendModList = netType.GetMethod("sendModListData", ALL);
            if (sendModList != null)
            {
                _harmony.Patch(sendModList, prefix: new HarmonyMethod(typeof(Patches).GetMethod("Block_SendModList", ALL)));
                Logger.LogInfo("  ✅ sendModListData 拦截发包");
            }
            else Logger.LogWarning("  ⚠️ sendModListData 未找到");
            #endregion

            #region ========== 第4层：发送缓冲根源清空 ==========
            Logger.LogInfo("--- 第4层：发送缓冲清空 ---");

            FieldInfo buf = netType.GetField("myModListBuffer", ALL);
            if (buf != null)
            {
                buf.SetValue(null, new Dictionary<uint, string>());
                Logger.LogInfo("  ✅ 压缩发送缓冲已清空");
            }
            else Logger.LogWarning("  ⚠️ myModListBuffer 未找到");

            FieldInfo bufRaw = netType.GetField("myModListBuffer_raw", ALL);
            if (bufRaw != null)
            {
                bufRaw.SetValue(null, new Dictionary<uint, string>());
                Logger.LogInfo("  ✅ 原始发送缓冲已清空");
            }
            else Logger.LogWarning("  ⚠️ myModListBuffer_raw 未找到");
            #endregion
        }
    }

    internal static class PluginInfo
    {
        public const string GUID = "dev.blockmodlistsync";
        public const string Name = "BlockModListSync";
        public const string Version = "4.3.0";
    }

    public static class Patches
    {
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

        // 第2层：收到对方模组列表请求，直接丢弃不响应
        public static bool Block_RecvRequest(object[] __args)
        {
            Plugin.Logger.LogDebug("[拦截] 丢弃对方的模组列表请求");
            return false;
        }

        // 第3层：拦截模组明细分片发送，不返回任何模组数据
        public static bool Block_SendModList(object[] __args)
        {
            Plugin.Logger.LogDebug("[拦截] 阻止发送模组明细分片");
            return false;
        }
    }
}
