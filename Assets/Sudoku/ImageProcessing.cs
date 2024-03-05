using OpenCvSharp;
using UnityEngine;
using UnityEngine.Serialization;
using Rect = OpenCvSharp.Rect;

public class ImageProcessing : MonoBehaviour
{
    [Header("Blur Settings")] public bool useBlur = true;
    [Range(0, 100)] public int blurSize = 5;
    [Range(0, 100)] public double sigmaX = 0;

    [Header("Bilateral Filter")] public bool useBilateralFilter = false;
    [Range(1, 10)] public int d = 5; // Diameter of each pixel neighborhood
    [Range(10, 150)] public double sigmaColor = 75; // Filter sigma in the color space
    [Range(10, 150)] public double sigmaSpace = 75; // Filter sigma in the coordinate space


    [Header("Adaptive thresholding")] public bool useAdaptive = true;
    [Range(0, 100)] public int blockSize = 41;
    [Range(0, 100)] public double C = 20;

    [Header("Edge Detection")] public bool useEdgeDetection = false;
    [Range(0, 200)] public double cannyThreshold1 = 50;
    [Range(0, 200)] public double cannyThreshold2 = 150;

    [Header("Contrast Adjustment")] public bool useContrastAdjustments = true;
    [Range(1.0f, 3.0f)] public float alpha = 1.0f; // Contrast control
    [Range(0.0f, 100.0f)] public float beta = 0; // Brightness control

    [Header("CLAHE")] public bool useClahe = true;
    [Range(1, 40)] public float clipLimit = 2.0f;
    [Range(1, 128)] public int tileGridSize = 8;

    [Header("EqualizeHist")] public bool useEqualizeHist = true;

    [Header("Morphological Operations")] public bool useMorph = true;
    [Range(1, 5)] public int morphSize = 1; // Kernel size for morphological operations


    [Header("Crop and resize settings")] [Range(0, 0.45f)]
    public float cropBorderPercentage = 0.20f;

    [Range(3, 10)] public float trimFromCenterValue = 3;
    [FormerlySerializedAs("gridSize")] public int resizedWidthAndHeight = 64;

    public int minPixelsForEnlarge = 8;

    public Mat CropAndResizeImage(Mat originalImage)
    {
        Mat originalImageCropped = new Mat();
        originalImageCropped = originalImage.Clone();

       
        float removingPixelsX = originalImageCropped.Width * cropBorderPercentage;
        float removingPixelsY = originalImageCropped.Width * cropBorderPercentage;
        originalImageCropped = new Mat(originalImageCropped,
            new Rect((int)removingPixelsX, (int)removingPixelsY,
                (int)(originalImageCropped.Width - 2 * removingPixelsX),
                (int)(originalImageCropped.Height - 2 * removingPixelsY)));
             


        originalImageCropped = TrimBordersFromCenter(originalImageCropped);

      
/*
        Cv2.CopyMakeBorder(originalImageCropped, originalImageCropped, 20, 20, 20, 20,
            BorderTypes.Constant, new Scalar(0, 0, 0));
*/
        originalImageCropped = TrimImage(originalImageCropped);

        if (Cv2.CountNonZero(originalImageCropped) < minPixelsForEnlarge)
        {
            originalImageCropped.Release();
            // Create an 8x8 black Mat
            Mat blackMat = Mat.Zeros(resizedWidthAndHeight, resizedWidthAndHeight, MatType.CV_8UC1).ToMat();
            return blackMat;
        }
        else
        {
            Cv2.Resize(originalImageCropped, originalImageCropped,
                new Size(resizedWidthAndHeight, resizedWidthAndHeight));
            Cv2.Threshold(originalImageCropped, originalImageCropped, 128, 255, ThresholdTypes.Binary);
        }

        return originalImageCropped;
    }

