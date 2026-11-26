using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Browser-specific setup for the WebGL (itch.io) build. Does nothing on other platforms.
/// - Lets the browser drive the frame rate (GameManager sets 60, which stutters on web).
/// - Saves when the tab is hidden or closed (OnApplicationQuit is unreliable in browsers).
/// - Keeps the portrait-designed screen-space UI readable in a landscape frame.
/// </summary>
public class WebPlatform : MonoBehaviour
{
    public static bool IsMobileBrowser { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void WebPlatform_RegisterPageHide(Action callback);
    [DllImport("__Internal")] private static extern int WebPlatform_IsMobile();

    [AOT.MonoPInvokeCallback(typeof(Action))]
    private static void OnPageHide()
    {
        if (GameManager.Instance != null) GameManager.Instance.ForceImmediateSave();
        PlayerPrefs.Save();
    }

    // Runs after every Awake in the first scene, so it overrides GameManager's targetFrameRate.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        Application.targetFrameRate = -1; // use the browser's requestAnimationFrame timing
        IsMobileBrowser = WebPlatform_IsMobile() == 1;
        WebPlatform_RegisterPageHide(OnPageHide);

        var go = new GameObject("[WebPlatform]");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<WebPlatform>();
    }
#endif

    // The UI was laid out for a 1080x1920 portrait reference. In a landscape window,
    // matching on height keeps every element at the same size relative to screen height
    // as on the phone, so nothing grows taller than the screen.
    private const float LandscapeMatch = 1f;
    private const float CheckInterval = 0.5f;

    private readonly Dictionary<CanvasScaler, float> originalMatch = new Dictionary<CanvasScaler, float>();
    private int lastWidth, lastHeight, lastScalerCount = -1;
    private float nextCheck;

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckInterval;

        var scalers = FindObjectsOfType<CanvasScaler>(true);
        if (Screen.width == lastWidth && Screen.height == lastHeight && scalers.Length == lastScalerCount) return;
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastScalerCount = scalers.Length;

        bool landscape = Screen.width > Screen.height;
        foreach (var scaler in scalers)
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            var canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;

            if (!originalMatch.ContainsKey(scaler)) originalMatch[scaler] = scaler.matchWidthOrHeight;
            bool portraitReference = scaler.referenceResolution.y > scaler.referenceResolution.x;
            scaler.matchWidthOrHeight = landscape && portraitReference ? LandscapeMatch : originalMatch[scaler];
        }
    }
}
