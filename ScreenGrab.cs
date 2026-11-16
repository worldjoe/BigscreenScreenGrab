using System;
using Il2CppBigscreen;
using Il2CppBigscreen.Hands;
using Il2CppBigscreen.UI;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(BigscreenScreenGrab.ScreenGrab), "Bigscreen Screen Grab", "0.12.0", "Copilot")]
[assembly: MelonGame("Bigscreen Inc.", "Bigscreen")]

namespace BigscreenScreenGrab
{
    public sealed class ScreenGrab : MelonMod
    {
        private const float MaxRayDistance = 100f;
        private const float ResizeSpeed = 0.8f;
        private const float JoystickDeadzone = 0.2f;
        private const string PreferenceKey = "BigscreenScreenGrab.Enabled";
        private const string CamPreferenceKey = "BigscreenScreenGrab.Cam2Follow";
        private readonly Vector3[] corners = new Vector3[4];
        private Transform? section;
        private Transform? display;
        private Mesh? mesh;
        private bool leftHeld;
        private bool rightHeld;
        private int grabbingHand = -1;
        private GameObject? teleportObject;
        private bool restoreTeleport;
        private int teleportHand = -1;
        private Vector3 startHand;
        private Vector3 startPosition;
        private Vector3 baseScale;
        private float scaleFactor = 1f;
        private Vector3 startCenter;
        private Quaternion startRotation;
        private Vector3 startFacing;
        private Quaternion startScreenBasis;
        private Vector3 screenUpLocal;
        private Transform? head;
        private float grabRayDistance;
        private float nextSearch;
        private float nextButtonSearch;
        private BigUIButton? recenterButton;
        private UnityAction<Il2CppSystem.Object>? recenterListener;
        private bool recenterPending;
        private int recenterFrame;
        private bool enabledSetting;
        private Toggle? menuToggle;
        private UnityAction<bool>? menuListener;
        private Toggle? camMenuToggle;
        private UnityAction<bool>? camMenuListener;
        private Cam2Follow? camFollow;
        private bool camEnabled;
        private float nextMenuSearch;
        private bool hasOriginalTransform;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;

        public override void OnInitializeMelon()
        {
            enabledSetting = !PlayerPrefs.HasKey(PreferenceKey) || PlayerPrefs.GetInt(PreferenceKey) == 1;
            camEnabled = !PlayerPrefs.HasKey(CamPreferenceKey) || PlayerPrefs.GetInt(CamPreferenceKey) == 1;
            camFollow = new Cam2Follow(LoggerInstance);
            camFollow.SetEnabled(camEnabled);
            LoggerInstance.Msg("Screen Grab loaded. Point and grip the screen to move it; use the same controller's joystick to resize.");
        }

        public override void OnUpdate()
        {
            camFollow?.Update();
            RestoreTeleportWhenNeutral();
            if (Time.unscaledTime >= nextMenuSearch)
            {
                nextMenuSearch = Time.unscaledTime + 2f;
                EnsureMenu();
            }
            if (!enabledSetting)
                return;
            if (Time.unscaledTime >= nextButtonSearch)
            {
                nextButtonSearch = Time.unscaledTime + 2f;
                FindRecenterButton();
            }
            if (recenterPending && Time.frameCount > recenterFrame)
            {
                recenterPending = false;
                RecenterScreen();
            }
            if (section == null || display == null || mesh == null)
            {
                EndGrab();
                if (Time.unscaledTime >= nextSearch)
                {
                    nextSearch = Time.unscaledTime + 2f;
                    FindScreen();
                }
                return;
            }

            var hands = BIG_STATIC_SINGLETONS.bigPairOfHands;
            var input = BIG_STATIC_SINGLETONS.bigHandInput;
            if (hands == null || input == null || hands.Left == null || hands.Right == null)
            {
                EndGrab();
                return;
            }

            HandPointer? leftPointer = hands.Left.handPointer;
            HandPointer? rightPointer = hands.Right.handPointer;
            Transform? left = leftPointer != null ? leftPointer.pointerTransform : null;
            Transform? right = rightPointer != null ? rightPointer.pointerTransform : null;
            if (leftPointer == null || rightPointer == null || left == null || right == null)
            {
                EndGrab();
                return;
            }
            bool leftGrip = input.ContinueGrabbing((Handedness)0);
            bool rightGrip = input.ContinueGrabbing((Handedness)1);
            UpdateCorners();

            if (grabbingHand >= 0)
            {
                bool held = grabbingHand == 0 ? leftGrip : rightGrip;
                if (!held)
                    EndGrab();
                else
                {
                    Handedness hand = (Handedness)grabbingHand;
                    float vertical = input.GetThumbstick(hand, false).y;
                    UpdateGrab(grabbingHand == 0 ? left : right, vertical);
                }
            }
            else
            {
                if (leftGrip && !leftHeld)
                    TryGrab(leftPointer, 0);
                if (grabbingHand < 0 && rightGrip && !rightHeld)
                    TryGrab(rightPointer, 1);
            }

            leftHeld = leftGrip;
            rightHeld = rightGrip;
        }

