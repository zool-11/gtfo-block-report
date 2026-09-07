using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;

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
            public const string Name = "BlockModListSync";
            public const string Version = "1.0.0";
        }

        public override void Load()
        {
            LogInstance = Log;
            Log.LogInfo($"[{PluginInfo.Name}] Load() start");
            try
            {
                // 按程序集名称定位 GTFO-API
                Assembly gtfoApiAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "GTFO-API");

                if (gtfoApiAsm == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] GTFO-API assembly not found");
                    return;
                }
                Log.LogInfo($"[{PluginInfo.Name}] Found GTFO-API assembly");

                Type networkApiType;
                try
                {
                    networkApiType = gtfoApiAsm.GetType("GTFO.API.NetworkAPI");
                }
                catch (Exception ex)
                {
                    Log.LogError($"[{PluginInfo.Name}] GetType NetworkAPI failed: {ex.Message}");
                    return;
                }

                if (networkApiType == null)
                {
                    Log.LogError($"[{PluginInfo.Name}] NetworkAPI type not found");
                    return;
                }

                Log.LogInfo($"[{PluginInfo.Name}] === NetworkAPI ALL Static Methods ===");

                // 枚举所有静态方法：公开+非公开
                MethodInfo[] methods = networkApiType.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                foreach (var mi in methods)
                {
                    string isGeneric = mi.IsGenericMethod ? " [GENERIC]" : "";
                    var paramList = string.Join(", ", mi.GetParameters()
                        .Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Log.LogInfo($"[{PluginInfo.Name}] {mi.Name}{isGeneric} | ({paramList})");
                }

                Log.LogInfo($"[{PluginInfo.Name}] === End of List ===");
            }
            catch (Exception ex)
            {
                Log.LogError($"[{PluginInfo.Name}] Top-level exception: {ex}");
            }
        }
    }
}
