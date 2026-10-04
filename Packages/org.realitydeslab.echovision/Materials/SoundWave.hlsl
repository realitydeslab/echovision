#define MAX_RIPPLE_COUNT 3

uniform float rippleOriginList[MAX_RIPPLE_COUNT*3];
uniform float rippleDirectionList[MAX_RIPPLE_COUNT*3];

uniform float rippleAgeList[MAX_RIPPLE_COUNT];
uniform float rippleRangeList[MAX_RIPPLE_COUNT];

uniform float rippleAngleList[MAX_RIPPLE_COUNT];
uniform float rippleThicknessList[MAX_RIPPLE_COUNT];
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

void CalculateAlpha_float(float3 position, float ripple_min_thickness, out float alpha, out float position_in_ripple, out float alpha_band, out float alpha_angle)
{
    alpha = 0;
    position_in_ripple = 0;
    alpha_band = 0;
    alpha_angle = 0;

    [unroll]
    for(int i=0; i<MAX_RIPPLE_COUNT; i++)
    {
        float ripple_range = rippleRangeList[i];
        float ripple_thickness = rippleThicknessList[i];    

        // if it's dead, continue
        if(ripple_thickness == 0)
        {
            continue;
        }    


        // calculate ripple's position
        float3 origin = float3(rippleOriginList[i*3], rippleOriginList[i*3+1], rippleOriginList[i*3+2]);
        float3 direction = normalize(position - origin);
        float dis = distance(position, origin);

        
        float ripple_alpha = 1;//smoothstep(0, 0.2, rippleAgeList[i]);

        // take the distance that ripple has travelled into account
        float dis_per = linstep(ripple_range-ripple_thickness, ripple_range, dis);        
        if(dis_per <= 0)ripple_alpha = 0;
        else if(dis_per >= 1)ripple_alpha = 0;
        else
        {
            float base_alpha = 0.1;
            dis_per = linstep(max(0,ripple_range-ripple_min_thickness), ripple_range, dis);
            ripple_alpha = fadeinout(dis_per, 0, 1, 0.2, 0.9) + base_alpha;
        }
        // ripple_alpha *= clamp(noise(float2(direction.x, direction.z) * float2(_NoiseScale, _NoiseScale)) + 0.2, 0.2, 1);


        // check if it's within the angle
        // take the angle between vertex and origin into account 
        float3 ripple_direction = normalize(float3(rippleDirectionList[i*3], rippleDirectionList[i*3+1], rippleDirectionList[i*3+2]));
        float angle = degrees(acos(dot(ripple_direction, direction)));
        ripple_alpha *= 1 - pow(smoothstep(0, rippleAngleList[i]*0.5, angle), _AngleGamma);
        // ripple_alpha *= clamp(noise(float2(position.x, position.z) * float2(_NoiseScale, _NoiseScale))+ 0.5, 0.5, 1);

       
        // output alpha
        alpha += ripple_alpha;


        // output hard edge
        if(dis > ripple_range-ripple_thickness && dis<ripple_range)
        {
            alpha_band += 1;
        }
        else
        {
            alpha_band += 0;
        }
        if(angle < rippleAngleList[i]*0.5)
        {
            alpha_angle += 1;
        }
        else
        {
            alpha_angle += 0;
        }

        // output precentage in ripple
        position_in_ripple = max(position_in_ripple, linstep(ripple_range-ripple_thickness, ripple_range, dis));
    }
}

