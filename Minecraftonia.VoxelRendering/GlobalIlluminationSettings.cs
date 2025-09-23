using System.Numerics;

namespace Minecraftonia.VoxelRendering;

public readonly record struct GlobalIlluminationSettings(
    bool Enabled = true,
    int DiffuseSampleCount = 6,
    int BounceCount = 1,
    float MaxDistance = 18f,
    float Strength = 1.1f,
    float SkyContribution = 0.9f,
    float OcclusionStrength = 1.0f,
    float DistanceAttenuation = 0.24f,
    float ShadowBias = 0.00075f,
    float SunShadowSoftness = 0.65f,
    float SunMaxDistance = 70f,
    Vector3 SunDirection = default,
    Vector3 SunColor = default,
    float SunIntensity = 1.45f,
    Vector3 AmbientLight = default,
    bool UseBentNormalForAmbient = true,
    int MaxSecondarySteps = 96,
    bool EnableSunVisibilityCache = true,
    bool EnableIrradianceCache = true,
    float TemporalBlendFactor = 0.35f,
    int AdaptiveMinSamples = 4,
    float AdaptiveStartDistance = 20f,
    float AdaptiveEndDistance = 64f)
{
    public static GlobalIlluminationSettings Default => new(
        Enabled: true,
        DiffuseSampleCount: 6,
        BounceCount: 1,
        MaxDistance: 18f,
        Strength: 1.1f,
        SkyContribution: 0.9f,
        OcclusionStrength: 1.0f,
        DistanceAttenuation: 0.24f,
        ShadowBias: 0.00075f,
        SunShadowSoftness: 0.65f,
        SunMaxDistance: 70f,
        SunDirection: Vector3.Normalize(new Vector3(-0.35f, 0.88f, 0.25f)),
        SunColor: new Vector3(1.08f, 1.0f, 0.86f),
        SunIntensity: 1.45f,
        AmbientLight: new Vector3(0.16f, 0.19f, 0.24f),
        UseBentNormalForAmbient: true,
        MaxSecondarySteps: 96,
        EnableSunVisibilityCache: true,
        EnableIrradianceCache: true,
        TemporalBlendFactor: 0.35f,
        AdaptiveMinSamples: 4,
        AdaptiveStartDistance: 20f,
        AdaptiveEndDistance: 64f);
}
