using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Runtime performance/thermal guardrails. Runs automatically before the first
/// scene loads (no scene wiring needed). The frame-rate cap is the single biggest
/// fix for heat on fanless laptops (e.g. M4 MacBook Air): uncapped, Unity renders
/// as fast as the GPU allows and pins it at 100%. On the Mac build it also trims
/// GPU cost (render scale / MSAA / shadow distance). Editor and other platforms
/// keep full quality.
/// </summary>
public static class PerformanceBoot
{
    // Change this to raise/lower the cap (0 = uncapped). 60 matches typical panels.
    private const int TargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        // vSync must be 0 for targetFrameRate to take effect.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;

        // On a Mac player build (e.g. fanless M4 Air) trim GPU load further. Guarded
        // to OSXPlayer so the editor and the Windows build keep full quality, and so
        // we never persist these onto the shared URP asset in the editor.
        if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale     = 0.85f; // render at 85%, upscale — large GPU saving
                urp.msaaSampleCount = 1;     // MSAA off
                urp.shadowDistance  = 35f;   // shorter shadow range = fewer shadow draws
            }
        }
    }
}
