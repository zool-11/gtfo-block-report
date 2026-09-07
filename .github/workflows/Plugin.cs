using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using GTFO.API;

namespace BlockPlayerStatusReport
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("GTFO.exe")]
    public class Plugin : BasePlugin
    {
        public static class PluginInfo
        {
            public const string GUID = "temp.blockreport";
            public const string Name = "BlockPlayerStatusReport";
            public const string Version = "1.0.0";
        }

        private Harmony _harmony;

        public override void Load()
        {
            _harmony = new Harmony(PluginInfo.GUID);
            MethodInfo target = AccessTools.Method(typeof(NetworkAPI), "InvokeEvent", new[] {typeof(string), typeof(object)});
            HarmonyMethod prefix = new HarmonyMethod(typeof(Patch), nameof(Patch.Prefix));
            _harmony.Patch(target, prefix);
        }
    }

    public static class Patch
    {
        public static bool Prefix(string eventName, object data)
        {
            if(eventName == "PlayerStatusUpdate")
            {
                return false;
            }
            return true;
        }
    }
}
