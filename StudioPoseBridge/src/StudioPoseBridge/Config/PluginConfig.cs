using System;
using BepInEx.Configuration;

namespace StudioPoseBridge.Config
{
    public sealed class PluginConfig
    {
        public ConfigEntry<int> Port { get; }
        public ConfigEntry<string> Token { get; }
        public ConfigEntry<bool> Autostart { get; }
        public ConfigEntry<PoseBridgeLogLevel> LogLevel { get; }

        public PluginConfig(ConfigFile config)
        {
            Port = config.Bind("Server", "Port", 7842, "Loopback HTTP port.");
            Token = config.Bind("Server", "Token", "", "Shared secret for X-Pose-Token. Empty = generate on first run.");
            Autostart = config.Bind("Server", "Autostart", true, "Start HTTP listener when the plugin loads.");
            LogLevel = config.Bind("Logging", "Level", PoseBridgeLogLevel.Info, "Debug logs every HTTP request.");
        }

        public void EnsureToken()
        {
            if (string.IsNullOrWhiteSpace(Token.Value))
            {
                Token.Value = Guid.NewGuid().ToString("N");
            }
        }

        public bool IsDebug => LogLevel.Value == PoseBridgeLogLevel.Debug;
    }

    public enum PoseBridgeLogLevel
    {
        Info,
        Debug
    }
}
