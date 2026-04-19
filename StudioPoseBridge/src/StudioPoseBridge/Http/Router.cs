using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StudioPoseBridge.Config;
using StudioPoseBridge.Endpoints;

namespace StudioPoseBridge.Http
{
    internal static class Router
    {
        private static readonly Regex RxCharactersId = new Regex(@"^/v1/characters/(\d+)$", RegexOptions.Compiled);
        private static readonly Regex RxSelect = new Regex(@"^/v1/characters/(\d+)/select$", RegexOptions.Compiled);
        private static readonly Regex RxPose = new Regex(@"^/v1/characters/(\d+)/pose$", RegexOptions.Compiled);
        private static readonly Regex RxBones = new Regex(@"^/v1/characters/(\d+)/bones$", RegexOptions.Compiled);
        private static readonly Regex RxFkIk = new Regex(@"^/v1/characters/(\d+)/fk_ik$", RegexOptions.Compiled);
        private static readonly Regex RxScreenshot = new Regex(@"^/v1/characters/(\d+)/screenshot$", RegexOptions.Compiled);
        private static readonly Regex RxCheckpoint = new Regex(@"^/v1/characters/(\d+)/checkpoints$", RegexOptions.Compiled);
        private static readonly Regex RxCheckpointRestore = new Regex(@"^/v1/characters/(\d+)/checkpoints/([^/]+)/restore$", RegexOptions.Compiled);
        private static readonly Regex RxBonesAll = new Regex(@"^/v1/characters/(\d+)/bones/all$", RegexOptions.Compiled);

        public static async Task<ApiResponse> Dispatch(HttpListenerRequest req, PluginConfig config)
        {
            var path = req.Url.AbsolutePath;
            var method = req.HttpMethod.ToUpperInvariant();

            if (method == "GET" && path == "/v1/health")
                return await PoseEndpoints.Health().ConfigureAwait(false);

            if (method == "GET" && path == "/v1/characters")
                return await PoseEndpoints.ListCharacters().ConfigureAwait(false);

            if (method == "GET" && path == "/v1/bones/regions")
                return await PoseEndpoints.Regions().ConfigureAwait(false);

            var m = RxSelect.Match(path);
            if (method == "POST" && m.Success)
                return await PoseEndpoints.Select(int.Parse(m.Groups[1].Value)).ConfigureAwait(false);

            m = RxPose.Match(path);
            if (method == "GET" && m.Success)
                return await PoseEndpoints.GetPose(int.Parse(m.Groups[1].Value), req.Url.Query, config).ConfigureAwait(false);

            m = RxBones.Match(path);
            if (method == "POST" && m.Success)
                return await PoseEndpoints.WriteBones(int.Parse(m.Groups[1].Value), req).ConfigureAwait(false);

            m = RxFkIk.Match(path);
            if (method == "POST" && m.Success)
                return await PoseEndpoints.SetFkIk(int.Parse(m.Groups[1].Value), req).ConfigureAwait(false);

            m = RxScreenshot.Match(path);
            if (method == "GET" && m.Success)
                return await PoseEndpoints.Screenshot(int.Parse(m.Groups[1].Value), req.Url.Query).ConfigureAwait(false);

            m = RxCheckpoint.Match(path);
            if (method == "POST" && m.Success)
                return await PoseEndpoints.SaveCheckpoint(int.Parse(m.Groups[1].Value), req).ConfigureAwait(false);

            m = RxCheckpointRestore.Match(path);
            if (method == "POST" && m.Success)
                return await PoseEndpoints.RestoreCheckpoint(int.Parse(m.Groups[1].Value), Uri.UnescapeDataString(m.Groups[2].Value), req).ConfigureAwait(false);

            m = RxBonesAll.Match(path);
            if (method == "GET" && m.Success)
                return await PoseEndpoints.BonesAll(int.Parse(m.Groups[1].Value), config).ConfigureAwait(false);

            return ApiResponse.Fail("not found", "E_NOT_FOUND", 404);
        }

        public static Dictionary<string, string> ParseQuery(string query)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return d;
            var q = query.TrimStart('?');
            foreach (var part in q.Split('&'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                var kv = part.Split(new[] { '=' }, 2);
                var key = Uri.UnescapeDataString(kv[0]);
                var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                d[key] = val;
            }
            return d;
        }
    }
}
