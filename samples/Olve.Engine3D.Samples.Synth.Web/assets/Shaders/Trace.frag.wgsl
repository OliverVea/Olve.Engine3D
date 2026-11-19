struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) stroke_0 : vec2<f32>,
    @location(1) phosphor_0 : vec3<f32>,
};

@fragment
fn fragmentMain( _S1 : pixelInput_0, @builtin(position) position_0 : vec4<f32>) -> FragmentOut_0
{
    var o_0 : FragmentOut_0;
    var _S2 : f32 = _S1.stroke_0.x;
    if((_S2 > 1.5f))
    {
        o_0.fragColor_0 = vec4<f32>(0.0f, 0.0f, 0.0f, _S1.stroke_0.y);
        return o_0;
    }
    o_0.fragColor_0 = vec4<f32>(_S1.phosphor_0, _S1.stroke_0.y * exp(-3.5f * _S2 * _S2) * (1.0f - smoothstep(0.85000002384185791f, 1.0f, abs(_S2))));
    return o_0;
}

