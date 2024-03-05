using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CameraControlUI : MonoBehaviour
{
    public SudokuImageReader sudokuImageReader;
    public WebCameraSudoku webCameraSudoku;
    public Toggle flipHorizontalToggle;
    public Toggle fixFisheyeToggle;
    public GameObject buttonPrefab;
    public Transform buttonsContainer;
    public ImageClickHandler emptyTextureRender;
    public ImageClickHandler webcameraTextureRender;

    private List<Button> cameraButtons = new List<Button>();

    void Start()
    {
        GenerateCameraButtons();
        flipHorizontalToggle.onValueChanged.AddListener(OnFlipHorizontalChanged);
        fixFisheyeToggle.onValueChanged.AddListener(OnFisheyeChanged);
    }

    private void GenerateCameraButtons()
    {
        foreach (var btn in cameraButtons)
        {
            Destroy(btn.gameObject);
        }

        cameraButtons.Clear();

        CreateButton("No Camera", () => SetCameraIndex(-1));

        for (int i = 0; i < WebCamTexture.devices.Length; i++)
        {
            int index = i;
            string cameraName = WebCamTexture.devices[i].name;
            CreateButton(cameraName, () => SetCameraIndex(index));
        }
    }

    private void CreateButton(string text, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObj = Instantiate(buttonPrefab, buttonsContainer);
        buttonObj.SetActive(true);
        Button button = buttonObj.GetComponent<Button>();
        Text buttonText = button.GetComponentInChildren<Text>();
        buttonText.text = text;
        button.onClick.AddListener(action);

        cameraButtons.Add(button);
    }

    private void SetCameraIndex(int index)
    {
        if (index < 0)
        {
            sudokuImageReader.useCamera = false;
            webCameraSudoku.enabled = (false);
            emptyTextureRender.OnPointerClick(null);
        }
        else
        {
            webCameraSudoku.cameraIndex = index;
            webCameraSudoku.DeviceName = index >= 0 ? WebCamTexture.devices[index].name : null;
            webCameraSudoku.UpdateCameraIndex();
            webCameraSudoku.enabled = (true);
            sudokuImageReader.useCamera = true;
            webcameraTextureRender.OnPointerClick(null);
        }
    }

    private void OnFlipHorizontalChanged(bool isOn)
    {
        webCameraSudoku.forcedFlipHorizontal = isOn;
        webCameraSudoku.DeviceName = webCameraSudoku.DeviceName;
        webCameraSudoku.UpdateCameraIndex();
    }

    private void OnFisheyeChanged(bool isOn)
    {
        webCameraSudoku.fixFisheye = isOn;
    }
}