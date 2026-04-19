using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Studio;
using StudioPoseBridge.Config;
using StudioPoseBridge.Game;
using StudioPoseBridge.Http;
using StudioPoseBridge.Threading;
using UnityEngine;

namespace StudioPoseBridge.Endpoints
{
    internal static class PoseEndpoints
    {
        public static async Task<ApiResponse> Health()
        {
            return await MainThreadDispatcher.RunAsync(() =>
            {
                var st = global::Studio.Studio.Instance != null;
                return ApiResponse.Success(new Dictionary<string, object>
                {
                    ["version"] = "0.1.0",
                    ["studio"] = "neov2",
                    ["scene_loaded"] = st
                });
            }).ConfigureAwait(false);
        }

        public static async Task<ApiResponse> ListCharacters()
        {
            return await MainThreadDispatcher.RunAsync(() =>
            {
                var list = CharacterRegistry.ListCharacters();
                var arr = new List<object>();
                foreach (var c in list)
                {
                    arr.Add(new Dictionary<string, object>
                    {
                        ["id"] = c.Id,
                        ["name"] = c.Name,
                        ["sex"] = c.Sex,
                        ["selected"] = c.Selected,
                        ["position"] = c.Position
                    });
                }
                return ApiResponse.Success(new Dictionary<string, object> { ["characters"] = arr });
            }).ConfigureAwait(false);
        }

        public static async Task<ApiResponse> Regions()
        {
            return await Task.FromResult(ApiResponse.Success(new Dictionary<string, object>
            {
                ["regions"] = BoneRegions.Map.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
            })).ConfigureAwait(false);
        }

        public static async Task<ApiResponse> Select(int id)
        {
            return await MainThreadDispatcher.RunAsync(() =>
            {
                if (!CharacterRegistry.TrySelect(id, out var err))
                    return ApiResponse.Fail(err ?? "select failed", "E_NO_CHARACTER", 404);
                return ApiResponse.Success(new Dictionary<string, object>());
            }).ConfigureAwait(false);
        }

        public static async Task<ApiResponse> GetPose(int id, string query, PluginConfig config)
        {
            var q = Router.ParseQuery(query);
            var regionsCsv = q.ContainsKey("regions") ? q["regions"] : null;
            var regions = string.IsNullOrEmpty(regionsCsv)
                ? BoneRegions.DefaultPoseRegions
                : regionsCsv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            var space = q.ContainsKey("space") ? q["space"] : "local";
            if (space != "local" && space != "world")
                return await Task.FromResult(ApiResponse.Fail("invalid space", "E_BAD_REQUEST", 400)).ConfigureAwait(false);
            var includeIk = q.ContainsKey("include_ik") &&
                            (q["include_ik"] == "1" || q["include_ik"].Equals("true", StringComparison.OrdinalIgnoreCase));
            var precision = 1;
            if (q.ContainsKey("precision") && int.TryParse(q["precision"], out var pr))
                precision = Mathf.Clamp(pr, 0, 6);
            var quat = q.ContainsKey("format") && q["format"] == "quat";

            return await MainThreadDispatcher.RunAsync(() =>
            {
                var oci = CharacterRegistry.GetById(id);
                if (oci == null)
                    return ApiResponse.Fail("character not found", "E_NO_CHARACTER", 404);
                var data = PoseSerializer.BuildPoseResponse(oci, id, regions, space, includeIk, precision, quat);
                return ApiResponse.Success(data);
            }).ConfigureAwait(false);
        }

