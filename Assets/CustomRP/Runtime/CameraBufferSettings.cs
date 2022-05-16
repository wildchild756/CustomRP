using UnityEngine;

[System.Serializable]
public struct CameraBufferSettings
{
    public bool allowHDR;
    public bool copyColor;
    public bool copyColorReflections;
    public bool copyDepth;
    public bool copyDepthReflections;

    [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
    public float renderScale;
    public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }
    public BicubicRescalingMode bicubicResaling;
}
