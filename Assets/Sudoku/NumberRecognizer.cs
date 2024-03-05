using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using OpenCvSharp;
using UnityEngine;

public class NumberRecognizer : MonoBehaviour
{
    [Range(0.1f, 1)] public float RecognizeThreshold = 0.9f;
    [Range(0.1f, 1)] public float VariantThreshold = 0.8f;
    private string dbPath = "number_db.json";
    private Dictionary<string, List<PatternVariant>> db = new Dictionary<string, List<PatternVariant>>();

    public NumberRecognizer()
    {
        LoadDB();
    }

    public void LoadDB()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, dbPath);


        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            db = JsonConvert.DeserializeObject<Dictionary<string, List<PatternVariant>>>(json);
            if (db == null)
            {
                Debug.LogWarning("Creating new DB file");
                db = new Dictionary<string, List<PatternVariant>>();
                SaveDB();
            }
        }
        else
        {
            Debug.LogError("File not dound" + filePath);
            db = new Dictionary<string, List<PatternVariant>>();
        }
    }


    public void ResetLearn()
    {
        db = new Dictionary<string, List<PatternVariant>>();
        SaveDB();
    }

    private void SaveDB()
    {
        string json = JsonConvert.SerializeObject(db);


        string filePath = Path.Combine(Application.streamingAssetsPath, dbPath);
        Debug.LogError("File saved" + filePath);
        File.WriteAllText(filePath, json);
    }

    public (string bestMatchKey, float bestSimilarity, int bestVariantIndex) RecognizeNumber(Mat image)
    {
        float similarityThreshold = RecognizeThreshold;

        float[] imageFlat = FlattenImage(image);
        string bestMatchKey = null;
        float bestSimilarity = -1;
        int bestVariantIndex = -1;

        //Debug.Log("DB contains:" + db.Count);
        foreach (KeyValuePair<string, List<PatternVariant>> entry in db)
        {
            string key = entry.Key;
            List<PatternVariant> variants = entry.Value;

            for (int index = 0; index < variants.Count; index++)
            {
                PatternVariant variant = variants[index];
                float[] variantArray = variant.Image;

                //Debug.Log(imageFlat.Length);
                //Debug.Log(variantArray.Length);
                // Ensure the compared images are of the same dimension
                if (imageFlat.Length == variantArray.Length)
                {
                    float similarity = CalculateSimilarityScore(imageFlat, variantArray);

                    // Debug.Log(imageFlat.Length);
                    // Debug.Log(imageFlat[0]);
                    // Debug.Log(variantArray[0]);
                    // Debug.Log(similarity);
                    // Debug.Log(bestSimilarity);
                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestMatchKey = key;
                        bestVariantIndex = index;
                    }
                }
            }
        }


        if (bestSimilarity > similarityThreshold)
        {
            return (bestMatchKey, bestSimilarity, bestVariantIndex);
        }

        return (null, bestSimilarity, bestVariantIndex);
    }


    float[] FlattenImage(Mat image)
    {
        // Ensure the image is in grayscale
        Mat grayImage = new Mat();
        if (image.Channels() > 1)
        {
            Cv2.CvtColor(image, grayImage, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            grayImage = image;
        }

        int totalPixels = (int)grayImage.Total();
        // Convert Mat to byte array
        byte[] grayscaleBytes = new byte[totalPixels];
        grayImage.GetArray(0, 0, grayscaleBytes); // Assuming grayImage is of type Mat


        //SudokuImageReader.DisplayResultTexture(image,"image for flattening");
        //SudokuImageReader.DisplayResultTexture(grayImage,"grayImage for flattening");

        // Convert byte array to float array
        float[] grayscaleFlatFloat = new float[totalPixels];
        for (int i = 0; i < grayscaleBytes.Length; i++)
        {
            grayscaleFlatFloat[i] = (float)grayscaleBytes[i];
        }

        return grayscaleFlatFloat;
    }

    private float CalculateSimilarityScore(float[] imageFlat, float[] variantArray)
    {
        if (imageFlat.Length != variantArray.Length)
        {
            Debug.LogError("Arrays must be of the same length to calculate similarity score.");
            return 0f; // Indicate error or non-comparability
        }

        float diffSum = 0f;

        for (int i = 0; i < imageFlat.Length; i++)
        {
            if (float.IsNaN(imageFlat[i]))
            {
                Debug.LogError("imageFlat IS NAN: " + imageFlat[i] + " at " + i);
                imageFlat[i] = 0;
            }

            if (float.IsNaN(variantArray[i]))
            {
                Debug.LogError("variantArray IS NAN: " + variantArray[i] + " at " + i);
                variantArray[i] = 0;
            }

            // Calculate the absolute difference between corresponding pixels
            diffSum += Mathf.Abs(imageFlat[i] - variantArray[i]);
        }

        // Calculate the mean of the absolute differences
        float meanDiff = diffSum / imageFlat.Length; // Corrected this line
        // Normalize the mean difference to be between 0 and 1, where 1 means identical
        float similarity = 1 - ((float)meanDiff / 255f);

        return similarity;
    }


    // Adjusts the variant in the database to better match the new input
    private void AdjustVariant(float[] imageFlat, string numberKey, int variantIndex)
    {
        if (db.ContainsKey(numberKey) && db[numberKey].Count > variantIndex)
        {
            float[] variantArray = db[numberKey][variantIndex].Image;
            float[] adjustedVariant = new float[imageFlat.Length];

            for (int i = 0; i < imageFlat.Length; i++)
            {
                adjustedVariant[i] = (float)((variantArray[i] + imageFlat[i]) / 2);
            }

            db[numberKey][variantIndex].Image = adjustedVariant;
            Debug.Log($"Adjusted variant for {numberKey} to better match new input.");
        }
    }

    // Updates the database with the new image, creating a new entry if necessary
    public void UpdateDB(Mat image, string number)
    {
        float updateThreshold = VariantThreshold;


        float[] imageFlat = FlattenImage(image); // Assuming FlattenImage is defined elsewhere
        (string closestNumber, float bestSimilarity, int variantIndex) = RecognizeNumber(image);

        if (bestSimilarity < updateThreshold)
        {
            if (!db.ContainsKey(number))
            {
                db[number] = new List<PatternVariant>();
                Debug.Log($"Creating new entry for {number} in database.");
            }

            db[number].Add(new PatternVariant { Image = imageFlat });
            Debug.Log($"Adding new variant for {number}.");
        }
        else if (bestSimilarity < 1 && variantIndex != -1)
        {
            AdjustVariant(imageFlat, number, variantIndex);
        }
        else
        {
            Debug.Log($"Existing variant of {number} is similar enough; not adding a new variant.");
        }

        SaveDB(); // Assuming SaveDB is defined elsewhere to serialize the db to a file
    }


    // Struct or class to hold pattern variants in the database
    public class PatternVariant
    {
        public float[] Image; // This will need to be adapted based on how you choose to store image data
    }

    public string DBInfo()
    {
        string info = "";
        foreach (KeyValuePair<string,List<PatternVariant>> keyValuePair in db)
        {
            info += keyValuePair.Key + "=" + keyValuePair.Value.Count + "; ";
        }
        return info;
    }
}