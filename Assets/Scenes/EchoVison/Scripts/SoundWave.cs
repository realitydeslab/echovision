using System.Collections;
using System.Collections.Generic;
using UnityEngine;



#if USE_SOUNDWAVE_ATTRACTOR

public class WaveAttractor
{
    public Vector3 position;
    public Vector3 speed;
    public Transform sphere;
    public float strength;
    public float life;
    public float age;
    public WaveAttractor()
    {
        position = speed = Vector3.zero;
        strength = age = life = 0;
    }
}
#endif

public class SoundWave
{
#if USE_SOUNDWAVE_ATTRACTOR
    //const int ATTACTOR_COUNT_EACH_WAVE = 5;
    //public WaveAttractor[] attactors;
#endif
    public Vector3 origin;
    public Vector3 direction;

    public float range = 0;
    public float speed = 1;

    public float strength = 1;
    public float angle = 90;
    public float thickness = 0.1f;

    public float alive = 0;
    public float age = 1; // originally dead
    public float life = 1;
    public float age_in_percentage
    {
        get
        {
            return life == 0 ? 0 : age / life;
        }
        set
        {
            age = Mathf.Clamp01(value) * life;
        }
    }
    

    

    public SoundWave()
    {
#if USE_SOUNDWAVE_ATTRACTOR
        //attactors = new WaveAttractor[ATTACTOR_COUNT_EACH_WAVE];
#endif
    }
}
