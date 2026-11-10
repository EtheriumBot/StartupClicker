using StartupClicker.Core;
using UnityEngine;

namespace StartupClicker.Game
{
    /// <summary>Pans and zooms the orthographic camera, keeps it over the map, and holds the backdrop.</summary>
    public sealed class CameraController : MonoBehaviour
    {
        public const float MinZoom = 1.8f;
        public const float MaxZoom = 9.5f;

        Camera cam;
        float targetSize;
        Transform backdrop;

        public Camera Camera { get { return cam; } }

        public void Init(Camera camera)
        {
            cam = camera;
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.backgroundColor = new Color(15 / 255f, 25 / 255f, 35 / 255f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0.35f, -10f);
            targetSize = cam.orthographicSize;

            var bg = new GameObject("Backdrop");
            bg.transform.SetParent(cam.transform, false);
            bg.transform.localPosition = new Vector3(0, 0, 20f);
            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteBank.Centered("bg");
            sr.sortingOrder = -32000;
            backdrop = bg.transform;
        }

        public Vector3 ScreenToWorld(Vector2 screen)
        {
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            w.z = 0f;
            return w;
        }

        public Vector2 WorldToScreen(Vector3 world)
        {
            return cam.WorldToScreenPoint(world);
        }

        /// <summary>Move the camera so the world appears to follow the cursor by this many screen pixels.</summary>
        public void PanByScreenDelta(Vector2 delta)
        {
            float unitsPerPixel = cam.orthographicSize * 2f / Mathf.Max(1, Screen.height);
            cam.transform.position -= new Vector3(delta.x, delta.y, 0f) * unitsPerPixel;
            Clamp();
        }

        public void PanByWorld(Vector2 delta)
        {
            cam.transform.position += new Vector3(delta.x, delta.y, 0f);
            Clamp();
        }

        /// <summary>steps &gt; 0 zooms in. The actual size eases towards the target in LateUpdate.</summary>
        public void Zoom(float steps)
        {
            targetSize = Mathf.Clamp(targetSize * Mathf.Pow(0.87f, steps), MinZoom, MaxZoom);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, k);
            Clamp();

            // keep the backdrop sprite exactly filling the view (it is 512px at 256 PPU = 2 units)
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            backdrop.localScale = new Vector3(w / 2f, h / 2f, 1f) * 1.02f;
        }

        void Clamp()
        {
            float halfW = Catalog.GridSize * 0.5f;
            float halfH = Catalog.GridSize * 0.25f;
            var p = cam.transform.position;
            p.x = Mathf.Clamp(p.x, -halfW, halfW);
            p.y = Mathf.Clamp(p.y, -halfH - 0.5f, halfH + 1f);
            cam.transform.position = p;
        }
    }
}