        public override void OnLateUpdate()
        {
            camFollow?.LateUpdate();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            camFollow?.ReleaseCamera();
            RestoreTeleport();
            recenterPending = false;
            RemoveRecenterListener();
            nextButtonSearch = 0f;
            ForgetScreen();
        }

        public override void OnDeinitializeMelon()
        {
            camFollow?.ReleaseCamera();
            RestoreTeleport();
            RemoveRecenterListener();
            RemoveMenu();
            RestoreOriginalTransform();
            ForgetScreen();
        }

        private void EnsureMenu()
        {
            if (menuToggle != null && camMenuToggle != null)
                return;
            GameObject canvas = GameObject.Find("ModCanvas");
            Transform? content = canvas != null ? canvas.transform.Find("DropdownPanel/Content") : null;
            if (content == null || content.childCount == 0)
                return;
            Toggle? template = null;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (child.name == "Screen Grab" || child.name == "Passthrough" || child.name == "Cam 2 follow")
                    continue;
                Toggle candidate = child.GetComponent<Toggle>();
                if (candidate != null && child.Find("Label") != null)
                {
                    template = candidate;
                    break;
                }
            }
            if (template == null)
            {
                LoggerInstance.Error("Cannot add Screen Grab toggle: Mod Selection template is missing its toggle or label.");
                nextMenuSearch = Time.unscaledTime + 30f;
                return;
            }
            if (menuToggle == null)
                menuToggle = CreateToggle(template, content, "Screen Grab", enabledSetting, ref menuListener, SetEnabled);
            if (camMenuToggle == null)
                camMenuToggle = CreateToggle(template, content, "Cam 2 follow", camEnabled, ref camMenuListener, SetCamEnabled);
        }

        private Toggle? CreateToggle(Toggle template, Transform content, string name, bool value, ref UnityAction<bool>? listener, Action<bool> onChanged)
        {
            GameObject item = UnityEngine.Object.Instantiate(template.gameObject, content);
            item.name = name;
            item.transform.SetAsFirstSibling();
            Toggle toggle = item.GetComponent<Toggle>();
            Text label = item.transform.Find("Label").GetComponent<Text>();
            if (toggle == null || label == null)
            {
                LoggerInstance.Error($"Cannot add {name} toggle: cloned template is incomplete.");
                UnityEngine.Object.Destroy(item);
                nextMenuSearch = Time.unscaledTime + 30f;
                return null;
            }
            label.text = name;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.SetIsOnWithoutNotify(value);
            LayoutElement layout = item.GetComponent<LayoutElement>();
            if (layout == null)
                layout = item.AddComponent<LayoutElement>();
            layout.minHeight = 30f;
            layout.preferredHeight = 30f;
            RectTransform panel = content.parent.GetComponent<RectTransform>();
            VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>();
            if (panel != null && group != null)
            {
                float height = group.padding.top + group.padding.bottom +
                    content.childCount * 30f + Mathf.Max(0, content.childCount - 1) * group.spacing;
                Vector2 size = panel.sizeDelta;
                size.y = Mathf.Max(size.y, height);
                panel.sizeDelta = size;
            }
            listener = (UnityAction<bool>)onChanged;
            toggle.onValueChanged.AddListener(listener);
            LoggerInstance.Msg($"Added {name} toggle to Mod Selection.");
            return toggle;
        }

