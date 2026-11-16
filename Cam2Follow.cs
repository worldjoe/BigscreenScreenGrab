using System;
using Il2CppBigscreen;
using Il2CppBigscreen.UI;
using MelonLoader;
using UnityEngine;

namespace BigscreenScreenGrab
{
    internal sealed class Cam2Follow
    {
        private readonly MelonLogger.Instance logger;
        private Camera? camera;
        private Il2Cpp.StreamerCamera? streamerCamera;
        private bool cameraVisualWasActive;
        private Camera.CameraCallback? callback;
        private Transform? display;
        private Renderer? screenRenderer;
        private Mesh? mesh;
        private int oldMask;
        private CameraClearFlags oldClearFlags;
        private Color oldBackground;
        private Vector3 oldPosition;
        private Quaternion oldRotation;
        private bool conflictReported;
        private bool framingValid;
        private bool enabled;
        private float nextSearch;
        private Vector3 screenUpLocal;
        private StreamerCamPage? activatedPage;
        private bool activationPending;
        private float activationDeadline;
        private bool activationWarning;

        internal Cam2Follow(MelonLogger.Instance logger)
        {
            this.logger = logger;
        }

        internal void SetEnabled(bool value)
        {
            enabled = value;
            if (!value)
            {
                ReleaseCamera();
                if (activatedPage != null && activatedPage.CameraActive)
                {
                    activatedPage.ConfigureStreamerCamera(false);
                    logger.Msg("Cam 2 turned off; it was started by Cam 2 follow.");
                }
                activatedPage = null;
                activationPending = false;
                activationWarning = false;
            }
            else
                nextSearch = 0f;
        }

        internal void Update()
        {
            if (!enabled)
                return;
            if (camera == null)
            {
                if (Time.unscaledTime < nextSearch)
                    return;
                nextSearch = Time.unscaledTime + 2f;
                FindCamera();
                if (camera == null)
                    EnsureCameraEnabled();
            }
        }

        private void EnsureCameraEnabled()
        {
            if (activationPending)
            {
                if (Time.unscaledTime < activationDeadline)
                    return;
                activationPending = false;
                if (!activationWarning)
                {
                    logger.Warning("Cam 2 did not become available after activation; use Bigscreen's camera controls to check it.");
                    activationWarning = true;
                }
                nextSearch = Time.unscaledTime + 30f;
                return;
            }
            if (GameObject.Find("PresentationScreen(Clone)") == null)
                return;
            TabletUI tablet = BIG_STATIC_SINGLETONS.bigTabletUI;
            if (tablet == null)
                return;
            BigUIPage page = tablet.GetPage(BigUIState.StreamerCamPage);
            StreamerCamPage? cameraPage = page != null ? page.GetComponent<StreamerCamPage>() : null;
            if (cameraPage == null)
            {
                if (!activationWarning)
                {
                    logger.Warning("Cam 2 controls are not loaded yet; open Bigscreen's streamer camera page.");
                    activationWarning = true;
                }
                nextSearch = Time.unscaledTime + 30f;
                return;
            }
            if (cameraPage.CameraActive)
            {
                if (!activationWarning)
                {
                    logger.Warning("Cam 2 reports active but its camera is unavailable; check Bigscreen's camera controls.");
                    activationWarning = true;
                }
                nextSearch = Time.unscaledTime + 30f;
                return;
            }
            EnsureRenderGuard();
            cameraPage.ConfigureStreamerCamera(true);
            activatedPage = cameraPage;
            activationPending = true;
            activationDeadline = Time.unscaledTime + 10f;
            activationWarning = false;
            logger.Msg("Requested Cam 2 activation for Cam 2 follow.");
            FindCamera();
        }

        internal void LateUpdate()
        {
            if (!enabled || camera == null)
                return;
            framingValid = false;
            FindScreen();
            if (display == null || mesh == null || screenRenderer == null)
            {
                camera.cullingMask = 0;
                return;
            }
            Bounds bounds = mesh.bounds;
            Vector3 size = bounds.size;
            int normalAxis = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int a = normalAxis == 0 ? 1 : 0;
            int b = normalAxis == 2 ? 1 : 2;
            Vector3 pa = bounds.center;
            Vector3 pb = bounds.center;
            pa[a] += bounds.extents[a];
            pb[b] += bounds.extents[b];
            Vector3 center = display.TransformPoint(bounds.center);
            Vector3 edgeA = display.TransformPoint(pa) - center;
            Vector3 edgeB = display.TransformPoint(pb) - center;
            Vector3 normal = Vector3.Cross(edgeA, edgeB).normalized;
            if (normal.sqrMagnitude < 0.0001f || camera.aspect <= 0f || camera.orthographic)
            {
                camera.cullingMask = 0;
                if (!conflictReported)
                {
                    logger.Error("Cam 2 follow cannot frame this screen or camera; output set to black.");
                    conflictReported = true;
                }
                return;
            }
            if (Vector3.Dot(normal, camera.transform.position - center) < 0f)
                normal = -normal;
            Vector3 referenceUp = display.TransformDirection(screenUpLocal);
            bool aIsUp = Mathf.Abs(Vector3.Dot(edgeA.normalized, referenceUp)) >
                Mathf.Abs(Vector3.Dot(edgeB.normalized, referenceUp));
            Vector3 up = aIsUp ? edgeA : edgeB;
            if (Vector3.Dot(up, referenceUp) < 0f)
                up = -up;
            float halfHeight = up.magnitude;
            float halfWidth = aIsUp ? edgeB.magnitude : edgeA.magnitude;
            float tangent = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            if (tangent <= 0.0001f)
            {
                camera.cullingMask = 0;
                logger.Error("Cam 2 field of view is invalid; output set to black.");
                return;
            }
            float distance = Mathf.Max(halfHeight / tangent, halfWidth / (tangent * camera.aspect)) * 1.02f;
            if (distance <= camera.nearClipPlane || distance >= camera.farClipPlane)
            {
                camera.cullingMask = 0;
                if (!conflictReported)
                {
                    logger.Error("Cam 2 screen exceeds camera clipping range; output set to black.");
                    conflictReported = true;
                }
                return;
            }
            camera.transform.SetPositionAndRotation(center + normal * distance, Quaternion.LookRotation(-normal, up.normalized));
            framingValid = true;
        }

