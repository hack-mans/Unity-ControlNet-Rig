using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OpenPoseUI : MonoBehaviour
{
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private GameObject posePreviewPanel;
    [SerializeField] private RectTransform previewMaskedCrop;
    [SerializeField] private RectTransform maskedCrop;
    [SerializeField] private RectTransform imageCrop;
    [SerializeField] private Slider widthSlider;
    [SerializeField] private TextMeshProUGUI widthValue;
    [SerializeField] private Slider heightSlider;
    [SerializeField] private TextMeshProUGUI heightValue;
    [SerializeField] private OpenPoseControlNet openPoseControlNet;

    private Vector2 originalSize;
    private Vector2 scaledSize;
    private int width = 512;
    private int height = 768;

    void Start()
    {
        originalSize = maskedCrop.sizeDelta;

        uiPanel.SetActive(false);
        posePreviewPanel.SetActive(false);
        widthSlider.onValueChanged.AddListener(delegate { WidthSliderUpdate(); });
        heightSlider.onValueChanged.AddListener(delegate { HeightSliderUpdate(); });
        
        ResizeToFitScreen();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            uiPanel.SetActive(!uiPanel.activeSelf);
        }
    }

    public void WidthSliderUpdate()
    {
        switch (widthSlider.value)
        {
            case 0:
                // min width 512
                width = 512;
                widthValue.text = 512.ToString();
                break;
            case 1:
                width = 640;
                widthValue.text = 640.ToString();
                break;
            case 2:
                width = 768;
                widthValue.text = 768.ToString();
                break;
            case 3:
                width = 896;
                widthValue.text = 896.ToString();
                break;
            case 4:
                // max width 1024
                width = 1024;
                widthValue.text = 1024.ToString();
                break;
        }
        AspectCropUpdate();
    }

    public void HeightSliderUpdate()
    {
        switch (heightSlider.value)
        {
            case 0:
                // min width 512
                height = 512;
                heightValue.text = 512.ToString();
                break;
            case 1:
                height = 640;
                heightValue.text = 640.ToString();
                break;
            case 2:
                height = 768;
                heightValue.text = 768.ToString();
                break;
            case 3:
                height = 896;
                heightValue.text = 896.ToString();
                break;
            case 4:
                // max width 1024
                height = 1024;
                heightValue.text = 1024.ToString();
                break;
        }
        AspectCropUpdate();
    }

    public void AspectCropUpdate()
    {
        originalSize = new Vector2(width, height);
        ResizeToFitScreen();
    }

    public void ResizeToFitScreen()
    {
        // Get the current screen resolution
        Vector2 screenResolution = new Vector2(Screen.width, Screen.height);
        
        // Calculate the ratio between the screen resolution and the original size
        Vector2 ratio = screenResolution / originalSize;
        
        // Determine which side of the rect transform is longer
        bool isWidthLonger = (originalSize.x * ratio.y) < screenResolution.x;
        
        // Set the new size of the rec transform based on the length of the longest side
        scaledSize = isWidthLonger
            ? new Vector2(originalSize.x * ratio.y, screenResolution.y)
            : new Vector2(screenResolution.x, originalSize.y * ratio.x);
        
        // Update maskedCrop and imageCrop sizes
        maskedCrop.sizeDelta = scaledSize;
        imageCrop.sizeDelta = scaledSize;
        previewMaskedCrop.sizeDelta = scaledSize;
        
        // Update width and height in OpenPoseControlNet script
        openPoseControlNet.SetWidthHeight((int)originalSize.x,(int)originalSize.y, (int)scaledSize.x, (int)scaledSize.y);
    }

    public void ShowPosePreview()
    {
        posePreviewPanel.SetActive(!posePreviewPanel.activeSelf);
    }
}
