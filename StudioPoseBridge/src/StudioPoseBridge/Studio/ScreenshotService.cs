using Studio;
using UnityEngine;

namespace StudioPoseBridge.Game
{
    public static class ScreenshotService
    {
        private static Camera _pooledCam;
        private static GameObject _pooledRoot;

        private static Vector3 GetPelvisWorld(OCIChar oci)
        {
            var hip = BoneAccess.FindBone(oci, "cf_j_hips");
            if (hip?.guideObject?.transformTarget != null)
                return hip.guideObject.transformTarget.position;
            return oci.charInfo != null ? oci.charInfo.transform.position : Vector3.zero;
        }

        private static float FramingScale(string framing)
        {
            switch (framing)
            {
                case "head": return 0.4f;
                case "upper_body": return 0.7f;
                case "tight": return 0.55f;
                default: return 1.0f;
            }
        }

        private static Camera GetOrCreatePooledCamera()
        {
            if (_pooledCam != null) return _pooledCam;
            _pooledRoot = new GameObject("StudioPoseBridge_ScreenshotCam");
            UnityEngine.Object.DontDestroyOnLoad(_pooledRoot);
            _pooledRoot.hideFlags = HideFlags.HideAndDontSave;
            _pooledCam = _pooledRoot.AddComponent<Camera>();
            _pooledCam.enabled = false;
            _pooledCam.clearFlags = CameraClearFlags.Skybox;
            if (Camera.main != null)
            {
                _pooledCam.cullingMask = Camera.main.cullingMask;
                _pooledCam.nearClipPlane = Camera.main.nearClipPlane;
                _pooledCam.farClipPlane = Camera.main.farClipPlane;
            }
            return _pooledCam;
        }

        public static byte[] Capture(
            OCIChar oci,
            string angle,
            int longEdge,
            string format,
            string framing,
            out int width,
            out int height)
        {
            longEdge = Mathf.Clamp(longEdge, 16, 1024);
            if (angle == "current")
            {
                var main = Camera.main;
                if (main == null)
                    throw new System.Exception("No main camera");
                return CaptureFromCamera(main, longEdge, format, out width, out height);
            }

            var pelvis = GetPelvisWorld(oci);
            var s = FramingScale(framing);
            var cam = GetOrCreatePooledCamera();
            if (Camera.main != null)
            {
                cam.cullingMask = Camera.main.cullingMask;
                cam.fieldOfView = Camera.main.fieldOfView;
            }

            var lookTarget = pelvis + new Vector3(0f, 0.1f, 0f);
            Vector3 pos;
            switch (angle)
            {
                case "front":
                    pos = pelvis + new Vector3(0f, 0.2f * s, 2.0f * s);
                    break;
                case "back":
                    pos = pelvis + new Vector3(0f, 0.2f * s, -2.0f * s);
                    break;
                case "left":
                    pos = pelvis + new Vector3(-2.0f * s, 0.2f * s, 0f);
                    break;
                case "right":
                    pos = pelvis + new Vector3(2.0f * s, 0.2f * s, 0f);
                    break;
                case "three_quarter":
                    pos = pelvis + new Vector3(1.4f * s, 0.4f * s, 1.4f * s);
                    break;
                case "top":
                    pos = pelvis + new Vector3(0f, 2.5f * s, 0.01f * s);
                    break;
                default:
                    pos = pelvis + new Vector3(0f, 0.2f * s, 2.0f * s);
                    break;
            }

            cam.transform.position = pos;
            if (angle == "top")
                cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            else
                cam.transform.rotation = Quaternion.LookRotation(lookTarget - pos, Vector3.up);

            return CaptureFromCamera(cam, longEdge, format, out width, out height);
        }

        private static byte[] CaptureFromCamera(Camera cam, int longEdge, string format, out int width, out int height)
        {
            var main = cam;
            var w = Screen.width;
            var h = Screen.height;
            if (w <= 0 || h <= 0)
            {
                w = 1920;
                h = 1080;
            }
            var aspect = (float)w / h;
            if (w < h)
            {
                width = longEdge;
                height = Mathf.Max(1, Mathf.RoundToInt(longEdge / aspect));
            }
            else
            {
                height = longEdge;
                width = Mathf.Max(1, Mathf.RoundToInt(longEdge * aspect));
            }

            var rt = RenderTexture.GetTemporary(width, height, 24);
            var prevTarget = main.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                main.targetTexture = rt;
                main.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                byte[] bytes;
                if (format == "jpg")
                    bytes = tex.EncodeToJPG(85);
                else
                    bytes = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
                return bytes;
            }
            finally
            {
                main.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
