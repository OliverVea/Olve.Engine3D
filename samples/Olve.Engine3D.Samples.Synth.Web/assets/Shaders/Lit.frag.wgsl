struct _MatrixStorage_float4x4std140_0
{
    @align(16) data_0 : array<vec4<f32>, i32(4)>,
};

struct SpacePassData_std140_0
{
    @align(16) view_0 : _MatrixStorage_float4x4std140_0,
    @align(16) projection_0 : _MatrixStorage_float4x4std140_0,
    @align(16) cameraPosition_0 : vec3<f32>,
    @align(16) skyAmbient_0 : vec3<f32>,
    @align(16) groundAmbient_0 : vec3<f32>,
    @align(16) keyPosition_0 : vec3<f32>,
    @align(16) keyColor_0 : vec3<f32>,
    @align(16) light0Position_0 : vec3<f32>,
    @align(16) light0Color_0 : vec3<f32>,
    @align(16) light1Position_0 : vec3<f32>,
    @align(16) light1Color_0 : vec3<f32>,
    @align(16) glowOnly_0 : vec4<f32>,
};

@binding(0) @group(0) var<uniform> spacePass_0 : SpacePassData_std140_0;
fn toLinear_0( c_0 : vec3<f32>) -> vec3<f32>
{
    return pow(max(c_0, vec3<f32>(0.0f)), vec3<f32>(2.2000000476837158f));
}

fn hash_0( p_0 : vec3<f32>) -> f32
{
    var _S1 : vec3<f32> = fract(p_0 * vec3<f32>(0.31830990314483643f) + vec3<f32>(0.10000000149011612f)) * vec3<f32>(17.0f);
    var _S2 : f32 = _S1.x;
    var _S3 : f32 = _S1.y;
    var _S4 : f32 = _S1.z;
    return fract(_S2 * _S3 * _S4 * (_S2 + _S3 + _S4));
}

fn noise_0( x_0 : vec3<f32>) -> f32
{
    var i_0 : vec3<f32> = floor(x_0);
    var f_0 : vec3<f32> = fract(x_0);
    var f_1 : vec3<f32> = f_0 * f_0 * (vec3<f32>(3.0f) - vec3<f32>(2.0f) * f_0);
    var _S5 : f32 = f_1.x;
    var _S6 : f32 = f_1.y;
    return mix(mix(mix(hash_0(i_0), hash_0(i_0 + vec3<f32>(1.0f, 0.0f, 0.0f)), _S5), mix(hash_0(i_0 + vec3<f32>(0.0f, 1.0f, 0.0f)), hash_0(i_0 + vec3<f32>(1.0f, 1.0f, 0.0f)), _S5), _S6), mix(mix(hash_0(i_0 + vec3<f32>(0.0f, 0.0f, 1.0f)), hash_0(i_0 + vec3<f32>(1.0f, 0.0f, 1.0f)), _S5), mix(hash_0(i_0 + vec3<f32>(0.0f, 1.0f, 1.0f)), hash_0(i_0 + vec3<f32>(1.0f, 1.0f, 1.0f)), _S5), _S6), f_1.z);
}

fn whiteLight_0( lightPosition_0 : vec3<f32>,  position_0 : vec3<f32>,  normal_0 : vec3<f32>,  toEye_0 : vec3<f32>,  albedo_0 : vec3<f32>,  shine_0 : f32) -> vec3<f32>
{
    var toLight_0 : vec3<f32> = lightPosition_0 - position_0;
    var distance2_0 : f32 = dot(toLight_0, toLight_0);
    var l_0 : vec3<f32> = toLight_0 * vec3<f32>(inverseSqrt(max(distance2_0, 9.99999997475242708e-07f)));
    var _S7 : f32 = dot(normal_0, l_0);
    var exponent_0 : f32 = mix(12.0f, 80.0f, shine_0);
    return (albedo_0 * vec3<f32>(saturate((_S7 + 0.20000000298023224f) / 1.2000000476837158f)) + vec3<f32>((pow(max(dot(normal_0, normalize(l_0 + toEye_0)), 0.0f), exponent_0) * shine_0 * (exponent_0 + 8.0f) / 160.0f * step(0.0f, _S7)))) * vec3<f32>((1.0f / (1.0f + 0.80000001192092896f * distance2_0)));
}

fn rimLight_0( normal_1 : vec3<f32>,  toEye_1 : vec3<f32>) -> vec3<f32>
{
    return vec3<f32>(0.05000000074505806f, 0.059999998658895493f, 0.090000003576278687f) * vec3<f32>(pow(1.0f - saturate(dot(normal_1, toEye_1)), 3.0f)) * vec3<f32>(saturate(dot(normal_1, normalize(vec3<f32>(0.0f, 0.5f, -1.0f))) * 0.80000001192092896f + 0.20000000298023224f));
}

