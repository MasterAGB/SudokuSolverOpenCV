using UnityEngine;
using UnityEngine.UI; // For UI elements
using System.Collections; // For IEnumerator
using System;
using System.Collections.Generic;
using OpenCvSharp; // For Action

public class CustomOCREngine : MonoBehaviour
{
    // Assume these are set up and linked in the Unity Editor
    public GameObject popup;
    public RawImage rawImageOrig;
    public RawImage rawImage;
    public RawImage rawImageProcessed;
    public Text instructionText;
    public InputField inputField;
    public Button confirmButton;
    public Button closeButton;
    public List<Button> numberButtons;


    public ImageProcessing imageProcessing;
    public NumberRecognizer numberRecognizer;
    private Action<string> onNumberConfirmed;


    public bool updateDB = false;

    public int minPixelsForTrain = 8;
    // Example method to simulate OCR and possibly ask for user input
    public IEnumerator RecognizeAsync(Mat originalImage, Mat wrappedImage, Mat wrappedOptimizedImage, Action<string> onRecognized)
    {
        
        if (Cv2.CountNonZero(wrappedOptimizedImage)  < minPixelsForTrain)
        {
            Debug.Log("Empty");
            onRecognized("");
            yield break;
        }


        (string recognizedNumber, float confidence, int potentialNumber) =
            numberRecognizer.RecognizeNumber(wrappedOptimizedImage);


        if (recognizedNumber == null)
        {
            SudokuImageReader.DisplayResultTexture(originalImage, "Cell before processing", rawImageOrig);
            SudokuImageReader.DisplayResultTexture(wrappedImage, "Cell before processing", rawImage);
            SudokuImageReader.DisplayResultTexture(wrappedOptimizedImage, "Cell after processing", rawImageProcessed);

            Debug.LogError("Recognized number was null, but potentially its " + potentialNumber + " with confidence " +
                           confidence);

            numberConfirmed = false;
            // Display UI to ask for user input
            popup.gameObject.SetActive(true);

            inputField.text = potentialNumber.ToString();
            instructionText.text = $"Please enter the digit shown in the image: Confidence {confidence:0.00}";

            onNumberConfirmed = (inputNumber) =>
            {
                if (!string.IsNullOrEmpty(inputNumber) && inputNumber!="0" && inputNumber!="-1")
                {
                    numberRecognizer.UpdateDB(wrappedOptimizedImage, inputNumber);
                    Debug.Log($"Added as new variant for {inputNumber}.");


                    // Return or do something with the recognized number
                    // OCR result is confident, no need for user input
                    onRecognized(inputNumber);
                }

                // Hide UI elements after confirmation
                popup.gameObject.SetActive(false);
            };

            //SudokuImageReader.DisplayResultTexture(optimizedProcessedImage, "Please fill for this number");
            Debug.LogWarning("Nothing was recognized for number, Wautubg for input");
            // Wait for the user to input a number and press confirm
            yield return new WaitUntil(() => numberConfirmed);
            Debug.LogWarning("Wait was over...");

            // Reset the callback to ensure it's called only once
            onNumberConfirmed = null;
        }
        else
        {
            Debug.LogWarning($"Recognized digit: {recognizedNumber} with confidence: {confidence:0.00}");
            if (updateDB)
            {
                if (confidence < 1)
                {
                    numberRecognizer.UpdateDB(wrappedOptimizedImage, recognizedNumber);
                    // Return or do something with the recognized number
                    // OCR result is confident, no need for user input
                }
            }

            onRecognized(recognizedNumber);
        }
    }

    // Example method to simulate OCR and possibly ask for user input
    public void Recognize(Mat originalImage, Mat wrapImage, Mat optimizedProcessedImage, Action<string> onRecognized)
    {

        if (Cv2.CountNonZero(optimizedProcessedImage) < minPixelsForTrain)
        {
            Debug.Log("Empty");
            onRecognized("");
            return;
        }
        
        (string recognizedNumber, float confidence, int potentialNumber) =
            numberRecognizer.RecognizeNumber(optimizedProcessedImage);


        if (recognizedNumber == null)
        {
            //Debug.LogError("Recognized number was null, but potentially its "+potentialNumber+" with confidence "+confidence);
        }
        else
        {
            //Debug.LogWarning($"Recognized digit: {recognizedNumber} with confidence: {confidence:0.00}");
            if (updateDB)
            {
                if (confidence < 1)
                {
                    numberRecognizer.UpdateDB(optimizedProcessedImage, recognizedNumber);
                }
            }

            onRecognized(recognizedNumber);
        }
    }

    void Start()
    {
        //assing actions on UI button
        confirmButton.onClick.AddListener(ConfirmInput);
        closeButton.onClick.AddListener(CancelInput);
        foreach (Button numberButton in numberButtons)
        {
            numberButton.onClick.AddListener(() => ConfirmInputButton(numberButton));

        }
    }

    private bool numberConfirmed = false;


    public void ConfirmInputButton(Button pressedButton)
    {
        Text buttonText = pressedButton.GetComponentInChildren<Text>();

        if (onNumberConfirmed != null && buttonText != null)
        {
            numberConfirmed = true;
            onNumberConfirmed.Invoke(buttonText.text);
            inputField.text = ""; // Reset input field
        }
    }

    // This method should be called by your Confirm button in the UI
    public void ConfirmInput()
    {
        if (onNumberConfirmed != null)
        {
            numberConfirmed = true;
            onNumberConfirmed.Invoke(inputField.text);
            inputField.text = ""; // Reset input field
        }
    }   
    
    public void CancelInput()
    {
        if (onNumberConfirmed != null)
        {
            numberConfirmed = true;
            onNumberConfirmed.Invoke(null);
            inputField.text = ""; // Reset input field
        }
    }
}