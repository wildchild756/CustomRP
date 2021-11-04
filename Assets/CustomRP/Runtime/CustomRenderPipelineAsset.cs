using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool useDynamicBatching = false, useGPUInstancing = true, useSRPBatcher = true;

    protected override RenderPipeline CreatePipeline()
    {
        // Debug.Log("CreatePipeline");
        return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher);
    }
}
