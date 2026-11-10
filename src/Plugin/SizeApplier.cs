using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesSize
{
    public struct BodyHandle
    {
        public int Key;
        public Body Body;
        public bool IsLocal;
    }

    public interface IBodyDirectory
    {
        IEnumerable<BodyHandle> Bodies();
    }

    public sealed class SingleplayerDirectory : IBodyDirectory
    {
        public static readonly SingleplayerDirectory Instance = new SingleplayerDirectory();

        public IEnumerable<BodyHandle> Bodies()
        {
            if (PlayerCamera.main != null && PlayerCamera.main.body != null)
                yield return new BodyHandle { Key = SizeBook.SingleplayerKey, Body = PlayerCamera.main.body, IsLocal = true };
        }
    }

    public sealed class SizeApplier : MonoBehaviour
    {
        private const float ScanIntervalSeconds = 0.25f;

        private float nextScan;
        private float cameraBaseSize;
        private bool cameraBaseKnown;
        private Camera cameraHoldingBase;

        private const string PixelPerfectTypeName = "UnityEngine.Experimental.Rendering.Universal.PixelPerfectCamera";

        private SizedBody localSized;
        private Component pixelPerfectOwner;
        private int baseResolutionX;
        private int baseResolutionY;

        private void LateUpdate()
        {
            if (localSized != null) FollowCamera(localSized);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + ScanIntervalSeconds;
            localSized = null;
            foreach (BodyHandle handle in Plugin.CurrentDirectory().Bodies())
            {
                if (handle.Body == null) continue;
                float target = SizeBook.Get(handle.Key);
                SizedBody sized = handle.Body.GetComponent<SizedBody>();
                if (sized == null && target == 1f) continue;
                if (sized == null) sized = SizedBody.For(handle.Body);
                sized.SetTarget(target);
                if (handle.IsLocal) localSized = sized;
            }
        }

        private void FollowCamera(SizedBody sized)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            float zoom = Core.SizeMath.CameraZoomMult(SizeBook.Rules, sized.Current);
            Component pixelPerfect = camera.GetComponent(PixelPerfectTypeName);
            if (pixelPerfect != null)
            {
                ZoomPixelPerfect(pixelPerfect, zoom);
                return;
            }
            if (!camera.orthographic) return;
            if (!cameraBaseKnown || cameraHoldingBase != camera)
            {
                cameraBaseSize = camera.orthographicSize;
                cameraHoldingBase = camera;
                cameraBaseKnown = true;
            }
            camera.orthographicSize = cameraBaseSize * zoom;
        }

        private void ZoomPixelPerfect(Component pixelPerfect, float zoom)
        {
            System.Type type = pixelPerfect.GetType();
            System.Reflection.PropertyInfo width = type.GetProperty("refResolutionX");
            System.Reflection.PropertyInfo height = type.GetProperty("refResolutionY");
            if (width == null || height == null) return;
            if (pixelPerfectOwner != pixelPerfect)
            {
                pixelPerfectOwner = pixelPerfect;
                baseResolutionX = (int)width.GetValue(pixelPerfect, null);
                baseResolutionY = (int)height.GetValue(pixelPerfect, null);
            }
            width.SetValue(pixelPerfect, Mathf.Max(1, Mathf.RoundToInt(baseResolutionX * zoom)), null);
            height.SetValue(pixelPerfect, Mathf.Max(1, Mathf.RoundToInt(baseResolutionY * zoom)), null);
        }
    }
}
