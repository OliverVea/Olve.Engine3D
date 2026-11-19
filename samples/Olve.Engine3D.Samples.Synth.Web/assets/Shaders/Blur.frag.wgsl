@binding(0) @group(0) var uSource_texture_0 : texture_2d<f32>;

@binding(1) @group(0) var uSource_sampler_0 : sampler;

struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) uv_0 : vec2<f32>,
    @location(1) step_0 : vec4<f32>,
};

@fragment
fn fragmentMain( _S1 : pixelInput_0, @builtin(position) position_0 : vec4<f32>) -> FragmentOut_0
{
    var weights_0 : array<f32, i32(4)> = array<f32, i32(4)>( 0.19648255407810211f, 0.296906977891922f, 0.094470396637916565f, 0.010381362400949001f );
    var _S2 : array<f32, i32(4)> = array<f32, i32(4)>( 0.0f, 1.4117647409439087f, 3.2941176891326904f, 5.1764707565307617f );
    var width_0 : f32;
    var height_0 : f32;
    {var dim = textureDimensions((uSource_texture_0));((width_0)) = f32(dim.x);((height_0)) = f32(dim.y);};
    var _S3 : vec2<f32> = _S1.step_0.xy / vec2<f32>(height_0);
    ;
    var _S4 : vec3<f32> = (textureSample((uSource_texture_0), (uSource_sampler_0), (_S1.uv_0))).xyz * vec3<f32>(0.19648255407810211f);
    var k_0 : i32 = i32(1);
    var sum_0 : vec3<f32> = _S4;
    loop
    {
        if((k_0 < i32(4)))
        {
        }
        else
        {
            break;
        }
        var _S5 : vec2<f32> = _S3 * vec2<f32>(_S2[k_0]);
        var _S6 : vec2<f32> = _S1.uv_0 + _S5;
        ;
        var _S7 : vec3<f32> = vec3<f32>(weights_0[k_0]);
        var sum_1 : vec3<f32> = sum_0 + (textureSample((uSource_texture_0), (uSource_sampler_0), (_S6))).xyz * _S7;
        var _S8 : vec2<f32> = _S1.uv_0 - _S5;
        ;
        var sum_2 : vec3<f32> = sum_1 + (textureSample((uSource_texture_0), (uSource_sampler_0), (_S8))).xyz * _S7;
        k_0 = k_0 + i32(1);
        sum_0 = sum_2;
    }
    var o_0 : FragmentOut_0;
    o_0.fragColor_0 = vec4<f32>(sum_0, 1.0f);
    return o_0;
}

