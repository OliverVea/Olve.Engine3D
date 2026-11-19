struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) color_0 : vec3<f32>,
};

@fragment
fn fragmentMain( _S1 : pixelInput_0, @builtin(position) position_0 : vec4<f32>) -> FragmentOut_0
{
    var o_0 : FragmentOut_0;
    o_0.fragColor_0 = vec4<f32>(_S1.color_0, 1.0f);
    return o_0;
}