fn rollOff_0( c_1 : vec3<f32>) -> vec3<f32>
{
    var knee_0 : vec3<f32> = vec3<f32>(0.75f);
    var _S8 : vec3<f32> = max(c_1 - knee_0, vec3<f32>(0.0f));
    return min(c_1, knee_0) + _S8 / (vec3<f32>(1.0f) + _S8 * vec3<f32>(2.0f));
}

fn toSrgb_0( c_2 : vec3<f32>) -> vec3<f32>
{
    return pow(max(c_2, vec3<f32>(0.0f)), vec3<f32>(0.45454543828964233f));
}

struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) worldPosition_0 : vec3<f32>,
    @location(1) localPosition_0 : vec3<f32>,
    @location(2) normal_2 : vec3<f32>,
    @location(3) tangent_0 : vec3<f32>,
    @location(4) albedo_1 : vec4<f32>,
    @location(5) emissive_0 : vec4<f32>,
    @location(6) finish_0 : vec4<f32>,
};

fn shade_0( _S9 : vec3<f32>,  _S10 : vec3<f32>,  _S11 : vec3<f32>,  _S12 : f32,  _S13 : vec3<f32>) -> vec3<f32>
{
    var toEye_2 : vec3<f32> = normalize(spacePass_0.cameraPosition_0 - _S9);
    return _S11 * mix(spacePass_0.groundAmbient_0, spacePass_0.skyAmbient_0, vec3<f32>((_S10.y * 0.5f + 0.5f))) + whiteLight_0(spacePass_0.keyPosition_0, _S9, _S10, toEye_2, _S11, _S12) * spacePass_0.keyColor_0 + whiteLight_0(spacePass_0.light0Position_0, _S9, _S10, toEye_2, _S11, _S12) * spacePass_0.light0Color_0 + whiteLight_0(spacePass_0.light1Position_0, _S9, _S10, toEye_2, _S11, _S12) * spacePass_0.light1Color_0 + _S13;
}

fn environment_0( _S14 : vec3<f32>,  _S15 : vec3<f32>) -> vec3<f32>
{
    var colour_0 : vec3<f32> = mix(spacePass_0.groundAmbient_0, spacePass_0.skyAmbient_0, vec3<f32>((_S15.y * 0.5f + 0.5f))) * vec3<f32>(0.69999998807907104f);
    var ahead_0 : f32 = - _S15.z;
    var colour_1 : vec3<f32>;
    if((ahead_0 > 0.05000000074505806f))
    {
        var w_0 : vec2<f32> = _S15.xy / vec2<f32>(ahead_0);
        var _S16 : f32 = w_0.y;
        colour_1 = colour_0 + vec3<f32>(0.045000001788139343f, 0.041999999433755875f, 0.075000002980232239f) * vec3<f32>((smoothstep(1.1000000238418579f, 0.69999998807907104f, abs(w_0.x)) * smoothstep(-0.25f, 0.0f, _S16) * smoothstep(1.2000000476837158f, 0.80000001192092896f, _S16)));
    }
    else
    {
        colour_1 = colour_0;
    }
    var _S17 : vec3<f32> = vec3<f32>(0.60000002384185791f);
    return colour_1 + spacePass_0.keyColor_0 * vec3<f32>(pow(saturate(dot(_S15, normalize(spacePass_0.keyPosition_0 - _S14))), 60.0f)) * vec3<f32>(0.5f) + spacePass_0.light0Color_0 * vec3<f32>(pow(saturate(dot(_S15, normalize(spacePass_0.light0Position_0 - _S14))), 40.0f)) * _S17 + spacePass_0.light1Color_0 * vec3<f32>(pow(saturate(dot(_S15, normalize(spacePass_0.light1Position_0 - _S14))), 40.0f)) * _S17;
}

fn isGlowPass_0() -> bool
{
    return ((spacePass_0.glowOnly_0.x) > 0.5f);
}

