using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField]
    Shader shader = default;

    [System.NonSerialized]
    Material material;

    public BloomSettings Bloom => bloom;

    public Material Material
    {
        get
        {
            if(material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            return material;
        }
    }

    [Serializable]
    public struct BloomSettings
    {
        public bool ignoreRenderScale;

        [Range(0f, 16f)]
        public int maxIterations;

        [Min(1f)]
        public int downScaleLimit;

        public bool bicubicUpsampling;

        [Min(0f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;

        [Min(0f)]
        public float intensity;

        public bool fadeFireflies;

        public enum Mode
        {
            Additive,
            Scattering
        }
        public Mode mode;

        [Range(0.05f, 0.95f)]
        public float scatter;
    }

    [SerializeField]
    BloomSettings bloom = new BloomSettings
    {
        scatter = 0.7f
    };

    [Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None,
            ACES,
            Neutral,
            Reinhard,
        }

        public Mode mode;
    }

    [SerializeField]
    ToneMappingSettings toneMapping = default;
    public ToneMappingSettings ToneMapping => toneMapping;

    [Serializable]
    public struct ColorAdjustmentSettings
    {
        public float postExposure;

        [Range(-100f, 100f)]
        public float contrast;

        [ColorUsage(false, true)]
        public Color colorFilter;

        [Range(-180f, 180f)]
        public float hueShift;

        [Range(-100f, 100f)]
        public float saturation;
    }

    [SerializeField]
    ColorAdjustmentSettings colorAdjustments = new ColorAdjustmentSettings{
        colorFilter = Color.white
    };
    
    public ColorAdjustmentSettings ColorAdjustments => colorAdjustments;

    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f)]
        public float temperature, tint;
    }

    [SerializeField]
    WhiteBalanceSettings whiteBalance = default;

    public WhiteBalanceSettings WhiteBalance => whiteBalance;

    [Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)]
        public Color shadows;
        public Color highlights;

        [Range(-100f, 100f)]
        public float balance;
    }

    [SerializeField]
    SplitToningSettings splitToning = new SplitToningSettings{
        shadows = Color.gray,
        highlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;

    [Serializable]
    public struct ChannelMixerSettings
    {
        public Vector3 red;
        public Vector3 green;
        public Vector3 blue;
    }

    [SerializeField]
    ChannelMixerSettings channelMixer = new ChannelMixerSettings{
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    public ChannelMixerSettings ChannelMixer => channelMixer;

    [Serializable]
    public struct ShadowsMidtonesHighlightsSettings
    {
        [ColorUsage(false, true)]
        public Color shadows;

        [ColorUsage(false, true)]
        public Color midtones;

        [ColorUsage(false, true)]
        public Color highlights;

        [Range(0f, 2f)]
        public float shadowsStart;

        [Range(0f, 2f)]
        public float shadowsEnd;

        [Range(0f, 2f)]
        public float highlightsStart;

        [Range(0f, 2f)]
        public float highlightsEnd;
    }

    [SerializeField]
    ShadowsMidtonesHighlightsSettings shadowsMidtonesHighlights = new ShadowsMidtonesHighlightsSettings{
        shadows = Color.white,
        midtones = Color.white,
        highlights = Color.white,
        shadowsEnd = 0.3f,
        highlightsStart = 0.55f,
        highlightsEnd = 1f
    };

    public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights => shadowsMidtonesHighlights;
}