        public static async Task<ApiResponse> WriteBones(int id, HttpListenerRequest req)
        {
            Dictionary<string, object> body;
            try
            {
                body = JsonHelper.DeserializeObject(req.InputStream);
            }
            catch
            {
                return await Task.FromResult(ApiResponse.Fail("invalid json", "E_BAD_REQUEST", 400)).ConfigureAwait(false);
            }

            var space = body.ContainsKey("space") ? body["space"] as string ?? "local" : "local";
            if (space != "local" && space != "world")
                return await Task.FromResult(ApiResponse.Fail("invalid space", "E_BAD_REQUEST", 400)).ConfigureAwait(false);
            if (space == "world")
                return await Task.FromResult(ApiResponse.Fail("world space writes are not supported; use local", "E_BAD_REQUEST", 400)).ConfigureAwait(false);

            if (!body.ContainsKey("changes") || !(body["changes"] is List<object> list))
                return await Task.FromResult(ApiResponse.Fail("missing changes", "E_BAD_REQUEST", 400)).ConfigureAwait(false);

            var changes = new List<BoneChange>();
            foreach (var o in list)
            {
                if (!(o is Dictionary<string, object> d)) continue;
                if (!d.ContainsKey("bone") || !(d["bone"] is string bn)) continue;
                var mode = "absolute";
                if (d.ContainsKey("mode") && d["mode"] is string ms)
                {
                    mode = ms;
                    if (mode == "nudge") mode = "relative";
                }
                Vector3? rot = null;
                if (d.ContainsKey("rot_euler"))
                {
                    if (d["rot_euler"] is List<object> arr && arr.Count >= 3)
                    {
                        rot = new Vector3(Convert.ToSingle(arr[0]), Convert.ToSingle(arr[1]), Convert.ToSingle(arr[2]));
                    }
                }
                if (!rot.HasValue) continue;
                changes.Add(new BoneChange { Bone = bn, Rot = rot.Value, Mode = mode });
            }

            return await MainThreadDispatcher.RunAsync(() =>
            {
                var oci = CharacterRegistry.GetById(id);
                if (oci == null)
                    return ApiResponse.Fail("character not found", "E_NO_CHARACTER", 404);

                var warnings = new List<string>();
                var wasDisabled = false;
                if (!oci.oiCharInfo.enableFK)
                {
                    BoneAccess.EnsureFkEnabled(oci, out wasDisabled);
                    if (wasDisabled) warnings.Add("fk_was_disabled_auto_enabled");
                }

                var applied = new List<object>();
                var skipped = new List<object>();

                foreach (var ch in changes)
                {
                    var bone = BoneAccess.FindBone(oci, ch.Bone);
                    if (bone == null)
                    {
                        skipped.Add(new Dictionary<string, object> { ["bone"] = ch.Bone, ["reason"] = "unknown_bone" });
                        continue;
                    }

                    Vector3 target;
                    if (ch.Mode == "relative")
                    {
                        var cur = bone.guideObject.changeAmount.rot;
                        target = cur + ch.Rot;
                    }
                    else
                    {
                        target = ch.Rot;
                    }

                    bone.guideObject.changeAmount.rot = target;

                    var e = BoneAccess.RoundEuler(bone.guideObject.changeAmount.rot, 4);
                    applied.Add(new Dictionary<string, object>
                    {
                        ["bone"] = ch.Bone,
                        ["rot_euler"] = new[] { e.x, e.y, e.z }
                    });
                }

                return ApiResponse.Success(new Dictionary<string, object>
                {
                    ["applied"] = applied,
                    ["skipped"] = skipped,
                    ["warnings"] = warnings
                });
            }).ConfigureAwait(false);
        }

        private sealed class BoneChange
        {
            public string Bone;
            public Vector3 Rot;
            public string Mode;
        }

        public static async Task<ApiResponse> SetFkIk(int id, HttpListenerRequest req)
        {
            Dictionary<string, object> body;
            try
            {
                body = JsonHelper.DeserializeObject(req.InputStream);
            }
            catch
            {
                return await Task.FromResult(ApiResponse.Fail("invalid json", "E_BAD_REQUEST", 400)).ConfigureAwait(false);
            }

            bool? fk = null;
            bool? ik = null;
            if (body.ContainsKey("fk")) fk = Convert.ToBoolean(body["fk"]);
            if (body.ContainsKey("ik")) ik = Convert.ToBoolean(body["ik"]);

            return await MainThreadDispatcher.RunAsync(() =>
            {
                var oci = CharacterRegistry.GetById(id);
                if (oci == null)
                    return ApiResponse.Fail("character not found", "E_NO_CHARACTER", 404);
                BoneAccess.SetFkIk(oci, fk, ik);
                return ApiResponse.Success(new Dictionary<string, object>());
            }).ConfigureAwait(false);
        }

        public static async Task<ApiResponse> Screenshot(int id, string query)
        {
            var q = Router.ParseQuery(query);
            var angle = q.ContainsKey("angle") ? q["angle"] : "current";
            var size = 512;
            if (q.ContainsKey("size") && int.TryParse(q["size"], out var sz))
                size = sz;
            var fmt = q.ContainsKey("format") ? q["format"] : "png";
            if (fmt != "png" && fmt != "jpg")
                return await Task.FromResult(ApiResponse.Fail("invalid format", "E_BAD_REQUEST", 400)).ConfigureAwait(false);
            var framing = q.ContainsKey("framing") ? q["framing"] : "full_body";

            return await MainThreadDispatcher.RunAsync(() =>
            {
                var oci = CharacterRegistry.GetById(id);
                if (oci == null)
                    return ApiResponse.Fail("character not found", "E_NO_CHARACTER", 404);
                try
                {
                    var bytes = ScreenshotService.Capture(oci, angle, size, fmt, framing, out var w, out var h);
                    var b64 = Convert.ToBase64String(bytes);
                    return ApiResponse.Success(new Dictionary<string, object>
                    {
                        ["format"] = fmt,
                        ["width"] = w,
                        ["height"] = h,
                        ["image_base64"] = b64
                    });
                }
                catch (Exception ex)
                {
                    StudioPoseBridgePlugin.Log.LogError(ex);
                    return ApiResponse.Fail("screenshot failed", "E_INTERNAL", 500);
                }
            }).ConfigureAwait(false);
        }