@fragment
fn fragmentMain( _S18 : pixelInput_0, @builtin(position) position_1 : vec4<f32>) -> FragmentOut_0
{
    var normal_3 : vec3<f32> = normalize(_S18.normal_2);
    var albedo_2 : vec3<f32> = toLinear_0(_S18.albedo_1.xyz);
    var grain_0 : f32 = _S18.emissive_0.w;
    var normal_4 : vec3<f32>;
    var albedo_3 : vec3<f32>;
    if((grain_0 > 0.0f))
    {
        var p_1 : vec3<f32> = _S18.localPosition_0 * vec3<f32>(900.0f);
        var tilt_0 : vec3<f32> = vec3<f32>(noise_0(p_1), noise_0(p_1 + vec3<f32>(17.299999237060547f)), noise_0(p_1 + vec3<f32>(41.099998474121094f))) - vec3<f32>(0.5f);
        var albedo_4 : vec3<f32> = albedo_2 * vec3<f32>((1.0f + (noise_0(_S18.localPosition_0 * vec3<f32>(3100.0f)) - 0.5f) * 0.070000000298023224f * grain_0));
        normal_4 = normalize(normal_3 + (tilt_0 - normal_3 * vec3<f32>(dot(tilt_0, normal_3))) * vec3<f32>(0.18000000715255737f) * vec3<f32>(grain_0));
        albedo_3 = albedo_4;
    }
    else
    {
        normal_4 = normal_3;
        albedo_3 = albedo_2;
    }
    var _S19 : bool = ((_S18.finish_0.w) > 0.5f);
    var across_0 : f32;
    if(_S19)
    {
        across_0 = length(_S18.localPosition_0.xz);
    }
    else
    {
        across_0 = _S18.localPosition_0.y;
    }
    var fade_0 : f32 = saturate(1.5f - (fwidth((across_0))) * 1500.0f);
    var toEye_3 : vec3<f32> = normalize(spacePass_0.cameraPosition_0 - _S18.worldPosition_0);
    var _S20 : vec3<f32> = vec3<f32>(0.0f);
    var _S21 : f32 = _S18.finish_0.x;
    var sheen_0 : vec3<f32>;
    var streak_0 : f32;
    if((_S21 > 0.0f))
    {
        var along_0 : f32;
        if(_S19)
        {
            along_0 = atan2(_S18.localPosition_0.z, _S18.localPosition_0.x) * 0.019999999552965164f;
        }
        else
        {
            along_0 = _S18.localPosition_0.x;
        }
        if((fade_0 > 0.0099999997764825821f))
        {
            streak_0 = mix(0.5f, noise_0(vec3<f32>(along_0 * 30.0f, across_0 * 3000.0f, 0.0f)), fade_0);
        }
        else
        {
            streak_0 = 0.5f;
        }
        var albedo_5 : vec3<f32> = albedo_3 * vec3<f32>((1.0f + (streak_0 - 0.5f) * 0.30000001192092896f * _S21));
        var th_0 : f32 = dot(normalize(_S18.tangent_0 + vec3<f32>(9.99999997475242708e-07f, 0.0f, 0.0f)), normalize(normalize(spacePass_0.keyPosition_0 - _S18.worldPosition_0) + toEye_3));
        sheen_0 = spacePass_0.keyColor_0 * vec3<f32>(pow(sqrt(saturate(1.0f - th_0 * th_0)), 90.0f)) * vec3<f32>(0.05000000074505806f) * vec3<f32>(_S21) * vec3<f32>((0.60000002384185791f + 0.80000001192092896f * streak_0));
        albedo_3 = albedo_5;
    }
    else
    {
        sheen_0 = _S20;
        streak_0 = 0.5f;
    }
    var colour_2 : vec3<f32> = shade_0(_S18.worldPosition_0, normal_4, albedo_3, _S18.albedo_1.w, _S20) + sheen_0 + rimLight_0(normal_4, toEye_3);
    var _S22 : f32 = _S18.finish_0.y;
    var colour_3 : vec3<f32>;
    if((_S22 > 0.0f))
    {
        colour_3 = colour_2 + environment_0(_S18.worldPosition_0, reflect((vec3<f32>(0) - toEye_3), normal_4)) * mix(albedo_3, vec3<f32>(1.0f), vec3<f32>(pow(1.0f - saturate(dot(normal_4, toEye_3)), 5.0f))) * vec3<f32>(_S22) * vec3<f32>((0.80000001192092896f + 0.40000000596046448f * streak_0));
    }
    else
    {
        colour_3 = colour_2;
    }
    var _S23 : f32 = _S18.finish_0.z;
    if((_S23 > 0.0f))
    {
        colour_3 = colour_3 * vec3<f32>(mix(0.34999999403953552f, 1.0f, smoothstep(0.0f, _S23, _S18.localPosition_0.y)));
    }
    var _S24 : vec3<f32> = _S18.emissive_0.xyz;
    var colour_4 : vec3<f32> = colour_3 + toLinear_0(_S24);
    var o_0 : FragmentOut_0;
    var _S25 : vec4<f32>;
    if(isGlowPass_0())
    {
        _S25 = vec4<f32>(_S24, 1.0f);
    }
    else
    {
        _S25 = vec4<f32>(toSrgb_0(rollOff_0(colour_4)), 1.0f);
    }
    o_0.fragColor_0 = _S25;
    return o_0;
}

