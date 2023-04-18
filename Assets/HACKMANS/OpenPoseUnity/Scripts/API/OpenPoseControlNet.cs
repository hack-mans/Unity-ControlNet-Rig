using Newtonsoft.Json;
using System;
using System.Collections;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using Network = ru.mofrison.Unity3D.Network;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Component to help generate a Stable Diffusion image using OpenPose ControlNet
/// </summary>
[ExecuteAlways]
public class OpenPoseControlNet : StableDiffusionGenerator
{
    [Header("Requirements")] 
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private RawImage uiImage;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private RenderTexture tempRenderTexture;

    [Header("Setup")]
    [SerializeField] private string saveFolder = "OutputImages";
    [SerializeField] private float updateFrequency = 0.4f;
    
    [Header("Stable Diffusion Properties")]
    [ReadOnly]
    public string guid = "";
    
    private Texture2D inputTexture = null;
    public string prompt;
    public string negativePrompt;
    
    /// <summary>
    /// List of samplers to display as Drop-Down in the inspector
    /// </summary>
    [SerializeField]
    public string[] samplersList
    {
        get
        {
            if (sdc == null)
                sdc = FindObjectOfType<StableDiffusionConfiguration>();
            return sdc.samplers;
        }
    }
    /// <summary>
    /// Actual sampler selected in the drop-down list
    /// </summary>
    [HideInInspector]
    public int selectedSampler = 0;

    // Exposed SD Generation Parameters
    public int steps = 50;
    public float cfgScale = 7;
    public long seed = -1;
    
    public long generatedSeed = -1;

    // Internal Variables
    private string filename = "";
    private string originalFilename = "";
    private string filepath = "";
    private int count = 0;
    private bool autoSave = false;
    private int width;
    private int height;
    private int scaledWidth;
    private int scaledHeight;
    private Texture2D tempTexture = null;
    private Texture2D previewTexture = null;
    private Texture2D currentTexture = null;
    
    /// <summary>
    /// List of models to display as Drop-Down in the inspector
    /// </summary>
    [SerializeField]
    public string[] modelsList
    {
        get
        {
            if (sdc == null)
                sdc = FindObjectOfType<StableDiffusionConfiguration>();
            return sdc.modelNames;
        }
    }
    /// <summary>
    /// Actual model selected in the drop-down list
    /// </summary>
    [HideInInspector]
    public int selectedModel = 0;
    
    
    /// <summary>
    /// On Awake, fill the properties with default values from the selected settings.
    /// </summary>
    void Awake()
    {
        StableDiffusionConfiguration sdc = GameObject.FindObjectOfType<StableDiffusionConfiguration>();
    }

    private void Start()
    {
        PrepareData();
        SetupFolders();
    }

    void PrepareData()
    {
        // Initialise Textures for storing previews and final images
        previewTexture = new Texture2D(2, 2);
        currentTexture = new Texture2D(2, 2);
    }

    /// <summary>
    /// Loop update
    /// </summary>
    void Update()
    {
#if UNITY_EDITOR
        // If not setup already, generate a GUID (Global Unique Identifier)
        if (guid == "")
            guid = Guid.NewGuid().ToString();
    }
#endif

    // Internally keep tracking if we are currently generating (prevent re-entry)
    bool generating = false;

    /// <summary>
    /// Callback function for the inspector Generate button.
    /// </summary>
    public void Generate()
    { 
        // Start generation asynchronously
        if (!generating && !string.IsNullOrEmpty(prompt))
        {
            StartCoroutine(GenerateTxt2ImgControlNetAsync());
        }
    }

    /// <summary>
    /// Setup the output path and filename for image generation
    /// </summary>
    void SetupFolders()
    {
        // Get the configuration settings
        if (sdc == null)
            sdc = GameObject.FindObjectOfType<StableDiffusionConfiguration>();

        try
        {
            // Determine output path
            string root = Application.dataPath;
            string directory = Path.Combine(root, saveFolder);
            filename = "tempname.png";
            // originalFilename = filename;
            filepath = Path.Combine(directory, filename);

            // Create folders if they don't exist
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            // Refresh Assets
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n\n" + e.StackTrace);
        }
    }

    public void SetWidthHeight(int newWidth, int newHeight, int newScaledWidth, int newScaledHeight)
    {
        width = newWidth;
        height = newHeight;
        scaledWidth = newScaledWidth;
        scaledHeight = newScaledHeight;
    }

