using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // Required for detecting clicks and touches on UI elements
using UnityEngine.UI; // Required for using RawImage

public class ImageClickHandler : MonoBehaviour, IPointerClickHandler
{
    public RawImage sourceRawImage; // Assign in inspector, the source RawImage you click or tap on
    public RawImage targetRawImage; // Assign in inspector, the target RawImage to copy the texture to

    public static RawImage currentSource;

    // React to click or tap on the RawImage
    public void OnPointerClick(PointerEventData eventData)
    {
        CopyContents();
    }

    void CopyContents()
    {
        // Check if the sourceRawImage has a texture
        if (sourceRawImage.texture != null)
        {
            // Copy the texture from sourceRawImage to targetRawImage
            targetRawImage.texture = sourceRawImage.texture;
            targetRawImage.color = Color.white;
            targetRawImage.gameObject.SetActive(true);
        }
        else
        {
            // If the sourceRawImage texture is null, set the targetRawImage texture to null
            targetRawImage.texture = null;
            targetRawImage.color = new Color(0, 0, 0, 0);
            targetRawImage.gameObject.SetActive(false);
        }

        currentSource = sourceRawImage;
    }

    void Update()
    {
        // For mobile touches
        if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began)
        {
            // Check if touch is over the UI element
            if (IsPointerOverUIObject(Input.touches[0].position))
            {
                OnPointerClick(null); // Since we are using touch, eventData can be null
            }
        }

        if (currentSource == sourceRawImage)
        {
            CopyContents();
        }
    }


// Helper method to check if the touch is over a UI object
    private bool IsPointerOverUIObject(Vector2 touchPos)
    {
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        eventDataCurrentPosition.position = new Vector2(touchPos.x, touchPos.y);
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
        return results.Count > 0;
    }
}