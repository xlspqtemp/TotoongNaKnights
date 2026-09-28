using UnityEngine;

public class CameraScript_Zoom : MonoBehaviour
{
    private float _currentZoom;
    private Camera _camera;

    public float zoomSpeed = 1000;
    public float zoomSmoothness = 10;

    public float minZoom = 2;
    public float maxZoom = 50;

    private void Awake()
    {
        _camera = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        _currentZoom = Mathf.Clamp(_currentZoom - Input.mouseScrollDelta.y * zoomSpeed * Time.deltaTime, minZoom, maxZoom);
        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _currentZoom, zoomSmoothness * Time.deltaTime);
    }
}
