uniform float _Points[9*4];
float _scannedRemainValue;

float lerp(float from, float to, float rel){
  return ((1 - rel) * from) + (rel * to);
}
 
void custom_float(float3 position, out float3 direction, out float strength, out float remainStrength){
    float3 directionOutput = 0;
    float strengthOutput = 0;
    float simpleStrengthOutput = 0;
     
    [unroll]
    for (int i = 0; i < 9*4; i += 4){
        float3 p = float3(_Points[i], _Points[i+1], _Points[i+2]); // Position
        float t = _Points[i+3]; // Lifetime
         
        // Ripple Shape :
        // float rippleSize = 1;
        // float gradient = smoothstep(t/3, t, distance(position, p) / rippleSize);

        float simpleStrength = distance(position, p); // 0 - 9999

        simpleStrength = (simpleStrength  + 1) - ( _Points[i+3] * 3); // -2 - 9999

        //float simpleStrengthMax = (si mpleStrength  + 1) - ( _Points[i+3] * 3) - 0.5;

        float fade = 1- _Points[i+3];

        simpleStrength = simpleStrength * fade;


        if(simpleStrength > 1){
            simpleStrength = 0; // -2 - 1
        }

        if(simpleStrength < 0){
            simpleStrength = 0; // -2 - 1
        }


        simpleStrength = pow(simpleStrength, 12);
         
        // frac means it will have a sharp edge, while sine makes it more "soft"
        //float ripple = frac(gradient);
        // float ripple = saturate(sin(5 * (gradient)));
         
        // Distortion Direction & Strength :
        float3 rippleDirection = normalize(position-p);
         
        // float lifetimeFade = saturate(1-t); // Goes from 1 at t=0, to 0 at t=1
        // float rippleStrength = lifetimeFade * ripple;
         
        // directionOutput += rippleDirection * rippleStrength * 0.2;
        // strengthOutput += rippleStrength;
        simpleStrengthOutput += simpleStrength;
    }
     
    direction = directionOutput;

   // strength = strengthOutput;

    strength = simpleStrengthOutput;
    remainStrength = _scannedRemainValue;
}