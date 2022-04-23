using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool useDynamicBatching = false;
    bool useGPUInstancing = true;
    bool useSRPBatcher = true;
    bool useLightsPerObject = true;
    [SerializeField]
    ShadowSettings shadows = default;

    protected override RenderPipeline CreatePipeline()
    {
        // Debug.Log("CreatePipeline");
        return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightsPerObject, shadows);
    }
}
