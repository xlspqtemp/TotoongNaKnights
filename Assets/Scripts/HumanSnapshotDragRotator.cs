using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Orbits the Human Snapshot camera rig from horizontal pointer drags over its RawImage.</summary>
[RequireComponent(typeof(RawImage))]
public sealed class HumanSnapshotDragRotator : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Transform orbitRig;
    [SerializeField, Min(0.01f)] private float orbitSensitivity = 0.35f;
    [SerializeField, Min(0.01f)] private float smoothing = 14f;

    private float targetYaw;
    private float currentYaw;

    private void Awake()
    {
        if (orbitRig == null)
        {
            GameObject cameraRigObject = GameObject.Find("HumanSnapshotCameraRig");
            if (cameraRigObject != null)
                orbitRig = cameraRigObject.transform;
        }

        if (orbitRig != null)
            targetYaw = currentYaw = orbitRig.localEulerAngles.y;
    }

    /// <summary>Starts a drag gesture on the snapshot RawImage.</summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    /// <summary>Orbits the camera horizontally, preserving the current vertical angle.</summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (orbitRig == null || eventData == null)
            return;

        targetYaw = Mathf.Repeat(targetYaw - eventData.delta.x * orbitSensitivity, 360f);
    }

    /// <summary>Ends the drag gesture without changing the orbit angle.</summary>
    public void OnEndDrag(PointerEventData eventData)
    {
    }

    private void Update()
    {
        if (orbitRig == null)
            return;

        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        currentYaw = Mathf.Repeat(currentYaw + Mathf.DeltaAngle(currentYaw, targetYaw) * blend, 360f);
        orbitRig.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
    }
}
