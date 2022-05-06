using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();
    bool useDynamicBatching;
    bool useGPUInstancing;
    bool useLightsPerObject;
    bool allowHDR;
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;
    int colorLUTResolution;


    public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution)
    {
        this.allowHDR = allowHDR;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        this.shadowSettings = shadowSettings;
        this.postFXSettings = postFXSettings;
        this.colorLUTResolution = colorLUTResolution;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach(Camera camera in cameras)
        {
            renderer.Render(context, camera, allowHDR, useDynamicBatching, useGPUInstancing, useLightsPerObject, shadowSettings, postFXSettings, colorLUTResolution);
        }
    }
}
