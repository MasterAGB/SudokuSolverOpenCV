using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using OpenCvSharp;
using UnityEngine.UI;
using Rect = OpenCvSharp.Rect; // Assuming you have OpenCV for Unity

public class SudokuImageReader : MonoBehaviour
{
    [Header("Components")] public WebCameraSudoku webCameraSudoku;
    public SudokuSolver sudokuSolver;
    public CustomOCREngine customOCREngine;
    public ImageProcessing imageProcessing;
    public NumberRecognizer numberRecognizer;


    [Header("Raw Image Outputs")] public RawImage originalRawImage;
    public RawImage nonWrappedRawImage;
    public RawImage wrappedOriginalRawImage;
    public RawImage wrappedRawImage;
    public RawImage letterPreviewRawImage;
    public RawImage letterRawImage;
    public RawImage letterWrapRawImage;
    public RawImage trimmedLetterRawImage;
    public RawImage annotatedRawImage;
    public RawImage finalRawImage;
    public Text outputText;

    [Header("Blur Settings")]
    [Range(0, 100)] public int blockSize = 41;
    [Range(0, 100)] public double C = 20;
    [Range(0, 100)] public int blurSize = 5;
    [Range(0, 100)] public double sigmaX = 0;

    public int frameSkip = 3;

    [Header("Main Settings")] public bool solve = false;
    public bool useCamera = false;
    public bool RenderOneFrame = true;
    public bool RenderScreenInUpdate = false;


    public void ToggleAccuracy()
    {
        if (numberRecognizer.RecognizeThreshold < 0.8f)
        {
            numberRecognizer.RecognizeThreshold = 0.9f;
            numberRecognizer.VariantThreshold = 0.9f;
        }
        else
        {
            numberRecognizer.RecognizeThreshold = 0.4f;
            numberRecognizer.VariantThreshold = 0.4f;
        }
    }

    public void ToggleCameraOrSample()
    {
        useCamera = !useCamera;
    }

    public void ToggleCameraIndex()
    {
        webCameraSudoku.ToggleCameraIndex();
    }

    public void ToggleSolve()
    {
        solve = !solve;
    }

    public void ToggleTrain()
    {
        customOCREngine.updateDB = !customOCREngine.updateDB;
        if (customOCREngine.updateDB)
        {
            //Setting high accuracy
            numberRecognizer.RecognizeThreshold = 0.9f;
            numberRecognizer.VariantThreshold = 0.9f;
            
            RenderScreenInUpdate = false;
            RenderOneFrame = true;
        }
        else
        {
        }
        //Toggle learn
    }

    public void ResetTrain()
    {
        numberRecognizer.ResetLearn();
    }

    public void ToggleRealtime()
    {
        RenderScreenInUpdate = !RenderScreenInUpdate;
        if (RenderScreenInUpdate)
        {
            RenderOneFrame = false;
            //disable learn
        }
    }

    public void ProcessOneFrame()
    {
        RenderOneFrame = true;
        RenderScreenInUpdate = false;
    }

    public int[,] OptimizeRecognizedDigits(int[,] digits)
    {
        //Debug.Log("Original digits:");
        //PrintDigits(digits);

        int rows = digits.GetLength(0);
        int cols = digits.GetLength(1);
        int[,] optimizedDigits = new int[rows, cols];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                int digit = digits[i, j];
                // If digit is a double number, crop '1' (assuming '1' is an artifact)
                if (digit > 9)
                {
                    string digitStr = digit.ToString().Replace("1", ""); // Remove '1' from the digit
                    digit = (int.TryParse(digitStr, out int result) && result > 0 && result <= 9) ? result : 0;
                }

                // Ensure digit is within valid range, otherwise consider it as an empty cell
                digit = (digit >= 1 && digit <= 9) ? digit : 0;
                optimizedDigits[i, j] = digit;
            }
        }

        //Debug.Log("Optimized digits:");
        //PrintDigits(optimizedDigits);

        return optimizedDigits;
    }


    public Mat PreprocessImageForWrap(Mat originalImage)
    {
        if (blurSize % 2 == 0) blurSize++;
        if (blockSize % 2 == 0) blockSize++;
        Mat gray = imageProcessing.ConvertToGrayscale(originalImage);
        Mat blur = imageProcessing.ApplyGaussianBlur(gray, blurSize, blurSize, sigmaX);
        Mat adaptiveThresh = imageProcessing.ApplyAdaptiveThreshold(blur, 255, ImageProcessing.AdaptiveMethod.Gaussian,
            ImageProcessing.ThresholdType.BinaryInv, blockSize, C);
        return adaptiveThresh;
    }

    private void PrintDigits(int[,] digits)
    {
        string digitsOutput = "";
        for (int i = 0; i < digits.GetLength(0); i++)
        {
            for (int j = 0; j < digits.GetLength(1); j++)
            {
                digitsOutput += digits[i, j] + " ";
            }

            digitsOutput += "\n";
        }

        Debug.Log(digitsOutput);
    }


