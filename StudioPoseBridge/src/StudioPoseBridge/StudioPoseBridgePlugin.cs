using System;
using BepInEx;
using BepInEx.Logging;
using StudioPoseBridge.Config;
using StudioPoseBridge.Http;
using StudioPoseBridge.Threading;
using UnityEngine;

namespace StudioPoseBridge
{
    [BepInPlugin("com.suitji.studio_pose_bridge", "Studio Pose Bridge", "0.1.0")]
    [BepInProcess("StudioNEOV2.exe")]
    public class StudioPoseBridgePlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private PluginConfig _pluginConfig;
        private HttpServer _http;

        private void Awake()
        {
            Log = Logger;
            _pluginConfig = new PluginConfig(Config);
            _pluginConfig.EnsureToken();
            Config.Save();

            var go = new GameObject("__StudioPoseBridge__");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<MainThreadDispatcher>();

            if (_pluginConfig.Autostart.Value)
            {
                _http = new HttpServer(_pluginConfig.Port.Value, _pluginConfig.Token.Value, _pluginConfig);
                _http.Start();
                var tok = _pluginConfig.Token.Value;
                var preview = tok.Length >= 4 ? tok.Substring(0, 4) + "…" : "****";
                Log.LogInfo("Listening on http://127.0.0.1:" + _pluginConfig.Port.Value + "/ token=" + preview);
            }
            else
            {
                Log.LogInfo("HTTP server autostart disabled.");
            }
        }

        private void OnDestroy()
        {
            try
            {
                _http?.Dispose();
            }
            catch (Exception ex)
            {
                Log.LogError(ex);
            }
            var go = GameObject.Find("__StudioPoseBridge__");
            if (go != null)
                UnityEngine.Object.Destroy(go);
        }
    }
}