        public static async Task<ApiResponse> SaveCheckpoint(int id, HttpListenerRequest req)
        {
            Dictionary<string, object> body;
            try
            {
                body = JsonHelper.DeserializeObject(req.InputStream);
            }
            catch
            {
                return await Task.FromResult(ApiResponse.Fail("invalid json", "E_BAD_REQUEST", 400)).ConfigureAwait(false);
            }

            if (!body.ContainsKey("name") || !(body["name"] is string name) || string.IsNullOrWhiteSpace(name))
                return await Task.FromResult(ApiResponse.Fail("missing name", "E_BAD_REQUEST", 400)).ConfigureAwait(false);

            string[] regions;
            if (body.ContainsKey("regions") && body["regions"] is List<object> rl)
            {
                regions = rl.Select(x => x.ToString()).ToArray();
            }
            else
            {
                regions = BoneRegions.AllRegionNames;
            }

            return await MainThreadDispatcher.RunAsync(() =>
            {
                var oci = CharacterRegistry.GetById(id);
                if (oci == null)
                    return ApiResponse.Fail("character not found", "E_NO_CHARACTER", 404);
                var snap = PoseSerializer.CaptureBoneSnapshot(oci, id, regions);
                PoseBridgeRuntime.Checkpoints.Put(id, name, snap);
                var count = 0;
                if (snap["bones"] is IList lb) count = lb.Count;
                return ApiResponse.Success(new Dictionary<string, object>
                {
                    ["name"] = name,
                    ["snapshot"] = snap,
                    ["bone_count"] = count
                });
            }).ConfigureAwait(false);
        }

        public static async Task<ApiResponse> RestoreCheckpoint(int id, string name, HttpListenerRequest req)
        {
            if (string.IsNullOrEmpty(name))
                return await Task.FromResult(ApiResponse.Fail("missing name", "E_BAD_REQUEST", 400)).ConfigureAwait(false);

            return await MainThreadDispatcher.RunAsync(() =>
            {
                if (!PoseBridgeRuntime.Checkpoints.TryGet(id, name, out var snap))
                    return ApiResponse.Fail("checkpoint not found", "E_NOT_FOUND", 404);
                var oci = CharacterRegistry.GetById(id);
                if (oci == null)
                    return ApiResponse.Fail("character not found", "E_NO_CHARACTER", 404);

                BoneAccess.EnsureFkEnabled(oci, out var wasDisabled);
                var warnings = new List<string>();
                if (wasDisabled) warnings.Add("fk_was_disabled_auto_enabled");

                var diff = new List<object>();
                if (snap.ContainsKey("bones") && snap["bones"] is List<object> bl)
                {
                    foreach (var o in bl)
                    {
                        if (!(o is Dictionary<string, object> d)) continue;
                        if (!d.ContainsKey("bone") || !(d["bone"] is string bn)) continue;
                        var bone = BoneAccess.FindBone(oci, bn);
                        if (bone?.guideObject == null) continue;
                        var before = bone.guideObject.changeAmount.rot;
                        if (d.ContainsKey("rot_euler") && d["rot_euler"] is List<object> arr && arr.Count >= 3)
                        {
                            var e = new Vector3(
                                Convert.ToSingle(arr[0]),
                                Convert.ToSingle(arr[1]),
                                Convert.ToSingle(arr[2]));
                            bone.guideObject.changeAmount.rot = e;
                        }
                        var after = bone.guideObject.changeAmount.rot;
                        diff.Add(new Dictionary<string, object>
                        {
                            ["bone"] = bn,
                            ["from"] = new[] { before.x, before.y, before.z },
                            ["to"] = new[] { after.x, after.y, after.z }
                        });
                    }
                }

                return ApiResponse.Success(new Dictionary<string, object>
                {
                    ["restored"] = name,
                    ["warnings"] = warnings,
                    ["diff"] = diff
                });
            }).ConfigureAwait(false);
        }

        public static async Task<ApiResponse> BonesAll(int id, PluginConfig config)
        {
            if (!config.IsDebug)
                return await Task.FromResult(ApiResponse.Fail("debug only", "E_BAD_REQUEST", 400)).ConfigureAwait(false);

            return await MainThreadDispatcher.RunAsync(() =>
            {
                var oci = CharacterRegistry.GetById(id);
                if (oci == null)
                    return ApiResponse.Fail("character not found", "E_NO_CHARACTER", 404);
                var names = new List<string>();
                if (oci.listBones != null)
                {
                    foreach (var b in oci.listBones)
                    {
                        if (b?.gameObject != null)
                            names.Add(b.gameObject.name);
                    }
                }
                return ApiResponse.Success(new Dictionary<string, object> { ["bones"] = names });
            }).ConfigureAwait(false);
        }
    }
}
