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

fn whiteLight_0( lightPosition_0 : vec3<f32>,  position_0 : vec3<f32>,  normal_0 : vec3<f32>,  toEye_0 : vec3<f32>,  albedo_0 : vec3<f32>,  shine_0 : f32) -> vec3<f32>
{
    var toLight_0 : vec3<f32> = lightPosition_0 - position_0;
    var distance2_0 : f32 = dot(toLight_0, toLight_0);
    var l_0 : vec3<f32> = toLight_0 * vec3<f32>(inverseSqrt(max(distance2_0, 9.99999997475242708e-07f)));
    var _S1 : f32 = dot(normal_0, l_0);
    var exponent_0 : f32 = mix(12.0f, 80.0f, shine_0);
    return (albedo_0 * vec3<f32>(saturate((_S1 + 0.20000000298023224f) / 1.2000000476837158f)) + vec3<f32>((pow(max(dot(normal_0, normalize(l_0 + toEye_0)), 0.0f), exponent_0) * shine_0 * (exponent_0 + 8.0f) / 160.0f * step(0.0f, _S1)))) * vec3<f32>((1.0f / (1.0f + 0.80000001192092896f * distance2_0)));
}

fn rollOff_0( c_1 : vec3<f32>) -> vec3<f32>
{
    var knee_0 : vec3<f32> = vec3<f32>(0.75f);
    var _S2 : vec3<f32> = max(c_1 - knee_0, vec3<f32>(0.0f));
    return min(c_1, knee_0) + _S2 / (vec3<f32>(1.0f) + _S2 * vec3<f32>(2.0f));
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
    @location(2) normal_1 : vec3<f32>,
    @location(3) tangent_0 : vec3<f32>,
    @location(4) bitangent_0 : vec3<f32>,
    @location(5) paint_0 : vec4<f32>,
    @location(6) weave_0 : vec4<f32>,
};

fn shade_0( _S3 : vec3<f32>,  _S4 : vec3<f32>,  _S5 : vec3<f32>,  _S6 : f32,  _S7 : vec3<f32>) -> vec3<f32>
{
    var toEye_1 : vec3<f32> = normalize(spacePass_0.cameraPosition_0 - _S3);
    return _S5 * mix(spacePass_0.groundAmbient_0, spacePass_0.skyAmbient_0, vec3<f32>((_S4.y * 0.5f + 0.5f))) + whiteLight_0(spacePass_0.keyPosition_0, _S3, _S4, toEye_1, _S5, _S6) * spacePass_0.keyColor_0 + whiteLight_0(spacePass_0.light0Position_0, _S3, _S4, toEye_1, _S5, _S6) * spacePass_0.light0Color_0 + whiteLight_0(spacePass_0.light1Position_0, _S3, _S4, toEye_1, _S5, _S6) * spacePass_0.light1Color_0 + _S7;
}

fn isGlowPass_0() -> bool
{
    return ((spacePass_0.glowOnly_0.x) > 0.5f);
}

@fragment
fn fragmentMain( _S8 : pixelInput_0, @builtin(position) position_1 : vec4<f32>) -> FragmentOut_0
{
    var _S9 : f32 = _S8.localPosition_0.x;
    var _S10 : f32 = _S8.localPosition_0.z;
    var p_0 : vec2<f32> = vec2<f32>(_S9 + _S10, _S9 - _S10) * vec2<f32>(0.70710676908493042f) / vec2<f32>(_S8.weave_0.x);
    var cell_0 : vec2<f32> = floor(p_0);
    var f_0 : vec2<f32> = fract(p_0) - vec2<f32>(0.5f);
    var _S11 : f32 = _S8.weave_0.y;
    var _S12 : vec2<f32> = vec2<f32>((_S11 * 0.5f));
    var across_0 : vec2<f32> = f_0.yx / _S12;
    var width_0 : vec2<f32> = (fwidth((p_0))) / _S12;
    var _S13 : vec2<f32> = vec2<f32>(1.0f);
    var onWire_0 : vec2<f32> = saturate(_S13 - (abs(across_0) - _S13) / max(width_0, vec2<f32>(9.9999997473787516e-05f)));
    var parity_0 : f32;
    if((((((abs(cell_0.x + cell_0.y))) % ((2.0f)))) < 0.5f))
    {
        parity_0 = 1.0f;
    }
    else
    {
        parity_0 = -1.0f;
    }
    var _S14 : f32 = - parity_0;
    var lift_0 : vec2<f32> = vec2<f32>(parity_0, _S14) * cos(f_0 * vec2<f32>(3.1415927410125732f));
    var firstOnTop_0 : bool = ((lift_0.x) >= (lift_0.y));
    var _S15 : f32 = onWire_0.x;
    var _S16 : f32 = onWire_0.y;
    var _S17 : f32 = max(_S15, _S16);
    var a_0 : f32;
    if(firstOnTop_0)
    {
        if((_S15 > 0.0f))
        {
            a_0 = across_0.x;
        }
        else
        {
            a_0 = across_0.y;
        }
    }
    else
    {
        if((_S16 > 0.0f))
        {
            a_0 = across_0.y;
        }
        else
        {
            a_0 = across_0.x;
        }
    }
    var _S18 : bool;
    if(firstOnTop_0)
    {
        _S18 = (_S15 > 0.0f);
    }
    else
    {
        _S18 = false;
    }
    if(_S18)
    {
        _S18 = true;
    }
    else
    {
        if(!firstOnTop_0)
        {
            _S18 = (_S16 <= 0.0f);
        }
        else
        {
            _S18 = false;
        }
    }
    var alongFirst_0 : f32;
    if(_S18)
    {
        alongFirst_0 = 1.0f;
    }
    else
    {
        alongFirst_0 = 0.0f;
    }
    var normal_2 : vec3<f32> = normalize(_S8.normal_1);
    var _S19 : vec3<f32> = vec3<f32>((1.0f - alongFirst_0));
    var sideways_0 : vec3<f32> = normalize(mix(_S8.bitangent_0, _S8.tangent_0, _S19));
    var along_0 : vec3<f32> = normalize(mix(_S8.tangent_0, _S8.bitangent_0, _S19));
    if(firstOnTop_0)
    {
        parity_0 = _S14;
    }
    if((alongFirst_0 > 0.5f))
    {
        alongFirst_0 = f_0.x;
    }
    else
    {
        alongFirst_0 = f_0.y;
    }
    var fine_0 : f32 = saturate(max(width_0.x, width_0.y) * 0.5f - 0.5f);
    var _S20 : f32 = 1.0f - _S11;
    var alpha_0 : f32 = mix(_S17, 1.0f - _S20 * _S20, fine_0);
    var _S21 : vec3<f32> = shade_0(_S8.worldPosition_0, normalize(mix(normalize(normal_2 + sideways_0 * vec3<f32>(clamp(a_0, -1.0f, 1.0f)) * vec3<f32>(0.89999997615814209f) + along_0 * vec3<f32>((parity_0 * sin(alongFirst_0 * 3.1415927410125732f) * 0.5f))), normal_2, vec3<f32>(fine_0))), toLinear_0(_S8.paint_0.xyz), _S8.paint_0.w, vec3<f32>(0.0f));
    var o_0 : FragmentOut_0;
    var _S22 : vec4<f32>;
    if(isGlowPass_0())
    {
        _S22 = vec4<f32>(0.0f, 0.0f, 0.0f, alpha_0);
    }
    else
    {
        _S22 = vec4<f32>(toSrgb_0(rollOff_0(_S21)), alpha_0);
    }
    o_0.fragColor_0 = _S22;
    return o_0;
}

