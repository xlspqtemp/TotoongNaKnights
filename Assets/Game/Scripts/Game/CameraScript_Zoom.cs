using UnityEngine;
using UnityEngine.EventSystems;

public class CameraScript_Zoom : MonoBehaviour
{
    private float _currentZoom;
    private Camera _camera;
    private float previousPinchDistance;
    private bool isPinching;
    private bool pinchStartedOverWorld;

    public float zoomSpeed = 1000f;
    public float zoomSmoothness = 10f;
    [Min(0f)] public float pinchZoomSensitivity = 0.5f;

    public float minZoom = 2f;
    public float maxZoom = 50f;

    private void Awake()
    {
        _camera = GetComponentInChildren<Camera>();
        if (_camera != null && _camera.orthographic)
            _currentZoom = Mathf.Clamp(_camera.orthographicSize, minZoom, maxZoom);
        else
            _currentZoom = minZoom;
    }

    private void Update()
    {
        if (_camera == null)
            return;

        bool pinchInputActive = Input.touchCount >= 2;
        if (pinchInputActive)
        {
            UpdatePinchZoom();
        }
        else
        {
            isPinching = false;
            pinchStartedOverWorld = false;
            _currentZoom = Mathf.Clamp(
                _currentZoom - Input.mouseScrollDelta.y * zoomSpeed * Time.unscaledDeltaTime,
                minZoom,
                maxZoom);
        }

        if (_camera.orthographic)
        {
            _camera.orthographicSize = Mathf.Lerp(
                _camera.orthographicSize,
                _currentZoom,
                zoomSmoothness * Time.unscaledDeltaTime);
        }
    }

    private void UpdatePinchZoom()
    {
        Touch firstTouch = Input.GetTouch(0);
        Touch secondTouch = Input.GetTouch(1);
        float currentPinchDistance = Vector2.Distance(firstTouch.position, secondTouch.position);
        bool pinchStartedThisFrame = !isPinching ||
            firstTouch.phase == TouchPhase.Began || secondTouch.phase == TouchPhase.Began;

        if (pinchStartedThisFrame)
        {
            previousPinchDistance = currentPinchDistance;
            pinchStartedOverWorld = !IsTouchOverUI(firstTouch) && !IsTouchOverUI(secondTouch);
            isPinching = true;
            return;
        }

        if (!pinchStartedOverWorld || Screen.height <= 0)
        {
            previousPinchDistance = currentPinchDistance;
            return;
        }

        float pinchDistanceDelta = currentPinchDistance - previousPinchDistance;
        previousPinchDistance = currentPinchDistance;
        float zoomRange = Mathf.Max(0f, maxZoom - minZoom);
        float normalizedPinchDelta = pinchDistanceDelta / Screen.height;
        _currentZoom = Mathf.Clamp(
            _currentZoom - normalizedPinchDelta * zoomRange * pinchZoomSensitivity,
            minZoom,
            maxZoom);
    }

    private static bool IsTouchOverUI(Touch touch)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
    }
}