    public Mat PreprocessImage(Mat originalImage)
    {
        Mat originalImageCropped = new Mat();
        originalImageCropped = originalImage.Clone();


        if (blurSize % 2 == 0) blurSize++;
        if (blockSize % 2 == 0) blockSize++;

        Mat gray = new Mat();
        Cv2.CvtColor(originalImageCropped, gray, ColorConversionCodes.BGR2GRAY);
        originalImageCropped.Dispose();


        Mat claheImg = new Mat();
        if (useClahe)
        {
            // Apply CLAHE
            CLAHE clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileGridSize, tileGridSize));
            clahe.Apply(gray, claheImg);
        }
        else
        {
            claheImg = gray.Clone();
        }

        gray.Dispose();


        if (useEqualizeHist)
        {
            Cv2.EqualizeHist(claheImg, claheImg);
        }


        Mat contrastAdjusted = new Mat();
        if (useContrastAdjustments)
        {
            // Dynamic contrast and brightness adjustment (optional, depending on results from CLAHE)
            claheImg.ConvertTo(contrastAdjusted, -1, alpha, beta);
        }
        else
        {
            contrastAdjusted = claheImg.Clone();
        }

        claheImg.Dispose();


        if (useBilateralFilter)
        {
            Mat bilateralFiltered = new Mat();
            Cv2.BilateralFilter(contrastAdjusted, bilateralFiltered, d, sigmaColor, sigmaSpace);
            contrastAdjusted = bilateralFiltered;
        }


        Mat blurred = new Mat();
        if (useBlur)
        {
            // Apply Gaussian Blur to reduce noise
            Cv2.GaussianBlur(contrastAdjusted, blurred, new Size(blurSize, blurSize), sigmaX);
        }
        else
        {
            blurred = contrastAdjusted.Clone();
        }

        contrastAdjusted.Dispose();


        Mat adaptiveThresh = new Mat();
        if (useAdaptive)
        {
            // Apply adaptive thresholding
            Cv2.AdaptiveThreshold(blurred, adaptiveThresh, 255, AdaptiveThresholdTypes.GaussianC,
                ThresholdTypes.BinaryInv, blockSize, C);
        }
        else
        {
            adaptiveThresh = blurred.Clone();
        }

        blurred.Dispose();


        if (useEdgeDetection)
        {
            Mat edges = new Mat();
            Cv2.Canny(adaptiveThresh, edges, cannyThreshold1, cannyThreshold2);
            adaptiveThresh = edges;
        }


        Mat morphed = new Mat();
        if (useMorph)
        {
            // Apply morphological operations to enhance grid lines
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2 * morphSize + 1, 2 * morphSize + 1),
                new Point(morphSize, morphSize));

            Cv2.MorphologyEx(adaptiveThresh, morphed, MorphTypes.Close, kernel);
        }
        else
        {
            morphed = adaptiveThresh.Clone();
        }

        adaptiveThresh.Dispose();


        return morphed;
    }


    // Trim the outer parts of the image that do not contain the main content
    private Mat TrimImage(Mat binaryImage)
    {
        // Find contours
        Point[][] contours;
        HierarchyIndex[] hierarchy;
        Cv2.FindContours(binaryImage, out contours, out hierarchy, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        // Assuming the largest contour is the white number
        double maxArea = 0;
        Rect boundingRect = new Rect();
        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area > maxArea)
            {
                maxArea = area;
                boundingRect = Cv2.BoundingRect(contour);
            }
        }

        if (boundingRect.Width == 0 || boundingRect.Height == 0)
        {
            return binaryImage;
        }

        // Crop the image to the bounding rectangle of the largest contour
        Mat croppedImage = new Mat(binaryImage, boundingRect);

        return croppedImage;
    }


    // Trim borders from the center of a cell image
    private Mat TrimBordersFromCenter(Mat cell)
    {
        int width = cell.Width;
        int height = cell.Height;

        if (trimFromCenterValue > width / 2) trimFromCenterValue--;


        int startX = (int)(width / trimFromCenterValue);
        int endX = (int)((trimFromCenterValue - 1) * width / trimFromCenterValue);
        int startY = (int)(height / trimFromCenterValue);
        int endY = (int)((trimFromCenterValue - 1) * height / trimFromCenterValue);
        //Debug.Log("Start:"+width+"x"+height+" = "+startX + " " + endX + " " + startY + " " + endY);
        // Convert to binary for easier processing
        //Cv2.Threshold(cell, cell, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);


        //Debug.Log(startX + " " + endX + " " + startY + " " + endY);
        // Expand the box until white space is encountered
        while (mustExpandHasWhitePixels(cell, "left", startX, endX, startY, endY))
        {
            startX--;
        }

        while (mustExpandHasWhitePixels(cell, "right", startX, endX, startY, endY))
        {
            endX++;
        }

        while (mustExpandHasWhitePixels(cell, "up", startX, endX, startY, endY))
        {
            startY--;
        }

        while (mustExpandHasWhitePixels(cell, "down", startX, endX, startY, endY))
        {
            endY++;
        }

        //Debug.Log("End:"+width+"x"+height+" = "+startX + " " + endX + " " + startY + "+1 " + endY+" +1");
        // Crop the original grayscale image according to the final expanded box
        Mat croppedCell = new Mat(cell, new Rect(startX, startY, endX - startX +1, endY - startY+1));
        return croppedCell;
    }

    // Lambda function to check if expansion is possible in a direction
    bool mustExpandHasWhitePixels(Mat cell, string direction, int startX, int endX, int startY, int endY)
    {
        int width = cell.Width;
        int height = cell.Height;
        //Debug.Log(direction);
        bool ended = false;
        switch (direction)
        {
            case "left":
                if (startX <= 0) ended = true;
                break;
            case "right":
                if (endX >= width-1) ended = true;
                break;
            case "up":
                if (startY <= 0) ended = true;
                break;
            case "down":
                if (endY >= height-1) ended = true;
                break;
        }

        if (ended)
        {
            //Debug.Log("Direction " + direction + " ended for "+width+"x"+height);
            //Debug.Log("Dims " + startX+"-"+ endX+" Y:"+startY+"-"+endY);
            return false;
        }

        float exp = 0.001f;


        float whites = 0;
        switch (direction)
        {
            case "left":
                
                whites = CountWhites(cell[new Rect(startX - 1, startY, 1, endY - startY+1)]);
                //whites = 0;
                break;
            case "right":
                whites = CountWhites(cell[new Rect(endX + 1, startY, 1, endY - startY+1)]);
                //whites = 0;
                break;
            case "up":
                whites = CountWhites(cell[new Rect(startX, startY - 1, endX - startX+1, 1)]);
                //whites = 0;
                break;
            case "down":
                whites = CountWhites(cell[new Rect(startX, endY + 1, endX - startX+1, 1)]);
                //whites = 1;
                break;
                
        }

        bool expandHasWhitePixels = whites > exp;
        if (!expandHasWhitePixels)
        {
            //Debug.Log("No white pixels for side "+direction+" Whites:"+whites);
        }
        
        return expandHasWhitePixels;

    }


    private float CountWhites(Mat mat)
    {
        
        int countNonZero = Cv2.CountNonZero(mat);
        //Debug.Log(countNonZero);


        int width = mat.Width;
        int height = mat.Height;

        int totalPixels = width * height;

        //SudokuImageReader.DisplayResultTexture(mat, "Count - "+countNonZero);
        //Debug.Log((float)countNonZero / (float)totalPixels);
        return (float)countNonZero / (float)totalPixels;
    }
}