        private void FindCamera()
        {
            GameObject obj = GameObject.Find("StreamerCamera(Clone)");
            if (obj == null)
                return;
            Il2Cpp.StreamerCamera stream = obj.GetComponent<Il2Cpp.StreamerCamera>();
            if (stream == null || stream.viewCamera == null || !stream.viewCamera.enabled)
                return;
            camera = stream.viewCamera;
            activationPending = false;
            activationWarning = false;
            if (stream.cameraVisual == null)
            {
                logger.Warning("Cam 2 follow cannot hide the camera: its visual is unavailable.");
                camera = null;
                return;
            }
            streamerCamera = stream;
            cameraVisualWasActive = stream.cameraVisual.activeSelf;
            stream.HideCamera(true);
            oldMask = camera.cullingMask;
            oldClearFlags = camera.clearFlags;
            oldBackground = camera.backgroundColor;
            oldPosition = camera.transform.position;
            oldRotation = camera.transform.rotation;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            EnsureRenderGuard();
            logger.Msg("Cam 2 follow active; requested camera visual hidden. Other-player visibility requires verification.");
        }

        private void EnsureRenderGuard()
        {
            if (callback != null)
                return;
            callback = (Camera.CameraCallback)new Action<Camera>(OnPreCull);
            Camera.onPreCull = Camera.onPreCull + callback;
        }

        private void FindScreen()
        {
            GameObject root = GameObject.Find("PresentationScreen(Clone)");
            Transform? found = root != null ? root.transform.Find("Section/Display_Main") : null;
            if (found == display && display != null && mesh != null && screenRenderer != null)
                return;
            display = found;
            mesh = null;
            screenRenderer = null;
            if (display == null)
                return;
            MeshFilter filter = display.GetComponent<MeshFilter>();
            Renderer renderer = display.GetComponent<Renderer>();
            if (filter != null && filter.sharedMesh != null && renderer != null)
            {
                mesh = filter.sharedMesh;
                screenRenderer = renderer;
                Bounds bounds = mesh.bounds;
                Vector3 size = bounds.size;
                int normalAxis = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
                int a = normalAxis == 0 ? 1 : 0;
                int b = normalAxis == 2 ? 1 : 2;
                Vector3 localA = Vector3.zero;
                Vector3 localB = Vector3.zero;
                localA[a] = 1f;
                localB[b] = 1f;
                Vector3 worldA = display.TransformDirection(localA);
                Vector3 worldB = display.TransformDirection(localB);
                screenUpLocal = Mathf.Abs(Vector3.Dot(worldA, Vector3.up)) > Mathf.Abs(Vector3.Dot(worldB, Vector3.up)) ? localA : localB;
                if (Vector3.Dot(display.TransformDirection(screenUpLocal), Vector3.up) < 0f)
                    screenUpLocal = -screenUpLocal;
            }
        }

        private void OnPreCull(Camera rendering)
        {
            if (enabled && camera == null && rendering.gameObject.name == "StreamerCamera(Clone)")
            {
                FindCamera();
                if (camera == null)
                {
                    rendering.cullingMask = 0;
                    rendering.clearFlags = CameraClearFlags.SolidColor;
                    rendering.backgroundColor = Color.black;
                }
            }
            if (camera == null || rendering != camera)
                return;
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            if (!enabled || !framingValid || display == null || screenRenderer == null || mesh == null || !screenRenderer.enabled)
                return;
            int layer = display.gameObject.layer;
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (renderer != screenRenderer && renderer.enabled && renderer.gameObject.layer == layer)
                {
                    if (!conflictReported)
                    {
                        logger.Warning("Cam 2 output black: another visible renderer shares the screen layer.");
                        conflictReported = true;
                    }
                    return;
                }
            }
            if (conflictReported)
                logger.Msg("Cam 2 screen layer is isolated again.");
            conflictReported = false;
            camera.cullingMask = 1 << layer;
        }

        internal void ReleaseCamera()
        {
            if (callback != null)
                Camera.onPreCull = Camera.onPreCull - callback;
            callback = null;
            if (camera != null)
            {
                camera.cullingMask = oldMask;
                camera.clearFlags = oldClearFlags;
                camera.backgroundColor = oldBackground;
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
            }
            if (streamerCamera != null)
                streamerCamera.HideCamera(!cameraVisualWasActive);
            streamerCamera = null;
            camera = null;
            display = null;
            screenRenderer = null;
            mesh = null;
            conflictReported = false;
            framingValid = false;
        }
    }
}
