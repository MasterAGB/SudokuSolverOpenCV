using UnityEngine;

public class InteractiveBox : MonoBehaviour
{
    public float rotateSpeed = 100f;
    public float dragSpeed = 1f;
    public float zoomSpeed = 10f;

    private Camera cam;
    private Vector3 dragOrigin;

    private void Start()
    {
        cam = Camera.main; // Ensure you have a main camera tagged in the scene.
    }

    private void Update()
    {
        // PC Input
        if (Input.GetMouseButton(0)) // Left mouse button for rotation
        {
            float rotX = Input.GetAxis("Mouse X") * rotateSpeed * Mathf.Deg2Rad;
            float rotY = Input.GetAxis("Mouse Y") * rotateSpeed * Mathf.Deg2Rad;

            transform.Rotate(Vector3.up, -rotX, Space.World);
            transform.Rotate(Vector3.right, rotY, Space.World);
        }
        else if (Input.GetMouseButton(1)) // Right mouse button for dragging
        {
            float dragX = Input.GetAxis("Mouse X") * dragSpeed * Time.deltaTime;
            float dragY = Input.GetAxis("Mouse Y") * dragSpeed * Time.deltaTime;
            transform.Translate(-dragX, -dragY, 0, Space.World);
        }

        // Mouse wheel for zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        cam.transform.Translate(0, 0, scroll * zoomSpeed, Space.Self);

        // Mobile Input
        if (Input.touchCount == 1) // Single touch for rotation
        {
            Touch touch = Input.GetTouch(0);
            float rotX = touch.deltaPosition.x * rotateSpeed * Mathf.Deg2Rad * Time.deltaTime;
            float rotY = touch.deltaPosition.y * rotateSpeed * Mathf.Deg2Rad * Time.deltaTime;

            transform.Rotate(Vector3.up, -rotX, Space.World);
            transform.Rotate(Vector3.right, rotY, Space.World);
        }
        else if (Input.touchCount == 2) // Pinch for zoom and two-finger drag for move
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

            // Move
            if (Mathf.Abs(deltaMagnitudeDiff) < 1f) // Assuming small pinch movements count as a drag
            {
                Vector2 avgDeltaPosition = (touchZero.deltaPosition + touchOne.deltaPosition) / 2;
                transform.Translate(-avgDeltaPosition.x * dragSpeed * Time.deltaTime, -avgDeltaPosition.y * dragSpeed * Time.deltaTime, 0, Space.World);
            }

            // Zoom
            cam.transform.Translate(0, 0, deltaMagnitudeDiff * zoomSpeed * Time.deltaTime, Space.Self);
        }
    }
}
