using System;
using System.Collections.Generic;
using Studio;
using UnityEngine;

namespace StudioPoseBridge.Game
{
    public static class BoneAccess
    {
        public static OCIChar.BoneInfo FindBone(OCIChar oci, string boneName)
        {
            if (oci?.listBones == null) return null;
            foreach (var b in oci.listBones)
            {
                if (b?.gameObject != null && b.gameObject.name == boneName)
                    return b;
            }
            return null;
        }

        public static bool EnsureFkEnabled(OCIChar oci, out bool wasDisabled)
        {
            wasDisabled = false;
            if (oci?.oiCharInfo == null) return false;
            if (!oci.oiCharInfo.enableFK)
            {
                wasDisabled = true;
                oci.oiCharInfo.enableFK = true;
            }
            foreach (OIBoneInfo.BoneGroup g in Enum.GetValues(typeof(OIBoneInfo.BoneGroup)))
            {
                if (oci.fkCtrl != null)
                    oci.fkCtrl.SetEnable(g, true);
                oci.ActiveFK(g, true, false);
            }
            return true;
        }

        public static Vector3 ReadRotation(OCIChar.BoneInfo bone, string space)
        {
            if (bone?.guideObject == null) return Vector3.zero;
            if (space == "world")
            {
                var t = bone.guideObject.transformTarget;
                if (t == null) return Vector3.zero;
                return t.rotation.eulerAngles;
            }
            return bone.guideObject.changeAmount.rot;
        }

        public static Quaternion ReadRotationQuat(OCIChar.BoneInfo bone, string space)
        {
            if (bone?.guideObject == null) return Quaternion.identity;
            if (space == "world")
            {
                var t = bone.guideObject.transformTarget;
                return t != null ? t.rotation : Quaternion.identity;
            }
            var e = bone.guideObject.changeAmount.rot;
            return Quaternion.Euler(e);
        }

        public static void WriteRotationAbsolute(OCIChar.BoneInfo bone, Vector3 euler)
        {
            if (bone?.guideObject == null) return;
            bone.guideObject.changeAmount.rot = euler;
        }

        public static void WriteRotationRelative(OCIChar.BoneInfo bone, Vector3 delta)
        {
            if (bone?.guideObject == null) return;
            var cur = bone.guideObject.changeAmount.rot;
            bone.guideObject.changeAmount.rot = cur + delta;
        }

        public static Vector3 RoundEuler(Vector3 v, int decimals)
        {
            return new Vector3(
                (float)Math.Round(v.x, decimals, MidpointRounding.AwayFromZero),
                (float)Math.Round(v.y, decimals, MidpointRounding.AwayFromZero),
                (float)Math.Round(v.z, decimals, MidpointRounding.AwayFromZero));
        }

        public static void SetFkIk(OCIChar oci, bool? fk, bool? ik)
        {
            if (oci == null) return;
            if (fk.HasValue)
            {
                oci.oiCharInfo.enableFK = fk.Value;
                foreach (OIBoneInfo.BoneGroup g in Enum.GetValues(typeof(OIBoneInfo.BoneGroup)))
                {
                    if (oci.fkCtrl != null)
                        oci.fkCtrl.SetEnable(g, fk.Value);
                    oci.ActiveFK(g, fk.Value, false);
                }
            }
            if (ik.HasValue)
            {
                oci.oiCharInfo.enableIK = ik.Value;
                foreach (OIBoneInfo.BoneGroup g in Enum.GetValues(typeof(OIBoneInfo.BoneGroup)))
                    oci.ActiveIK(g, ik.Value, false);
            }
        }
    }
}
