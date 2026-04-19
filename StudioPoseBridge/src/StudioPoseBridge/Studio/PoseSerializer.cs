using System;
using System.Collections.Generic;
using System.Linq;
using Studio;
using UnityEngine;

namespace StudioPoseBridge.Game
{
    public static class PoseSerializer
    {
        public static Dictionary<string, object> BuildPoseResponse(
            OCIChar oci,
            int characterId,
            string[] regions,
            string space,
            bool includeIk,
            int precision,
            bool quatFormat)
        {
            var regionMap = new Dictionary<string, object>();
            foreach (var region in regions)
            {
                if (!BoneRegions.Map.TryGetValue(region, out var boneNames)) continue;
                var bones = new List<object>();
                foreach (var bn in boneNames)
                {
                    var bone = BoneAccess.FindBone(oci, bn);
                    if (bone == null) continue;
                    if (quatFormat)
                    {
                        var q = BoneAccess.ReadRotationQuat(bone, space);
                        bones.Add(new Dictionary<string, object>
                        {
                            ["bone"] = bn,
                            ["rot_quat"] = new[] { q.x, q.y, q.z, q.w }
                        });
                    }
                    else
                    {
                        var e = BoneAccess.ReadRotation(bone, space);
                        e = BoneAccess.RoundEuler(e, precision);
                        bones.Add(new Dictionary<string, object>
                        {
                            ["bone"] = bn,
                            ["rot_euler"] = new[] { e.x, e.y, e.z }
                        });
                    }
                }
                regionMap[region] = bones;
            }

            var data = new Dictionary<string, object>
            {
                ["character_id"] = characterId,
                ["fk_enabled"] = oci.oiCharInfo != null && oci.oiCharInfo.enableFK,
                ["ik_enabled"] = oci.oiCharInfo != null && oci.oiCharInfo.enableIK,
                ["space"] = space,
                ["regions"] = regionMap
            };

            if (includeIk && oci.listIKTarget != null)
            {
                var ikList = new List<object>();
                var idx = 0;
                foreach (var ik in oci.listIKTarget)
                {
                    if (ik?.guideObject == null) continue;
                    var pos = ik.targetObject != null
                        ? ik.targetObject.position
                        : ik.guideObject.changeAmount.pos;
                    ikList.Add(new Dictionary<string, object>
                    {
                        ["index"] = idx++,
                        ["bone_group"] = ik.boneGroup.ToString(),
                        ["target_pos"] = new[] { pos.x, pos.y, pos.z }
                    });
                }
                data["ik_targets"] = ikList;
            }

            return data;
        }

        public static Dictionary<string, object> CaptureBoneSnapshot(OCIChar oci, int characterId, string[] regions)
        {
            var bones = new List<Dictionary<string, object>>();
            foreach (var region in regions)
            {
                if (!BoneRegions.Map.TryGetValue(region, out var boneNames)) continue;
                foreach (var bn in boneNames)
                {
                    var bone = BoneAccess.FindBone(oci, bn);
                    if (bone?.guideObject == null) continue;
                    var e = bone.guideObject.changeAmount.rot;
                    bones.Add(new Dictionary<string, object>
                    {
                        ["bone"] = bn,
                        ["rot_euler"] = new[] { e.x, e.y, e.z },
                        ["space"] = "local"
                    });
                }
            }
            return new Dictionary<string, object>
            {
                ["character_id"] = characterId,
                ["regions"] = regions.ToList(),
                ["bones"] = bones
            };
        }

        public static List<string> ResolveRegions(string[] requested)
        {
            if (requested == null || requested.Length == 0)
                return BoneRegions.AllRegionNames.ToList();
            return requested.Where(r => BoneRegions.Map.ContainsKey(r)).Distinct().ToList();
        }
    }
}