        private void RemoveMenu()
        {
            if (menuToggle != null)
            {
                if (menuListener != null)
                    menuToggle.onValueChanged.RemoveListener(menuListener);
                UnityEngine.Object.Destroy(menuToggle.gameObject);
            }
            menuToggle = null;
            menuListener = null;
            if (camMenuToggle != null)
            {
                if (camMenuListener != null)
                    camMenuToggle.onValueChanged.RemoveListener(camMenuListener);
                UnityEngine.Object.Destroy(camMenuToggle.gameObject);
            }
            camMenuToggle = null;
            camMenuListener = null;
        }

        private void SetCamEnabled(bool value)
        {
            camEnabled = value;
            PlayerPrefs.SetInt(CamPreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
            camFollow?.SetEnabled(value);
            LoggerInstance.Msg($"Cam 2 follow {(value ? "enabled" : "disabled")}.");
        }

        private void SetEnabled(bool value)
        {
            enabledSetting = value;
            PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (!value)
            {
                recenterPending = false;
                EndGrab();
                RestoreTeleport();
                RestoreOriginalTransform();
                leftHeld = true;
                rightHeld = true;
            }
            LoggerInstance.Msg($"Screen Grab {(value ? "enabled" : "disabled")}.");
        }

        private void CaptureOriginalTransform()
        {
            if (hasOriginalTransform || section == null)
                return;
            originalPosition = section.position;
            originalRotation = section.rotation;
            originalScale = section.localScale;
            baseScale = originalScale;
            scaleFactor = 1f;
            hasOriginalTransform = true;
        }

        private void RestoreOriginalTransform()
        {
            if (!hasOriginalTransform)
                return;
            if (section == null)
            {
                LoggerInstance.Warning("Cannot restore screen: original screen no longer exists.");
                hasOriginalTransform = false;
                return;
            }
            section.SetPositionAndRotation(originalPosition, originalRotation);
            section.localScale = originalScale;
            baseScale = originalScale;
            scaleFactor = 1f;
            hasOriginalTransform = false;
            LoggerInstance.Msg("Screen position, rotation, and size restored.");
        }

        private void FindRecenterButton()
        {
            if (recenterButton != null)
                return;
            GameObject buttonObject = GameObject.Find("UI/TabletUI/TranslationContainer/ScalingContainer/Panes/Pane_Bottom/ControlStrip(Clone)/Content/ToolbarRecenter/Btn (6)");
            if (buttonObject == null)
                return;
            BigUIButton button = buttonObject.GetComponent<BigUIButton>();
            if (button == null || button.OnPoked == null)
            {
                LoggerInstance.Error("Recenter button found, but its poke event is unavailable.");
                nextButtonSearch = Time.unscaledTime + 30f;
                return;
            }
            recenterListener = (UnityAction<Il2CppSystem.Object>)new Action<Il2CppSystem.Object>(OnRecenterPoked);
            button.OnPoked.AddListener(recenterListener);
            recenterButton = button;
            LoggerInstance.Msg("Screen recenter attached to Bigscreen's recenter button.");
        }

        private void RemoveRecenterListener()
        {
            if (recenterButton != null && recenterListener != null && recenterButton.OnPoked != null)
                recenterButton.OnPoked.RemoveListener(recenterListener);
            recenterButton = null;
            recenterListener = null;
        }

        private void OnRecenterPoked(Il2CppSystem.Object ignored)
        {
            if (!enabledSetting)
                return;
            recenterPending = true;
            recenterFrame = Time.frameCount;
        }

        private void RecenterScreen()
        {
            if (section == null || display == null || mesh == null)
            {
                LoggerInstance.Warning("Cannot recenter: movie screen is unavailable.");
                return;
            }
            GameObject headCamera = GameObject.Find("SteamVRUser(Clone)/ViewContainer/Head/Camera_Primary");
            if (headCamera == null)
            {
                LoggerInstance.Warning("Cannot recenter: headset camera is unavailable.");
                return;
            }
            Transform headset = headCamera.transform;
            UpdateCorners();
            Vector3 center = display.TransformPoint(mesh.bounds.center);
            float distance = Vector3.Distance(center, headset.position);
            if (distance < 0.01f)
            {
                LoggerInstance.Warning("Cannot recenter: screen is too close to the headset.");
                return;
            }
            Vector3 facing = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]).normalized;
            if (Vector3.Dot(facing, headset.position - center) < 0f)
                facing = -facing;
            Quaternion screenBasis = Quaternion.LookRotation(facing, section.TransformDirection(screenUpLocal));
            Quaternion desiredBasis = Quaternion.LookRotation(-headset.forward, headset.up);
            EndGrab();
            CaptureOriginalTransform();
            section.rotation = desiredBasis * Quaternion.Inverse(screenBasis) * section.rotation;
            Vector3 target = headset.position + headset.forward * distance;
            section.position += target - display.TransformPoint(mesh.bounds.center);
            LoggerInstance.Msg("Movie screen recentered in front of the headset.");
        }

        private void FindScreen()
        {
            GameObject screenRoot = GameObject.Find("PresentationScreen(Clone)");
            Transform? displayTransform = screenRoot != null ? screenRoot.transform.Find("Section/Display_Main") : null;
            if (screenRoot == null || displayTransform == null)
                return;
            MeshFilter filter = displayTransform.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || screenRoot.transform.parent != null)
            {
                LoggerInstance.Warning("Movie display has no usable mesh or screen root is not top-level; screen grab is unavailable.");
                nextSearch = Time.unscaledTime + 30f;
                return;
            }
            display = displayTransform;
            section = screenRoot.transform;
            mesh = filter.sharedMesh;
            baseScale = section.localScale;
            scaleFactor = 1f;
            UpdateCorners();
            Vector3 edge1 = (corners[1] - corners[0]).normalized;
            Vector3 edge2 = (corners[2] - corners[0]).normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(edge1, Vector3.up)) > Mathf.Abs(Vector3.Dot(edge2, Vector3.up)) ? edge1 : edge2;
            if (Vector3.Dot(up, Vector3.up) < 0f)
                up = -up;
            screenUpLocal = section.InverseTransformDirection(up);
            LoggerInstance.Msg("Movie screen found. Moving its root keeps the interaction collider aligned.");
        }

        private void UpdateCorners()
        {
            if (display == null || mesh == null)
                return;
            Bounds bounds = mesh.bounds;
            Vector3 size = bounds.size;
            int normal = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int a = normal == 0 ? 1 : 0;
            int b = normal == 2 ? 1 : 2;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = bounds.center;
                p[a] += bounds.extents[a] * ((i & 1) == 0 ? -1f : 1f);
                p[b] += bounds.extents[b] * ((i & 2) == 0 ? -1f : 1f);
                corners[i] = display.TransformPoint(p);
            }
        }

        private void TryGrab(HandPointer handPointer, int which)
        {
            if (section == null || display == null || mesh == null || teleportHand >= 0)
                return;
            Collider target = handPointer.LastHit.collider;
            if (!handPointer.IsActive || target == null || !target.transform.IsChildOf(section))
                return;
            Transform pointer = handPointer.pointerTransform;
            if (!TryPointAtScreen(pointer, out _, out float rayDistance))
                return;
            GameObject headCamera = GameObject.Find("SteamVRUser(Clone)/ViewContainer/Head/Camera_Primary");
            if (headCamera == null)
            {
                LoggerInstance.Warning("Cannot move screen: headset camera not found.");
                return;
            }
            var seatSelection = BIG_STATIC_SINGLETONS.bigSeatSelectionUI;
            if (seatSelection == null)
            {
                LoggerInstance.Warning("Cannot grab screen: teleport controls are unavailable.");
                return;
            }
            teleportObject = seatSelection.gameObject;
            restoreTeleport = teleportObject.activeSelf;
            teleportHand = which;
            if (restoreTeleport)
                teleportObject.SetActive(false);
            CaptureOriginalTransform();
            grabbingHand = which;
            grabRayDistance = rayDistance;
            startHand = pointer.position + pointer.forward * rayDistance;
            startPosition = section.position;
            startRotation = section.rotation;
            head = headCamera.transform;
            startFacing = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]).normalized;
            startCenter = display.TransformPoint(mesh.bounds.center);
            if (Vector3.Dot(startFacing, head.position - startCenter) < 0f)
                startFacing = -startFacing;
            Vector3 screenUp = section.TransformDirection(screenUpLocal);
            startScreenBasis = Quaternion.LookRotation(startFacing, screenUp);
        }

        private bool TryPointAtScreen(Transform pointer, out Vector3 hit, out float distance)
        {
            hit = Vector3.zero;
            distance = 0f;
            Vector3 origin = corners[0];
            Vector3 x = corners[1] - origin;
            Vector3 y = corners[2] - origin;
            if (x.sqrMagnitude < 0.0001f || y.sqrMagnitude < 0.0001f)
                return false;
            Vector3 normal = Vector3.Cross(x, y).normalized;
            float denominator = Vector3.Dot(normal, pointer.forward);
            if (Mathf.Abs(denominator) < 0.0001f)
                return false;
            distance = Vector3.Dot(normal, origin - pointer.position) / denominator;
            if (distance <= 0f || distance > MaxRayDistance)
                return false;
            hit = pointer.position + pointer.forward * distance;
            float u = Vector3.Dot(hit - origin, x) / x.sqrMagnitude;
            float v = Vector3.Dot(hit - origin, y) / y.sqrMagnitude;
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        private void UpdateGrab(Transform pointer, float vertical)
        {
            if (section == null || display == null || mesh == null)
                return;
            Vector3 delta = pointer.position + pointer.forward * grabRayDistance - startHand;
            section.position = startPosition + delta;
            if (head == null)
            {
                LoggerInstance.Warning("Headset camera lost; ending screen grab.");
                EndGrab();
                return;
            }
            Vector3 center = startCenter + delta;
            Vector3 toHead = head.position - center;
            if (toHead.sqrMagnitude > 0.0001f)
            {
                Vector3 facing = toHead.normalized;
                Vector3 up = Vector3.ProjectOnPlane(head.up, facing);
                if (up.sqrMagnitude < 0.0001f)
                    up = Vector3.ProjectOnPlane(head.forward, facing);
                if (up.sqrMagnitude > 0.0001f)
                    section.rotation = Quaternion.LookRotation(facing, up.normalized) * Quaternion.Inverse(startScreenBasis) * startRotation;
            }
            if (Mathf.Abs(vertical) > JoystickDeadzone)
            {
                float input = (Mathf.Abs(vertical) - JoystickDeadzone) / (1f - JoystickDeadzone) * Mathf.Sign(vertical);
                scaleFactor = Mathf.Clamp(scaleFactor * Mathf.Exp(input * ResizeSpeed * Time.unscaledDeltaTime), 0.25f, 4f);
                section.localScale = baseScale * scaleFactor;
            }
            section.position += center - display.TransformPoint(mesh.bounds.center);
        }

        private void EndGrab()
        {
            grabbingHand = -1;
            head = null;
        }

        private void RestoreTeleportWhenNeutral()
        {
            if (teleportHand < 0 || grabbingHand >= 0)
                return;
            var input = BIG_STATIC_SINGLETONS.bigHandInput;
            if (input != null && input.GetThumbstick((Handedness)teleportHand, false).magnitude <= JoystickDeadzone)
                RestoreTeleport();
        }

        private void RestoreTeleport()
        {
            if (teleportObject != null && restoreTeleport)
                teleportObject.SetActive(true);
            teleportObject = null;
            restoreTeleport = false;
            teleportHand = -1;
        }

        private void ForgetScreen()
        {
            EndGrab();
            section = null;
            display = null;
            mesh = null;
            leftHeld = false;
            rightHeld = false;
            nextSearch = 0f;
            hasOriginalTransform = false;
        }
    }
}
