#ifndef HIGH_STAKES_OLD_QUARTER_ATMOSPHERE_INCLUDED
#define HIGH_STAKES_OLD_QUARTER_ATMOSPHERE_INCLUDED

float AtmosphereNoise(float3 position)
{
    float3 cell = floor(position);
    float3 blend = frac(position);
    blend = blend * blend * (3.0 - 2.0 * blend);

    float seed = dot(cell, float3(1.0, 57.0, 113.0));
    float4 corners = float4(seed, seed + 1.0, seed + 57.0, seed + 58.0);
    float4 lower = frac(sin(corners) * 43758.5453);
    float4 upper = frac(sin(corners + 113.0) * 43758.5453);
    float4 values = lerp(lower, upper, blend.z);
    return lerp(lerp(values.x, values.y, blend.x), lerp(values.z, values.w, blend.x), blend.y);
}

float3 OldQuarterSkyColor(float3 direction, float3 sunDirection, float3 fogColor)
{
    float altitude = saturate(direction.y);
    float sunFacing = saturate(dot(direction, sunDirection));
    float sunlight = pow(sunFacing, 6.0);
    float3 horizon = lerp(fogColor, float3(0.41, 0.54, 0.63), 0.75);
    horizon = lerp(horizon, float3(0.79, 0.56, 0.40), sunlight * 0.55);
    float3 sky = lerp(horizon, float3(0.095, 0.25, 0.46), sqrt(altitude));

    // Project view directions onto a cloud layer so the clouds stay fixed as the player moves.
    float2 cloudPlane = direction.xz * float2(2.6, 3.7) / (0.22 + altitude);
    float3 cloudPoint = float3(cloudPlane, 3.4);
    float cloudNoise = AtmosphereNoise(cloudPoint) * 0.64
        + AtmosphereNoise(cloudPoint * 2.05) * 0.24
        + AtmosphereNoise(cloudPoint * 4.12) * 0.12;
    float cloudCover = smoothstep(0.48, 0.73, cloudNoise) * smoothstep(0.025, 0.20, altitude);
    float3 cloudLight = lerp(float3(0.70, 0.78, 0.84), float3(0.94, 0.76, 0.57), sunlight);
    sky = lerp(sky, cloudLight, cloudCover * 0.80);

    float sunHalo = pow(sunFacing, 38.0) * 0.24;
    float sunDisc = smoothstep(0.99945, 0.99972, sunFacing) * (1.0 - cloudCover);
    sky += float3(1.0, 0.68, 0.33) * (sunHalo + sunDisc * 1.4);
    return lerp(fogColor, sky, smoothstep(-0.09, 0.025, direction.y));
}

#endif
