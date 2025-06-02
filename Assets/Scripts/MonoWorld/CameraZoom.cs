using UnityEngine;

public class DelayedSmoothZoomOut : MonoBehaviour
{
    public float targetZoom = 260f;
    public float zoomDuration = 50f;
    public float holdBeforeZoom = 25f;

    private Camera cam;
    private float startZoom;
    private float timer;
    private bool isZooming;

    void Start()
    {
        cam = GetComponent<Camera>();
        startZoom = cam.orthographic ? cam.orthographicSize : cam.fieldOfView;
        timer = 0f;
        isZooming = false;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (!isZooming)
        {
            if (timer >= holdBeforeZoom)
            {
                isZooming = true;
                timer = 0f;
            }
        }
        else
        {
            float t = Mathf.Clamp01(timer / zoomDuration);
            float zoom = Mathf.Lerp(startZoom, targetZoom, t);
            if (cam.orthographic)
                cam.orthographicSize = zoom;
            else
                cam.fieldOfView = zoom;
        }
    }
}