    string ConvertRenderTexture()
    {
        if (tempTexture != null)
        {
            Destroy(tempTexture);
        }
        tempTexture = new Texture2D(scaledWidth, scaledHeight, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        Rect regionToReadFrom = new Rect((renderTexture.width-scaledWidth)*0.5f,(renderTexture.height-scaledHeight)*0.5f, scaledWidth, scaledHeight);
        tempTexture.ReadPixels(regionToReadFrom, 0, 0);
        tempTexture.Apply();

        byte[] inputImgBytes = tempTexture.EncodeToPNG();
        string inputImgString = System.Convert.ToBase64String(inputImgBytes);

        return inputImgString;
    }

    /// <summary>
    /// Main Generation Function that sends API request
    /// </summary>
    IEnumerator GenerateTxt2ImgControlNetAsync()
    {
        generating = true;

        // Set the model parameters
        yield return sdc.SetModelAsync(modelsList[selectedModel]);
        
        // Generate the image
        HttpWebRequest httpWebRequest = null;
        try
        {
            // Make a HTTP POST request to the Stable Diffusion server
            httpWebRequest = (HttpWebRequest)WebRequest.Create(sdc.settings.StableDiffusionServerURL + sdc.settings.TextToImageAPI);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            // Send the generation parameters along with the POST request
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string inputImgString = ConvertRenderTexture();

                // Control Net Arguments
                Args args = new Args();
                args.input_image = inputImgString;
                args.module = "none";
                args.model = "control_sd15_openpose [fef5e48e]";
                args.weight = 1;
                args.processor_res = 512;
                args.guidance = 1;
                args.guidance_start = 0;
                args.guidance_end = 1;
                
                ControlNet controlNet = new ControlNet();
                controlNet.args = new Args[1] { args };

                AlwaysOnScripts alwaysOnScripts = new AlwaysOnScripts();
                alwaysOnScripts.ControlNet = controlNet;
                
                // ControlNet txt2img Params
                SDParamsInTxt2Img sd = new SDParamsInTxt2Img();
                sd.prompt = prompt;
                sd.negative_prompt = negativePrompt;
                sd.steps = steps;
                sd.cfg_scale = cfgScale;
                sd.width = width;
                sd.height = height;
                sd.seed = seed;
                sd.tiling = false;
                sd.alwayson_scripts = alwaysOnScripts;

                if (selectedSampler >= 0 && selectedSampler < samplersList.Length)
                    sd.sampler_name = samplersList[selectedSampler];

                // Serialize the input parameters
                string json = JsonConvert.SerializeObject(sd);

                Debug.Log(json);
                
                // Send to the server
                streamWriter.Write(json);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n\n" + e.StackTrace);
        }

        // Read the output of the generation and display progress
        if (httpWebRequest != null)
        {
            // Wait until the generation is complete before proceeding
            Task<WebResponse> webResponse = httpWebRequest.GetResponseAsync();

            while (!webResponse.IsCompleted)
            {
                LivePreviewProgressData();

                yield return new WaitForSeconds(updateFrequency);
            }
            
            // Stream the result from the server
            var httpResponse = webResponse.Result;

            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                // Decode the response as a JSON string
                string result = streamReader.ReadToEnd();
                
                // Deserialize the JSON string into a data structure
                SDResponseTxt2Img json = JsonConvert.DeserializeObject<SDResponseTxt2Img>(result);
                
                // If no image, there was probably an error so abort
                if (json.images == null || json.images.Length == 0)
                {
                    Debug.LogError("No image was returned by the server. This should not happen. Verify that the server is correctly setup");

                    generating = false;
                    yield break;
                }
                
                // Read back the image into a texture and send to UI
                byte[] imageData = Convert.FromBase64String(json.images[0]);

                try
                {
                    // Load the byte data into the texture
                    currentTexture.LoadImage(imageData);
            
                    // Load the texture into the UI Image
                    LoadIntoUI(currentTexture);
                    
                    // Read the generation info back (only seed should have changed)
                    if (json.info != "")
                    {
                        SDParamsOutTxt2Img info = JsonConvert.DeserializeObject<SDParamsOutTxt2Img>(json.info);
                    
                        // Read the seed that was used by Stable Diffusion to generate this result
                        generatedSeed = info.seed;
                    }

                    if (autoSave)
                    {
                        SaveImage();
                    }

                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message + "\n\n" + e.StackTrace);
                }
            }
        }
        
        generating = false;
        yield return null;
    }

    public void ToggleAutoSave()
    {
        autoSave = !autoSave;
    }

    public void SaveImage()
    {
        // Check texture is not empty (resolution greater than 512px)
        if (currentTexture.width >= 512)
        {
            // Convert the texture to a PNG byte array
            byte[] bytes = currentTexture.EncodeToPNG();
        
            // Check if a file already exists
            UpdateFileName();
        
            // Write the bytes array to a file
            File.WriteAllBytes(filepath, bytes);
            if (File.Exists(filepath))
            {
                Debug.Log("Texture saved to " + filepath);
            }
        }
        
        AssetDatabase.Refresh();
    }

    private void UpdateFileName()
    {
        // Check texture is not empty (resolution greater than 512px)
        if (currentTexture.width >= 512)
        {
            // Find the index of the last "." in the file name
            int dotIndex = filename.LastIndexOf(".");
            
            // Get the extension of the file
            string extension = filename.Substring(dotIndex);
            
            string name = count.ToString("D4") + "_" + generatedSeed;

            filename = name + ".png";
            filepath = Application.dataPath + "/" + saveFolder + "/" + filename;

            count++;
        }
    }

    /// <summary>
    /// Async task to live preview receive progress data
    /// </summary>
    private async void LivePreviewProgressData()
    {
        // Stable diffusion API url for setting a model
        string url = sdc.settings.StableDiffusionServerURL + sdc.settings.ProgressAPI;
        
        // Send the GET request
        string responseBody = await GetText(url);
        
        // Deserialize the response to a class
        SDProgress sdp = JsonConvert.DeserializeObject<SDProgress>(responseBody);
        
        // Decode the image from Base64 string into an array of bytes
        if (sdp.current_image != null)
        {
            byte[] imageData = Convert.FromBase64String(sdp.current_image);
            
            // Load the byte data into the texture
            previewTexture.LoadImage(imageData);
            
            // Load the texture into the UI Image
            LoadIntoUI(previewTexture);
        }
    }
    
    private async Task<string> GetText(string url)
    {
        return await Network.GetText(url);
    }

    void LoadIntoUI(Texture2D texture)
    {
        if (uiPanel.activeSelf == false)
        {
            uiPanel.SetActive(true);
        }

        uiImage.texture = texture;
    }
}