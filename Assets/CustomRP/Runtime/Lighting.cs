using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting {

	const string bufferName = "Lighting";

	const int maxDirLightCount = 4, maxOtherLightCount = 64;

	static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";
	static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
	static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
	static int dirLightDirectionsAndMasksId = Shader.PropertyToID("_DirectionalLightDirectionsAndMasks");
	static int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
	static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
	static Vector4[] dirLightDirectionsAndMasks = new Vector4[maxDirLightCount];
	/// <summary>
	/// 平行光阴影数据 数组
	/// x：当前平行光的阴影强度 
	/// y：当前平行光的第一级cascade tile在shadow atlas的编号 
	/// z:当前平行光深度偏移 
	/// w：当前平行光在shadow atlas图的通道
	/// </summary>
	static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];
	static int otherLightCountId = Shader.PropertyToID("_OtherLightCount");
	static int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
	static int otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
	static int otherLightDirectionsAndMasksId = Shader.PropertyToID("_OtherLightDirectionsAndMasks");
	static int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
	static int otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");
	static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightDirectionsAndMasks = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
	/// <summary>
	/// local light阴影数据 数组
	/// x：当前local light的阴影强度 
	/// y：当前local light tile在shadow atlas图中的编号 
	/// z:是否为点光源 
	/// w：当前local light在shadow atlas图的通道
	/// </summary>
	static Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];

	CommandBuffer buffer = new CommandBuffer 
    {
		name = bufferName
	};

	CullingResults cullingResults;

	Shadows shadows = new Shadows();

	public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask)
    {
		this.cullingResults = cullingResults;
		buffer.BeginSample(bufferName);
		shadows.Setup(context, cullingResults, shadowSettings);
		SetupLights(useLightsPerObject, renderingLayerMask);
		shadows.Render();
		buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	public void Cleanup() 
    {
		shadows.Cleanup();
	}

	void SetupLights(bool useLightsPerObject, int renderingLayerMask) 
    {
		NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		int dirLightCount = 0;
        int otherLightCount = 0;
		int i;
		for(i = 0; i < visibleLights.Length; i++)
        {
			int newIndex = -1;
			VisibleLight visibleLight = visibleLights[i];
			Light light = visibleLight.light;
			if((light.renderingLayerMask & renderingLayerMask) != 0)
			{
				switch(visibleLight.lightType)
				{
					case LightType.Directional:
						if(dirLightCount < maxDirLightCount)
						{
							SetupDirectionalLight(dirLightCount++, i, ref visibleLight, light);
						}
						break;
					case LightType.Point:
						if(otherLightCount < maxOtherLightCount)
						{
							newIndex = otherLightCount;
							SetupPointLight(otherLightCount++, i, ref visibleLight, light);
						}
						break;
					case LightType.Spot:
						if(otherLightCount < maxOtherLightCount)
						{
							newIndex = otherLightCount;
							SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
						}
						break;
				}
			}
			if(useLightsPerObject)
            {
				indexMap[i] = newIndex;
			}
		}

		if(useLightsPerObject)
        {
			for(; i < indexMap.Length; i++)
            {
				indexMap[i] = -1;
			}
			cullingResults.SetLightIndexMap(indexMap);
			indexMap.Dispose();
			Shader.EnableKeyword(lightsPerObjectKeyword);
		}
		else
        {
			Shader.DisableKeyword(lightsPerObjectKeyword);
		}

		buffer.SetGlobalInt(dirLightCountId, dirLightCount);
		if(dirLightCount > 0)
        {
			buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
			buffer.SetGlobalVectorArray(dirLightDirectionsAndMasksId, dirLightDirectionsAndMasks);
			buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
		}

		buffer.SetGlobalInt(otherLightCountId, otherLightCount);
		if(otherLightCount > 0)
        {
			buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
			buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
			buffer.SetGlobalVectorArray(otherLightDirectionsAndMasksId, otherLightDirectionsAndMasks);
			buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
			buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
		}
	}

	void SetupDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
		dirLightColors[index] = visibleLight.finalColor;
		Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
		dirLightDirectionsAndMasks[index] = dirAndMask;
		dirLightShadowData[index] = shadows.ReserveDirectionalShadows(light, visibleIndex);
	}

	void SetupPointLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
		otherLightColors[index] = visibleLight.finalColor;
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		otherLightSpotAngles[index] = new Vector4(0f, 1f);
		Vector4 dirAndMask = Vector4.zero;
		dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
		otherLightDirectionsAndMasks[index] = dirAndMask;
		otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
	}

	void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
		otherLightColors[index] = visibleLight.finalColor;
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
		otherLightDirectionsAndMasks[index] = dirAndMask;

		float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
		float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
		float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
		otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
		otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
	}
}