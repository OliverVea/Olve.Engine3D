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
fn hash1_0( n_0 : f32) -> f32
{
    return fract(sin(n_0 * 12.989800453186035f) * 43758.546875f);
}

fn hash2_0( p_0 : vec2<f32>) -> f32
{
    return fract(sin(dot(p_0, vec2<f32>(12.989800453186035f, 78.233001708984375f))) * 43758.546875f);
}

fn rollOff_0( c_0 : vec3<f32>) -> vec3<f32>
{
    var knee_0 : vec3<f32> = vec3<f32>(0.75f);
    var _S1 : vec3<f32> = max(c_0 - knee_0, vec3<f32>(0.0f));
    return min(c_0, knee_0) + _S1 / (vec3<f32>(1.0f) + _S1 * vec3<f32>(2.0f));
}

fn toSrgb_0( c_1 : vec3<f32>) -> vec3<f32>
{
    return pow(max(c_1, vec3<f32>(0.0f)), vec3<f32>(0.45454543828964233f));
}

struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) worldPosition_0 : vec3<f32>,
    @location(1) localPosition_0 : vec3<f32>,
    @location(2) view_1 : vec4<f32>,
};

fn isGlowPass_0() -> bool
{
    return ((spacePass_0.glowOnly_0.x) > 0.5f);
}

@fragment
fn fragmentMain( _S2 : pixelInput_0, @builtin(position) position_0 : vec4<f32>) -> FragmentOut_0
{
    var d_0 : vec3<f32> = normalize(_S2.worldPosition_0 - spacePass_0.cameraPosition_0);
    var across_0 : f32 = atan2(d_0.x, - d_0.z);
    var up_0 : f32 = d_0.y / max(length(d_0.xz), 0.0010000000474974513f);
    var _S3 : f32 = max((fwidth((across_0))), (fwidth((up_0))));
    var _S4 : vec3<f32> = vec3<f32>(0.0f);
    var colour_0 : vec3<f32> = mix(vec3<f32>(0.026000000536441803f, 0.020999999716877937f, 0.035999998450279236f), vec3<f32>(0.0024999999441206455f, 0.0030000000260770321f, 0.0080000003799796104f), vec3<f32>(smoothstep(-0.090000003576278687f, 0.23000001907348633f, up_0)));
    var row_0 : i32 = i32(0);
    var glow_0 : vec3<f32> = _S4;
    loop
    {
        if((row_0 < i32(3)))
        {
        }
        else
        {
            break;
        }
        var k_0 : f32 = f32(row_0);
        var columns_0 : f32 = mix(70.0f, 26.0f, k_0 / 2.0f);
        var x_0 : f32 = across_0 * columns_0 + k_0 * 17.299999237060547f;
        var id_0 : f32 = floor(x_0) + k_0 * 101.0f;
        var f_0 : f32 = fract(x_0);
        var tall_0 : f32 = hash1_0(id_0);
        var top_0 : f32 = -0.070000000298023224f + (0.012000000104308128f + 0.05000000074505806f * tall_0 * tall_0 + 0.012000000104308128f * k_0) * (0.69999998807907104f + 0.5f * k_0);
        var width_0 : f32 = 0.55000001192092896f + 0.40000000596046448f * hash1_0(id_0 + 7.0999999046325684f);
        var _S5 : bool;
        if((up_0 < top_0))
        {
            _S5 = ((abs(f_0 - 0.5f)) < (width_0 * 0.5f));
        }
        else
        {
            _S5 = false;
        }
        var inside_0 : bool;
        if(_S5)
        {
            inside_0 = ((hash1_0(id_0 + 3.2999999523162842f)) > 0.11999999731779099f);
        }
        else
        {
            inside_0 = false;
        }
        var glow_1 : vec3<f32>;
        if((tall_0 > 0.86000001430511475f))
        {
            glow_1 = glow_0 + vec3<f32>(1.0f, 0.079999998211860657f, 0.05000000074505806f) * vec3<f32>(smoothstep(0.40000000596046448f, 1.0f, sin(_S2.view_1.x * 2.2000000476837158f + id_0) * 0.5f + 0.5f)) * vec3<f32>(0.89999997615814209f) * vec3<f32>(saturate(1.0f - length(vec2<f32>((f_0 - 0.5f) / columns_0, up_0 - top_0 - 0.0030000000260770321f)) / 0.0024999999441206455f));
        }
        else
        {
            glow_1 = glow_0;
        }
        if(!inside_0)
        {
            glow_0 = glow_1;
            row_0 = row_0 + i32(1);
            continue;
        }
        var cell_0 : vec2<f32> = vec2<f32>((f_0 - 0.5f) / width_0 * mix(5.0f, 7.0f, hash1_0(id_0 + 1.7000000476837158f)), (top_0 - up_0) * columns_0 * 2.2000000476837158f);
        var at_0 : vec2<f32> = floor(cell_0);
        var inCell_0 : vec2<f32> = fract(cell_0);
        var _S6 : f32 = inCell_0.x;
        var _S7 : f32 = inCell_0.y;
        var _S8 : f32 = mix(0.05000000074505806f, 0.14000000059604645f, hash1_0(id_0 + 5.9000000953674316f));
        var _S9 : vec3<f32> = mix(vec3<f32>(1.0f, 0.62000000476837158f, 0.30000001192092896f), vec3<f32>(0.55000001192092896f, 0.69999998807907104f, 1.0f), vec3<f32>(step(0.69999998807907104f, hash2_0(at_0 + vec2<f32>(id_0))))) * vec3<f32>(mix(0.12999999523162842f * _S8 * 3.0f, step(0.25f, _S6) * step(_S6, 0.75f) * step(0.30000001192092896f, _S7) * step(_S7, 0.75f) * step(hash2_0(at_0 + vec2<f32>((id_0 * 3.0999999046325684f))), _S8), saturate(1.6000000238418579f - _S3 * columns_0 * 12.0f))) * vec3<f32>(mix(0.14000000059604645f, 0.30000001192092896f, k_0 / 2.0f));
        colour_0 = mix(vec3<f32>(0.017000000923871994f, 0.016000000759959221f, 0.026000000536441803f), vec3<f32>(0.004999999888241291f, 0.0054999999701976776f, 0.0080000003799796104f), vec3<f32>((k_0 / 2.0f)));
        glow_0 = _S9;
        row_0 = row_0 + i32(1);
    }
    var side_0 : f32 = clamp(_S2.localPosition_0.x / max(_S2.view_1.y, 0.0010000000474974513f), -1.0f, 1.0f);
    var sheen_0 : vec3<f32> = (spacePass_0.light0Color_0 * vec3<f32>(saturate(0.60000002384185791f - side_0)) + spacePass_0.light1Color_0 * vec3<f32>(saturate(0.60000002384185791f + side_0))) * vec3<f32>(0.0040000001899898052f) + spacePass_0.keyColor_0 * vec3<f32>(0.00060000002849847078f);
    var o_0 : FragmentOut_0;
    var _S10 : vec4<f32>;
    if(isGlowPass_0())
    {
        _S10 = vec4<f32>(glow_0, 1.0f);
    }
    else
    {
        _S10 = vec4<f32>(toSrgb_0(rollOff_0(colour_0 + glow_0 + sheen_0)), 1.0f);
    }
    o_0.fragColor_0 = _S10;
    return o_0;
}

