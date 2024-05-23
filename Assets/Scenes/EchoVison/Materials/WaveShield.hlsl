

uniform float rippleOrigin[3];
uniform float rippleDirection[3];
uniform float rippleAge;
uniform float rippleRange;
uniform float rippleAngle;
uniform float rippleThickness;
// 2D Random
float random (float2 st) {
    return frac(sin(dot(st.xy,
                         float2(12.9898,78.233)))
                 * 43758.5453123);
}

// 2D Noise based on Morgan McGuire @morgan3d
// https://www.shadertoy.com/view/4dS3Wd
float noise (float2 st) {
    float2 i = floor(st);
    float2 f = frac(st);

    // Four corners in 2D of a tile
    float a = random(i);
    float b = random(i + float2(1.0, 0.0));
    float c = random(i + float2(0.0, 1.0));
    float d = random(i + float2(1.0, 1.0));

    // Smooth Interpolation

    // Cubic Hermine Curve.  Same as SmoothStep()
    float2 u = f*f*(3.0-2.0*f);
    // u = smoothstep(0.,1.,f);

    // Mix 4 coorners percentages
    return lerp(a, b, u.x) +
            (c - a)* u.y * (1.0 - u.x) +
            (d - b) * u.x * u.y;
}

float fadeinout(float v, float min, float max, float t1, float t2)
{
    if(min == max) return v;
    v = clamp(v, min, max);
    float per = (v-min) / (max-min);
    if(per <= t1)per = t1 == 0 ? 1 : per / t1;
    else if(per >= t2) per = t2 == 1 ? 0 : (1-per)/(1-t2);
    else per = 1;
    return per;
}
float linstep(float min, float max, float v)
{
    if(min == max) return 0;
    return clamp((v - min) / (max - min), 0.0, 1.0);
}

void CalculateAlpha_float(float3 position, float noise_time, out float alpha/*, out float position_in_ripple*/)
{
    alpha = 0;
    // position_in_ripple = 0;

    //[unroll]

    if(_DebugMode)
    {

    }        

    float ripple_alpha = fadeinout(_Age, 0, 1, 0.3, 0.7);

    // calculate ripple's position
    // take the distance that ripple has travelled into account

    float ripple_range = _Range;//_RippleSpeed * rippleAge[i];
    // ripple_alpha *= fadeinout(ripple_range, 0, _RippleMaxRange, 0, 0.9);
    

    // check if it's inside the range of ripple
    // take the vertex position within the ripple into account
    // float3 origin = float3(rippleOrigin[0], rippleOrigin[1], rippleOrigin[2]);
    // float3 direction = normalize(position - origin);
    // float dis = distance(position, origin);
    // float temp_thickness = rippleThickness * smoothstep(0, 0.2, rippleAge);   
    // ripple_alpha *= pow(smoothstep(0, 1, fadeinout(dis, ripple_range-temp_thickness*0.5, ripple_range+temp_thickness*0.5, 0.4, 0.6)), _RippleBandGamma);
    // ripple_alpha *= clamp(noise(float2(direction.x, direction.z) * float2(_NoiseScale, _NoiseScale)) + 0.2, 0.2, 1);


    // check if it's within the angle
    // take the angle between vertex and origin into account 
    float3 origin = float3(_Origin[0], _Origin[1], _Origin[2]);
    float3 direction = normalize(position - origin);
    float3 ripple_direction = normalize(float3(_Direction[0], _Direction[1], _Direction[2]));
    float angle = degrees(acos(dot(ripple_direction, direction)));
    ripple_alpha *= 1 - pow(smoothstep(0, _Angle*0.5, angle), _AngleGamma);
    // ripple_alpha *= clamp(noise(float2(position.x, position.z) * float2(_NoiseScale, _NoiseScale))+ 0.5, 0.5, 1);

    
    // output alpha
    alpha += ripple_alpha;

    
    // output precentage in ripple
    // position_in_ripple = max(position_in_ripple, linstep(ripple_range-temp_thickness*0.5, ripple_range+temp_thickness*0.5, dis));

}

