using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class MinimapCameraSetup : MonoBehaviour
{
    void OnEnable()
    {
        Camera cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 60f;
    }
}