// Function to calculate Euclidean distance between two Point2f points
    public static double CalculateDistance(Point2f point1, Point2f point2)
    {
        return Math.Sqrt(Math.Pow(point2.X - point1.X, 2) + Math.Pow(point2.Y - point1.Y, 2));
    }

    public (Mat,Mat, Mat) FourPointTransform(Mat imageProcessed, Mat imageOriginal, Point2f[] pts)
    {
        Point2f[] rect = OrderPoints(pts).Select(v => new Point2f(v.X, v.Y)).ToArray();

        double widthA = CalculateDistance(rect[2], rect[3]);
        double widthB = CalculateDistance(rect[1], rect[0]);
        float maxWidth = Mathf.Max((float)widthA, (float)widthB);

        double heightA = CalculateDistance(rect[1], rect[2]);
        double heightB = CalculateDistance(rect[0], rect[3]);
        float maxHeight = Mathf.Max((float)heightA, (float)heightB);

        Point2f[] dst =
        {
            new Point2f(0, 0),
            new Point2f(maxWidth - 1, 0),
            new Point2f(maxWidth - 1, maxHeight - 1),
            new Point2f(0, maxHeight - 1)
        };

        Mat M = Cv2.GetPerspectiveTransform(rect, dst);
        Mat warped = new Mat();
        Cv2.WarpPerspective(imageProcessed, warped, M, new Size(maxWidth, maxHeight));
        Mat originalImageWarped = new Mat();
        Cv2.WarpPerspective(imageOriginal, originalImageWarped, M, new Size(maxWidth, maxHeight));

        return (warped,originalImageWarped, M);
    }


    public (Mat warped, Mat warpedOriginal, Point2f[] approx, Mat M) FindSudokuGrid(Mat preprocessedImage, Mat originalImage)
    {
        HierarchyIndex[] hierarchy;
        Point[][] contours;
        Cv2.FindContours(preprocessedImage, out contours, out hierarchy, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        double maxArea = 0;
        Point[] bestContour = null;

        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area > maxArea)
            {
                maxArea = area;
                bestContour = contour;
            }
        }

        if (bestContour != null)
        {
            double peri = Cv2.ArcLength(bestContour, true);
            Point[] approxPoints = Cv2.ApproxPolyDP(bestContour, 0.02 * peri, true);

            if (approxPoints.Length == 4) // The Sudoku grid should roughly be a square
            {
                // Convert points to OpenCV Point2f
                Point2f[] srcPoints = approxPoints.Select(p => new Point2f(p.X, p.Y)).ToArray();
                (Mat warped,Mat originalImageWarped, Mat M_orig) = FourPointTransform(preprocessedImage, originalImage,srcPoints);

                return (warped, originalImageWarped, srcPoints, M_orig); // Here, srcPoints is already Point2f[]
            }
        }

        return (null, null, null, null);
    }


    public IEnumerator ExtractAndRecognizeCellsAsync(Mat originalImage, Mat gridImage, Action<int[,]> onCompletion)
    {
        int[,] cells = new int[9, 9]; // Initialize the Sudoku grid as a 9x9 matrix of zeros

        int cellHeight = gridImage.Height / 9;
        int cellWidth = gridImage.Width / 9;

        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                // Extract the individual cell from the grid image
                Rect cellRect = new Rect(col * cellWidth, row * cellHeight, cellWidth, cellHeight);
                Mat cellOrig = new Mat(originalImage, cellRect);
                Mat cellWrap = new Mat(gridImage, cellRect);
                Mat cellProcessed = imageProcessing.PreprocessCellForOCR(cellOrig);


                bool firstLetterProcessed = false;


                // Assuming RecognizeAsync is an adjusted version of Recognize that works with coroutines
                yield return StartCoroutine(customOCREngine.RecognizeAsync(cellOrig,cellWrap, cellProcessed,
                    (digitText) =>
                    {
                        ProcessOCRResult(digitText, row, col, cells);
                        if (!firstLetterProcessed)
                        {
                            firstLetterProcessed = true;
                            DisplayResultTexture(cellOrig, "Letter sample", letterRawImage, false);
                            DisplayResultTexture(cellWrap, "Letter sample", letterWrapRawImage, false);
                            DisplayResultTexture(cellProcessed, "Trimmed letter sample", trimmedLetterRawImage, false);
                        }
                    }));

                //Debug.LogWarning("Break!");
                //break;
            }
        }

        // Callback or continuation action after all cells have been processed
        onCompletion?.Invoke(cells);
    }


    public void ExtractAndRecognizeCells(Mat originalImage, Mat gridImage, Action<int[,]> onCompletion)
    {
        int[,] cells = new int[9, 9]; // Initialize the Sudoku grid as a 9x9 matrix of zeros

        int cellHeight = gridImage.Height / 9;
        int cellWidth = gridImage.Width / 9;

        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                // Extract the individual cell from the grid image
                Rect cellRect = new Rect(col * cellWidth, row * cellHeight, cellWidth, cellHeight);
                Mat cellOrig = new Mat(originalImage, cellRect);
                Mat cellWrap = new Mat(gridImage, cellRect);
                Mat cellProcessed = imageProcessing.PreprocessCellForOCR(cellOrig);

                bool firstLetterProcessed = false;

                customOCREngine.Recognize(cellOrig, cellWrap, cellProcessed, (digitText) =>
                {
                    ProcessOCRResult(digitText, row, col, cells);
                    if (!firstLetterProcessed)
                    {
                        firstLetterProcessed = true;
                        DisplayResultTexture(cellOrig, "Letter sample", letterRawImage, false);
                        DisplayResultTexture(cellWrap, "Letter sample", letterWrapRawImage, false);
                        DisplayResultTexture(cellProcessed, "Trimmed letter sample", trimmedLetterRawImage, false);
                    }
                });
            }
        }

        // Callback or continuation action after all cells have been processed
        onCompletion?.Invoke(cells);
    }


    private void ProcessOCRResult(string digitText, int row, int col, int[,] cells)
    {
        digitText = digitText.Trim();
        if (int.TryParse(digitText, out int digit) && digit >= 1 && digit <= 9)
        {
            cells[row, col] = digit;
            //Debug.Log($"Detected digit: {digitText}");
        }
        else
        {
            //Debug.Log($"Not Found - unrecognized or empty");
            cells[row, col] = 0; // Empty cell
        }
    }


    public Mat AnnotateSolution(Mat originalImage, int[,] solution, List<Point2f[]> cellPositions, Scalar color,
        Mat MInv)
    {
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                int digit = solution[row, col];


                if (digit > 0) // If the cell is not empty
                {
                    // Calculate cell center based on its corners
                    Point2f[] cellCorners = cellPositions[row * 9 + col];
                    double averageX = cellCorners.Average(corner => corner.X);
                    double averageY = cellCorners.Average(corner => corner.Y);
                    Point2f cellCenter = new Point2f(
                        (float)averageX,
                        (float)averageY);

                    // Use the smaller dimension to ensure the text fits within the cell
                    double cellWidth = CalculateDistance(cellCorners[1], cellCorners[0]);
                    double cellHeight = CalculateDistance(cellCorners[3], cellCorners[0]);
                    double cellSize = Mathf.Min((float)cellWidth, (float)cellHeight);

                    // Dynamically adjust font scale and thickness based on cell size
                    double fontScale = cellSize / 40;
                    int thickness = Mathf.Max(2, (int)(cellSize / 20));

                    // Transform the center point back to the original image's coordinate system
                    Point2f[] srcPoint = { cellCenter };
                    Point2f[] dstPoint = Cv2.PerspectiveTransform(srcPoint, MInv);

                    // Draw the digit on the original image
                    Cv2.PutText(originalImage, digit.ToString(), dstPoint[0], HersheyFonts.HersheySimplex, fontScale,
                        color, thickness);
                }
            }
        }

        return originalImage;
    }

    public Point2f[] OrderPoints(Point2f[] pts)
    {
        // Initialize an array of points that will be ordered
        Point2f[] rect = new Point2f[4];

        // The ordering of points in the rect should be [top-left, top-right, bottom-right, bottom-left]
        // Calculate the sum and difference of the points
        var sum = pts.Select(pt => pt.X + pt.Y).ToArray();
        var diff = pts.Select(pt => pt.Y - pt.X).ToArray();

        // Assign corners based on the sum and difference
        rect[0] = pts[Array.IndexOf(sum, sum.Min())]; // Top-left has the smallest sum
        rect[2] = pts[Array.IndexOf(sum, sum.Max())]; // Bottom-right has the largest sum

        rect[1] = pts[Array.IndexOf(diff, diff.Min())]; // Top-right has the smallest difference
        rect[3] = pts[Array.IndexOf(diff, diff.Max())]; // Bottom-left has the largest difference

        return rect;
    }

    public List<Point2f[]> CalculateCellPositions(Mat originalImage, Point2f[] gridCorners)
    {
        // First, correct the perspective distortion
        var (correctedImage,correctedOriginalImage, M) = FourPointTransform(originalImage, originalImage, gridCorners);

        List<Point2f[]> cellPositions = new List<Point2f[]>();
        int cellSizeCol = correctedImage.Width / 9;
        int cellSizeRow = correctedImage.Height / 9;

        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                int cellX = col * cellSizeCol;
                int cellY = row * cellSizeRow;

                // For each cell, now store all four corners
                Point2f[] corners = new Point2f[]
                {
                    new Point2f(cellX, cellY), // Top-left
                    new Point2f(cellX + cellSizeCol, cellY), // Top-right
                    new Point2f(cellX + cellSizeCol, cellY + cellSizeRow), // Bottom-right
                    new Point2f(cellX, cellY + cellSizeRow) // Bottom-left
                };

                cellPositions.Add(corners);
            }
        }

        return cellPositions;
    }


    (Mat, Mat, Mat, Point2f[], List<Point2f[]>, Mat) PrepareWarpedGrid()
    {
        Mat originalImage = GetInputMat();

        DisplayResultTexture(originalImage, "OriginalImage", originalRawImage, false);

        
        Mat processedImage = PreprocessImageForWrap(originalImage);

        (Mat warpedGrid, Mat originalImageWarped, Point2f[] gridCorners, Mat M) = FindSudokuGrid(processedImage, originalImage);
        if (warpedGrid == null)
        {
            Debug.Log("Sudoku grid not found :(.");
            DisplayResultTexture(processedImage, "nonwarpedGrid", nonWrappedRawImage, false);
            DisplayResultTexture(warpedGrid, "warpedGrid", wrappedRawImage, false);
            DisplayResultTexture(originalImageWarped, "warpedGrid", wrappedOriginalRawImage, false);
            DisplayResultTexture((Texture2D)null, "warpedGrid", letterPreviewRawImage, false);
            return (originalImage, null, null, null, null, null);
        }

        //Debug.Log("Sudoku grid found.");
        //Debug.Log("Grid corners:");
        //Debug.Log(gridCorners);
        //Debug.Log("Warped grid:");
        //Debug.Log(warpedGrid);
        DisplayResultTexture(AddGridCorners(processedImage, gridCorners), "nonWarpedGrid", nonWrappedRawImage, false);
        DisplayResultTexture(warpedGrid, "warpedGrid", wrappedRawImage, false);
        DisplayResultTexture(originalImageWarped, "warpedGrid", wrappedOriginalRawImage, false);
        DisplayResultTexture(imageProcessing.PreprocessCellForOCR(originalImageWarped,false), "letterPreviewRawImage", letterPreviewRawImage, false);
        
        
        Mat MInv = new Mat();
        Cv2.Invert(M, MInv, DecompTypes.LU);


        List<Point2f[]> cellPositions = CalculateCellPositions(originalImage, gridCorners);
        //Debug.Log("Grid and cell positions calculated.");

        originalImage = AddGridCorners(originalImage, gridCorners);


        return (originalImage, originalImageWarped, warpedGrid, gridCorners, cellPositions, MInv);
    }

    private Mat AddGridCorners(Mat nonWarpedGrid, Point2f[] gridCorners)
    {
        Mat outputImage = new Mat();
        if (nonWarpedGrid.Channels() == 1)
        {
            Cv2.CvtColor(nonWarpedGrid, outputImage, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            outputImage = nonWarpedGrid.Clone();
        }

        // Define the color for the grid corners and lines (Red in BGR format)
        Scalar cornerColor = new Scalar(0, 0, 255); // Red
        int thickness = 2; // Line thickness

        // Convert Point2f to Point for drawing functions
        Point[] corners = gridCorners.Select(corner => new Point(corner.X, corner.Y)).ToArray();

        // Draw the corners
        foreach (var corner in corners)
        {
            Cv2.Circle(outputImage, corner, thickness * 2, cornerColor, -1); // -1 fills the circle
        }

        // Draw lines connecting the corners to form the detected grid
        Cv2.Line(outputImage, corners[0], corners[1], cornerColor, thickness);
        Cv2.Line(outputImage, corners[1], corners[2], cornerColor, thickness);
        Cv2.Line(outputImage, corners[2], corners[3], cornerColor, thickness);
        Cv2.Line(outputImage, corners[3], corners[0], cornerColor, thickness);

        return outputImage;
    }

    private void ProcessSudokuImageUpdate()
    {
        (Mat originalImage,Mat originalImageWarped, Mat warpedGrid, Point2f[] gridCorners, List<Point2f[]> cellPositions, Mat MInv) =
            PrepareWarpedGrid();

        if (warpedGrid != null)
        {
            ExtractAndRecognizeCells(originalImageWarped, warpedGrid, recognizedDigits =>
            {
                //Debug.Log("Recognized digits from the sudoku grid.");

                recognizedDigits = OptimizeRecognizedDigits(recognizedDigits);
                //Debug.Log("Optimized recognized digits.");

                Mat annotatedImage = AnnotateSolution(originalImage, recognizedDigits, cellPositions,
                    new Scalar(255, 0, 255), MInv);


                if (solve)
                {
                    DisplayResultTexture(annotatedImage, "Annotated texture", annotatedRawImage, false);
                    // Assuming SolveSudoku and a second call to AnnotateSolution are implemented similarly
                    int[,] solution = sudokuSolver.SolveSudoku(recognizedDigits);
                    int[,] onlyNewDigits = sudokuSolver.GetOnlyNewDigits(recognizedDigits, solution);

                    annotatedImage =
                        AnnotateSolution(originalImage, onlyNewDigits, cellPositions, new Scalar(255, 0, 0), MInv);


                    DisplayResultTexture(annotatedImage, "Result texture", finalRawImage, false);
                }
                else
                {
                    DisplayResultTexture(annotatedImage, "Annotated texture", annotatedRawImage, false);
                    DisplayResultTexture(annotatedImage, "Result texture", finalRawImage, false);
                }
            });
        }
        else
        {
            DisplayResultTexture(originalImage, "Annotated texture", annotatedRawImage, false);
            DisplayResultTexture(originalImage, "Result texture", finalRawImage, false);
        }
    }

    private IEnumerator ProcessSudokuImage()
    {
        (Mat originalImage, Mat originalImageWarped, Mat warpedGrid, Point2f[] gridCorners, List<Point2f[]> cellPositions, Mat MInv) =
            PrepareWarpedGrid();


        if (warpedGrid == null)
        {
            yield break;
        }


        // Wait for ExtractAndRecognizeCells to complete
        yield return StartCoroutine(ExtractAndRecognizeCellsAsync(originalImageWarped, warpedGrid, recognizedDigits =>
        {
            Debug.Log("Recognized digits from the sudoku grid.");

            recognizedDigits = OptimizeRecognizedDigits(recognizedDigits);
            Debug.Log("Optimized recognized digits.");

            Mat annotatedImage = AnnotateSolution(originalImage, recognizedDigits, cellPositions,
                new Scalar(255, 0, 255), MInv);
            DisplayResultTexture(annotatedImage, "Annotated texture", annotatedRawImage, false);

            // Assuming SolveSudoku and a second call to AnnotateSolution are implemented similarly
            int[,] solution = sudokuSolver.SolveSudoku(recognizedDigits);
            int[,] onlyNewDigits = sudokuSolver.GetOnlyNewDigits(recognizedDigits, solution);

            annotatedImage =
                AnnotateSolution(originalImage, recognizedDigits, cellPositions, new Scalar(0, 0, 255), MInv);


            DisplayResultTexture(annotatedImage, "Result texture", finalRawImage, false);
        }));
    }

    private Mat GetInputMat()
    {
        if (useCamera)
        {
            return webCameraSudoku.webCamMat;
        }

        Camera cam = Camera.main; // Get the main camera
        if (cam == null)
        {
            Debug.LogError("Main camera not found.");
            return null;
        }

        // Create a RenderTexture
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        cam.targetTexture = renderTexture; // Set the camera to render to our RenderTexture
        cam.Render(); // Force the camera to render

        // Now that we have rendered the scene to the renderTexture, let's copy it to a Texture2D
        Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture; // Set the current RenderTexture to the one we've rendered into
        texture.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0,
            0); // Copy the pixels from the RenderTexture to the Texture2D
        texture.Apply(); // Apply changes to the Texture2D

        // Clean up
        cam.targetTexture = null; // Reset the camera's target texture
        RenderTexture.active = null; // Reset the active RenderTexture
        Destroy(renderTexture); // We're done with the RenderTexture, destroy it

        Mat originalImage = OpenCvSharp.Unity.TextureToMat(texture); // Convert Texture2D to Mat

        return originalImage; // Return the Texture2D
    }


    public static void DisplayResultTexture(Mat mat, string nameOfTexture, RawImage rawImage = null, bool resize = true)
    {
        if (mat != null)
        {
            Texture2D resultTexture =
                OpenCvSharp.Unity.MatToTexture(mat); // Implement conversion based on your setup
            DisplayResultTexture(resultTexture, nameOfTexture, rawImage, resize);
        }
        else
        {
            DisplayResultTexture((Texture2D)null, nameOfTexture, rawImage, resize);
        }
    }

    public static void DisplayResultTexture(Texture2D texture, string nameOfTexture, RawImage rawImage = null,
        bool resize = true)
    {
        //Debug.Log("Showing image: " + nameOfTexture);

        if (rawImage == null)
        {
            GameObject resultTexture = new GameObject(nameOfTexture);
            Transform parent = FindAnyObjectByType<Canvas>().transform;
            resultTexture.transform.SetParent(parent);
            rawImage = resultTexture.AddComponent<RawImage>();
        }

        if (rawImage != null)
        {
            // Set the texture of the RawImage to display the processed image
            rawImage.texture = texture;

            if (resize)
            {
                if (texture != null)
                {
                    // Optionally, adjust the size of the RawImage to match the texture dimensions
                    rawImage.rectTransform.sizeDelta = new Vector2(texture.width, texture.height);
                }
            }
        }
        else
        {
            Debug.LogError("ResultDisplay RawImage component is not assigned or found.");
        }
    }


    void LateUpdate()
    {
        if (Time.frameCount % frameSkip != 0)
        {
            return;
        }

        outputText.text = "";
        if (useCamera)
        {
            outputText.text += "Camera: true. #" + webCameraSudoku.cameraIndex + "\n";
        }
        else
        {
            outputText.text += "Camera: false. Using test image.\n";
        }

        if (solve)
        {
            outputText.text += "Solve: true\n";
        }
        else
        {
            outputText.text += "Solve: false\n";
        }

        if (customOCREngine.updateDB)
        {
            outputText.text += "customOCREngine.updateDB: true\n";
        }
        else
        {
            outputText.text += "customOCREngine.updateDB: false\n";
        }

        if (RenderScreenInUpdate)
        {
            outputText.text += "RenderScreenInUpdate: true\n";
        }
        else
        {
            outputText.text += "RenderScreenInUpdate: false\n";
        }


        outputText.text += "Size: " + imageProcessing.gridSize + "\n";
        outputText.text += "Accuracy: " + numberRecognizer.RecognizeThreshold + "\n";


        if (useCamera)
        {
            webCameraSudoku.enabled = (true);
        }
        else
        {
            webCameraSudoku.enabled = (false);
        }

        if (RenderOneFrame)
        {
            if (customOCREngine.updateDB)
            {
                StartCoroutine(ProcessSudokuImage());
            }
            else
            {
                ProcessSudokuImageUpdate();
            }

            RenderOneFrame = false;
        }

        if (RenderScreenInUpdate)
        {
            ProcessSudokuImageUpdate();
        }
    }
}