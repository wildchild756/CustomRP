using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    /// <summary>
    /// 渲染阴影的CommandBuffer名
    /// </summary>
    const string bufferName = "Shadows";
    /// <summary>
    /// 产生投影的平行光最大数量
    /// </summary>
    const int maxShadowedDirectionalLightCount = 4;
    /// <summary>
    /// 产生投影的local light最大数量
    /// </summary>
    const int maxShadowedOtherLightCount = 16;
    /// <summary>
    /// 平行光最大级联数
    /// </summary>
    const int maxCascades = 4;
    /// <summary>
    /// 平行光阴影atlas图 ID
    /// </summary>
    /// <returns>int</returns>
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    /// <summary>
    /// local light阴影atlas图 ID
    /// </summary>
    /// <returns>int</returns>
    static int otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas");
    static int otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices");
    static int otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");
    /// <summary>
    /// 平行光级联阴影数 ID 无平行光时为0
    /// </summary>
    /// <returns>int</returns>
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    static int cascadeDataId = Shader.PropertyToID("_CascadeData");
    static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    /// <summary>
    /// 计算级联阴影fadeout所需数据 ID
    /// </summary>
    /// <returns>int</returns>
    static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    /// <summary>
    /// 是否开启shadow pancaking（投影物体顶点在相机近裁剪面背后时移动顶点到近裁剪面） ID
    /// </summary>
    /// <returns>int</returns>
    static int shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");
    /// <summary>
    /// 所有平行光的级联阴影剔除球体位置和半径数组
    /// xyz：球体位置
    /// w：球体半径
    /// </summary>
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];
    /// <summary>
    /// 存储平行光级联阴影的级联信息
    /// x：1/级联剔除球体半径
    /// y：应用深度偏移和纹理滤波后的纹素大小
    /// zw：0
    /// </summary>
    static Vector4[] cascadeData = new Vector4[maxCascades];
    /// <summary>
    /// local light逐tile的数据
    /// x：该tile左上角在atlas图上水平方向位置 分数
    /// y：该tile左上角在atlas图上垂直方向位置 分数
    /// z：该tile在atlas图上边长 分数
    /// w：该tile的最终深度偏移量
    /// </summary>
    static Vector4[] otherShadowTiles = new Vector4[maxShadowedOtherLightCount];
    /// <summary>
    /// 产生投影的平行光投影矩阵数组 ID 世界空间转光源空间
    /// </summary>
    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];
    /// <summary>
    /// 产生投影的local light的投影矩阵数组 ID 世界空间转光源空间
    /// </summary>
    static Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];
    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF7",
    };
    static string[] otherFilterKeywords = {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };
    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };
    static string[] shadowMaskKeywords = {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };
    CommandBuffer buffer = new CommandBuffer{
        name = bufferName
    };
    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings settings;
    /// <summary>
    /// shadow atlas图大小 ID
    /// x：平行光shadow atlas图边长 单位：像素
    /// y：1/x
    /// z：local light shadow atlas图边长 单位：像素
    /// w：1/z
    /// </summary>
    Vector4 atlasSizes;
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopScaleBias;
        public float nearPlaneOffset;
    }
    ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopScaleBias;
        public float normalBias;
        public bool isPoint;
    }
    ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowedOtherLightCount];
    int shadowedDirectionalLightCount;
    int shadowedOtherLightCount;
    bool useShadowMask;

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        shadowedDirectionalLightCount = 0;
        shadowedOtherLightCount = 0;
        useShadowMask = false;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    /// <summary>
    /// 配置平行光阴影数据
    /// </summary>
    /// <param name="light">当前可见平行光结构体</param>
    /// <param name="visibleLightIndex">当前渲染的平行光在所有需要渲染的平行光中的编号</param>
    /// <returns>Vector4 x：当前平行光的阴影强度 y：当前平行光的第一级cascade tile在shadow atlas的编号 z:当前平行光深度偏移 w：当前平行光在shadow atlas图的通道</returns>
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if(shadowedDirectionalLightCount < maxShadowedDirectionalLightCount && light.shadows != LightShadows.None && light.shadowStrength > 0.0f)
        {
            float maskChannel = -1;
            LightBakingOutput lightBaking = light.bakingOutput;
            if(lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            if(!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                return new Vector4(light.shadowStrength, 0f, 0f, maskChannel);
            }
            
            ShadowedDirectionalLights[shadowedDirectionalLightCount] = new ShadowedDirectionalLight{
                visibleLightIndex = visibleLightIndex,
                slopScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
            return new Vector4(light.shadowStrength, settings.directional.cascadeCount * shadowedDirectionalLightCount++, light.shadowNormalBias, maskChannel);
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }

    /// <summary>
    /// 配置local light阴影数据
    /// </summary>
    /// <param name="light">当前可见local light的结构体</param>
    /// <param name="visibleLightIndex">当前渲染的local light在所有需要渲染的local light中的编号</param>
    /// <returns>Vector4 x：当前local light的阴影强度 y：当前local light tile在shadow atlas图中的编号 z:是否为点光源 w：当前local light在shadow atlas图的通道</returns>
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if(light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }

        float maskChannel = -1f;
        LightBakingOutput lightBaking = light.bakingOutput;
        if(lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        bool isPoint = light.type == LightType.Point;
        int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
        if(newLightCount >= maxShadowedOtherLightCount || !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight{
            visibleLightIndex = visibleLightIndex,
            slopScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint
        };

        Vector4 data = new Vector4(light.shadowStrength, shadowedOtherLightCount++, isPoint ? 1f : 0f, maskChannel);
        shadowedOtherLightCount = newLightCount;
        return data;
    }

    //阴影渲染入口
    public void Render()
    {
        if(shadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
        if(shadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }

        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
        buffer.SetGlobalInt(
			cascadeCountId,
			shadowedDirectionalLightCount > 0 ? settings.directional.cascadeCount : 0
		);
		float f = 1f - settings.directional.cascadeFade;
		buffer.SetGlobalVector(
			shadowDistanceFadeId, new Vector4(
				1f / settings.maxDistance, 1f / settings.distanceFade,
				1f / (1f - f * f)
			)
		);
        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染所有平行光阴影
    /// </summary>
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = shadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        for(int i = 0; i < shadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染local light阴影
    /// </summary>
    void RenderOtherShadows()
    {
        int atlasSize = (int)settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;
        buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = shadowedOtherLightCount;
        //shadow atlas在水平和垂直方向等分为几分 1 2 4
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        //shadow atlas中一个tile的边长 单位为像素
        int tileSize = atlasSize / split;
        for(int i = 0; i < shadowedOtherLightCount;)
        {
            if(shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }
        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
        SetKeywords(otherFilterKeywords, (int)settings.other.filter - 1);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染某个平行光阴影
    /// </summary>
    /// <param name="index">当前渲染的平行光在所有需要渲染的平行光中的编号</param>
    /// <param name="split">平行光shadow atlas边长等分分数 1 1/2 1/4</param>
    /// <param name="tileSize">平行光shadow atlas单个tile的边长 单位：像素</param>
    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
        float tileScale = 1f / split;

        for(int i = 0; i < cascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;
            if(index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }
            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), tileScale
            );
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    /// <summary>
    /// 渲染某个spot light阴影
    /// </summary>
    /// <param name="index">当前spot light在所有渲染的local light中的编号</param>
    /// <param name="split">local light shadow atlas水平或垂直等分分数 1 1/2 1/4</param>
    /// <param name="tileSize">local light shadow atlas单个tile边长 单位：像素</param>
    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.visibleLightIndex, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
        shadowSettings.splitData = splitData;
        //spot light因为是透视投影 shadowmap上一纹素在世界空间中的大小不定 与离光源的距离成正比 texelSize表示shadowmap一纹素在世界空间中的大小
        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        Vector2 offset = SetTileViewport(index, split, tileSize);
        //shadow atlas水平或垂直等分数 1 1/2 1/4
        float tileScale = 1f / split;
        SetOtherTileData(index, offset, tileScale, bias);
        otherShadowMatrices[index] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }

    /// <summary>
    /// 渲染某个point light阴影
    /// </summary>
    /// <param name="index">当前point light在所有渲染的local light中的编号</param>
    /// <param name="split">local light shadow atlas水平或垂直等分分数 1 1/2 1/4</param>
    /// <param name="tileSize">local light shadow atlas单个tile边长 单位：像素</param>
    void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / split;
        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        for(int i = 0; i < 6; i++)
        {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(light.visibleLightIndex, (CubemapFace)i, fovBias, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;
            shadowSettings.splitData = splitData;
            int tileIndex = index + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize); 
            SetOtherTileData(tileIndex, offset, tileScale, bias);
            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    /// <summary>
    /// 填充cascadeCullingSpheres和cascadeData数据
    /// </summary>
    /// <param name="index">当前渲染的平行光在所有需要渲染的平行光中的编号</param>
    /// <param name="cullingSphere">级联阴影剔除球体位置xyz和半径w</param>
    /// <param name="tileSize">平行光shadow atlas图中每个tile的边长 单位：像素</param>
    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }

    /// <summary>
    /// 填充local light的tile相关数据 用于将采样clamp在tile内
    /// border为atlas图上半个像素分数
    /// </summary>
    /// <param name="index">当前spot light在所有渲染的local light中的编号</param>
    /// <param name="offset">local light的tile在atlas图第几列第几行</param>
    /// <param name="scale">atlas图水平或垂直分数</param>
    /// <param name="bias">最终的深度偏移量</param>
    void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        float border = atlasSizes.w * 0.5f;
        Vector4 data;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
    }

    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for(int i = 0; i < keywords.Length; i++)
        {
            if(i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    /// <summary>
    /// 设置第index个local light在shadow atlas图中的viewport
    /// </summary>
    /// <param name="index">当前渲染的local light在所有需要渲染的local light中的编号</param>
    /// <param name="split">shadow atlas水平或垂直被等分为几分 1 2 4</param>
    /// <param name="tileSize">shadow atlas中每个tile的边长 单位像素</param>
    /// <returns>Vector2 x：第几列 y：第几行</returns>
    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
    {
        if(SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
            m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
            m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
            m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
            m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
            m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
            m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
            m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
            m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
            m.m20 = 0.5f * (m.m20 + m.m30);
            m.m21 = 0.5f * (m.m21 + m.m31);
            m.m22 = 0.5f * (m.m22 + m.m32);
            m.m23 = 0.5f * (m.m23 + m.m33);
        }
        return m;
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        if(shadowedOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }
        ExecuteBuffer();
    }
}
