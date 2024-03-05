using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Demo;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using Rect = OpenCvSharp.Rect; // Assuming you have OpenCV for Unity

public class ImageProcessing : MonoBehaviour
{
    public int gridSize = 64;

    public enum AdaptiveMethod
    {
        Gaussian,
        Mean
    }

    public enum ThresholdType
    {
        Binary,
        BinaryInv
    }

    // Convert a Texture2D to a grayscale image
    public Mat ConvertToGrayscale(Mat imageMat)
    {
        Mat grayscaleMat = new Mat();
        Cv2.CvtColor(imageMat, grayscaleMat, ColorConversionCodes.BGR2GRAY);
        return grayscaleMat;
    }

    // Apply Gaussian Blur to an image
    public Mat ApplyGaussianBlur(Mat image, int width, int height, double sigmaX)
    {
        Mat blurredMat = new Mat();
        Cv2.GaussianBlur(image, blurredMat, new Size(width, height), sigmaX);
        return blurredMat;
    }


    [Range(0, 0.45f)] float cropBorderPercentage = 0.15f;


    [Header("Blur Settings")] [Range(0, 100)]
    public int blockSize = 41;

    [Range(0, 100)] public double C = 20;
    [Range(0, 100)] public int blurSize = 5;
    [Range(0, 100)] public double sigmaX = 0;


    public double clipLimit = 40;
    public int claheSize = 0;
    
    // Method for adaptive histogram equalization using CLAHE
    public Mat ApplyCLAHE(Mat image, double clipLimit = 40.0, Size tileGridSize = default(Size))
    {
        if (tileGridSize == default(Size))
        {
            tileGridSize = new Size(claheSize, claheSize); // Default tile grid size
        }
        
        Mat imgClahe = new Mat();
        // Create a CLAHE instance
        var clahe = Cv2.CreateCLAHE(clipLimit, tileGridSize);
        clahe.Apply(image, imgClahe);
        
        return imgClahe;
    }
    
    // This method should implement all the preprocessing steps for a cell image before OCR.
    public Mat PreprocessCellForOCR(Mat cell, bool cropBorder=true)
    {
        Mat decreasedCell = new Mat();
       
        if (cropBorder)
        {
            float removingPixelsX = cell.Width * cropBorderPercentage;
            float removingPixelsY = cell.Width * cropBorderPercentage;
            decreasedCell = new Mat(cell.Clone(),
                new Rect((int)removingPixelsX, (int)removingPixelsY, (int)(cell.Width - 2 * removingPixelsX),
                    (int)(cell.Height - 2 * removingPixelsY)));
        }
        else
        {
            decreasedCell = cell.Clone();
        }
        


        if (blurSize % 2 == 0) blurSize++;
        if (blockSize % 2 == 0) blockSize++;
        Mat gray = ConvertToGrayscale(decreasedCell);
        // Apply CLAHE to improve contrast and normalize brightness
        //Mat claheResult = ApplyCLAHE(gray,clipLimit);
        //return claheResult;
        
        //Cv2.BilateralFilter(gray, gray , 2,75, 75, BorderTypes.Constant);
        //Cv2.Canny(image, edges, threshold1, threshold2);

        Mat blur = ApplyGaussianBlur(gray, blurSize, blurSize, sigmaX);
        Mat adaptiveThresh = ApplyAdaptiveThreshold(blur, 255, ImageProcessing.AdaptiveMethod.Gaussian,
            ImageProcessing.ThresholdType.BinaryInv, blockSize, C);

        if (cropBorder)
        {
            adaptiveThresh = TrimBordersFromCenter(adaptiveThresh);

            //return adaptiveThresh;

            Cv2.CopyMakeBorder(adaptiveThresh, adaptiveThresh, 20, 20, 20, 20,
                BorderTypes.Constant, new Scalar(0, 0, 0));

            adaptiveThresh = TrimImage(adaptiveThresh);

          

            Cv2.Resize(adaptiveThresh, adaptiveThresh, new Size(gridSize, gridSize));
            Cv2.Threshold(adaptiveThresh, adaptiveThresh, 128, 255, ThresholdTypes.Binary);
        }

        return adaptiveThresh;


    }

    // Apply Adaptive Threshold to an image
    public Mat ApplyAdaptiveThreshold(Mat image, double maxValue, AdaptiveMethod method, ThresholdType type,
        int blockSize, double C)
    {
        Mat thresholdMat = new Mat();
        AdaptiveThresholdTypes adaptiveMethod = method == AdaptiveMethod.Gaussian
            ? AdaptiveThresholdTypes.GaussianC
            : AdaptiveThresholdTypes.MeanC;
        ThresholdTypes thresholdType = type == ThresholdType.Binary ? ThresholdTypes.Binary : ThresholdTypes.BinaryInv;
        Cv2.AdaptiveThreshold(image, thresholdMat, maxValue, adaptiveMethod, thresholdType, blockSize, C);
        return thresholdMat;
    }


    // Trim the outer parts of the image that do not contain the main content
    public Mat TrimImage(Mat binaryImage)
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


    [Range(3, 10)] public float trimFromCenterValue = 3;

    // Trim borders from the center of a cell image
    public Mat TrimBordersFromCenter(Mat cell)
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

        //Debug.Log("End:"+width+"x"+height+" = "+startX + " " + endX + " " + startY + " " + endY);
        // Crop the original grayscale image according to the final expanded box
        Mat croppedCell = new Mat(cell, new Rect(startX, startY, endX - startX, endY - startY));
        return croppedCell;
    }

    // Lambda function to check if expansion is possible in a direction
    bool mustExpandHasWhitePixels(Mat cell, string direction, int startX, int endX, int startY, int endY)
    {
        int width = cell.Width;
        int height = cell.Height;
        float expansionThreshold = 0.001f;
        //Debug.Log(direction);
        switch (direction)
        {
            case "left":
                return startX > 0 &&
                       CountWhites(cell[new Rect(startX - 1, startY, 1, endY - startY)]) > expansionThreshold;
            case "right":
                return endX < width - 1 && CountWhites(cell[new Rect(endX + 1, startY, 1, endY - startY)]) >
                    expansionThreshold;
            case "up":
                return startY > 0 && CountWhites(cell[new Rect(startX, startY - 1, endX - startX, 1)]) >
                    expansionThreshold;
            case "down":
                return endY < height - 1 && CountWhites(cell[new Rect(startX, endY + 1, endX - startX, 1)]) >
                    expansionThreshold;
            default:
                return false;
        }
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