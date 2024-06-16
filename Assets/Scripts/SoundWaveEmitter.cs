using UnityEngine;
using UnityEngine.VFX;

#if UNITY_IOS
using HoloKit;
using UnityEngine.InputSystem.XR;
#endif

#if UNITY_VISIONOS
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif


public class SoundWave
{

    public Vector3 origin;
    public Vector3 direction;

    public float range = 0;
    public float speed = 1;

    public float strength = 1;
    public float angle = 90;
    public float thickness = 0.1f;

    public float alive = 0;
    public float age = 0;
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
}

public class SoundWaveEmitter : MonoBehaviour
{
    [Header("References")]
#if UNITY_IOS
    [SerializeField] TrackedPoseDriver trackedPoseDriver;
#endif

#if UNITY_VISIONOS
    [SerializeField] UnityEngine.SpatialTracking.TrackedPoseDriver trackedPoseDriver;
#endif

    [SerializeField] HolokitAudioProcessor audioProcessor;
    [SerializeField] DepthImageProcessor depthImageProcessor;

    

    [Header("Effect")]
    [SerializeField] VisualEffect vfx;
    [SerializeField] Material matMeshing;

    [Header("SoundWave Settings")]
    [SerializeField] Vector2 soundwaveLife = new Vector2(0.5f, 1.5f);
    [SerializeField] Vector2 soundwaveSpeed = new Vector2(3f, 4.5f);
    [SerializeField] Vector2 soundwaveAngle = new Vector2(90, 180);
    [SerializeField] float minWaveThickness = 1;
    [SerializeField] float emitVolumeThreshold = 0.02f;


    const int MAX_SOUND_WAVE_COUNT = 3;
    SoundWave[] soundwaves = new SoundWave[MAX_SOUND_WAVE_COUNT];
    int nextEmitIndex = 0;
    float maxWaveRange = 50;
    float smoothedSoundVolume = 0;
    float smoothedSoundPitch = 0;
    bool debugMode = false;

    float[] rippleOriginList;
    float[] rippleDirectionList;
    float[] rippleAgeList;
    float[] rippleRangeList;
    float[] rippleAngleList;
    float[] rippleThicknessList;


    void Start()
    {
        // Init Soundwave and Attractors
        for (int i= 0; i < soundwaves.Length; i++)
        {
            soundwaves[i] = new SoundWave();
            DeactivateSoundWave(i);
        }

        // Init Mat Related Parameters
        rippleOriginList = new float[MAX_SOUND_WAVE_COUNT * 3];
        rippleDirectionList = new float[MAX_SOUND_WAVE_COUNT * 3];
        rippleAgeList = new float[MAX_SOUND_WAVE_COUNT];
        rippleRangeList = new float[MAX_SOUND_WAVE_COUNT];
        rippleAngleList = new float[MAX_SOUND_WAVE_COUNT];
        rippleThicknessList = new float[MAX_SOUND_WAVE_COUNT];
    }

    void Update()
    {
        // Smoothed Sound
        float temp_vel = 0;
        smoothedSoundVolume = Mathf.SmoothDamp(smoothedSoundVolume, audioProcessor.AudioVolume, ref temp_vel, 0.05f);
        smoothedSoundPitch = Mathf.SmoothDamp(smoothedSoundPitch, audioProcessor.AudioPitch, ref temp_vel, 0.05f);

        // Emit New SoundWave
        if (Input.GetMouseButton(0) || audioProcessor.AudioVolume > emitVolumeThreshold)
        {
            EmitSoundWave();
        }
        else
        {
            StopAllSoundWaves();
        }

        // Update Extant SoundWave        
        UpdateSoundWave();

        // Push changes to VFX and Mat
        PushIteratedChanges();
    }

#region Emit/Stop/Update SoundWave
    void EmitSoundWave()
    {        
        int cur_emit_index = GetCurrentWaveIndex();
        SoundWave wave = soundwaves[cur_emit_index];

        // If current wave still is still on going, then do nothing
        if (wave.alive == 1)
            return;

        // Emit New One
        int next_emit_index = GetNextWaveIndex();
        SoundWave new_wave = soundwaves[next_emit_index];

        Transform head_transform = trackedPoseDriver.transform;
        Vector3 pos = head_transform.position;
        Vector3 dir = Quaternion.Euler(head_transform.eulerAngles) * Vector3.forward;
        new_wave.origin = pos;
        new_wave.direction = dir;

        new_wave.speed = Random.Range(soundwaveSpeed.x, soundwaveSpeed.y);// * pitch; 
        new_wave.life = Random.Range(soundwaveLife.x, soundwaveLife.y) * Utilities.Remap(smoothedSoundPitch, 0f, 1f, 1f, 1.5f); // relative to pitch
        new_wave.angle = Utilities.Remap(smoothedSoundVolume, 0, 1, soundwaveAngle.x, soundwaveAngle.y); // relative to volume

        ActivateSoundWave(next_emit_index);

        PushInitialChanges(next_emit_index);

        MoveWaveIndex();        
    }

    void StopAllSoundWaves()
    {
        for (int i = 0; i < soundwaves.Length; i++)
        {
            StopSoundWave(i);
        }
    }

    void StopSoundWave(int index)
    {
        SoundWave wave = soundwaves[index];
        wave.alive = 0;
    }

    void UpdateSoundWave()
    {
        Transform head_transform = trackedPoseDriver.transform;
        for (int i = 0; i < soundwaves.Length; i++)
        {
            SoundWave wave = soundwaves[i];

            // IF Wave is totally dead. Skip
            if (IsWaveTotallyDead(wave))
                continue;


            // Move Wave forward anyway
            wave.range += wave.speed * Time.deltaTime;


            // IF Wave is alive (player keeps making sound)
            if (wave.alive == 1)
            {
                wave.thickness = wave.range;
                wave.age += Time.deltaTime;
                if (wave.age >= wave.life)
                {
                    wave.age = wave.life;
                    StopSoundWave(i);
                }
            }

            // IF Wave need to die (player stopped making sound)
            if (wave.alive == 0)
            {
                // if sound span is too short, force wave to last for at least a minimum thickness
                if (wave.thickness < minWaveThickness)
                {
                    wave.thickness = wave.range;
                }

                wave.age -= Time.deltaTime;
                if (wave.age < 0)
                    wave.age = 0;
            }
        }
    }

    void PushInitialChanges(int index)
    {
        // VFX
        SoundWave wave = soundwaves[index];
        if(index ==0)
        {
            // parameters of SoundWave0 are separated
            vfx.SetVector3("WaveOrigin", wave.origin);
            vfx.SetVector3("WaveDirection", wave.direction);
            vfx.SetFloat("WaveRange", wave.range);
            vfx.SetFloat("WaveAngle", wave.angle);
            vfx.SetFloat("WaveAge", wave.age_in_percentage);
        }else if(index == 1 || index == 2)
        {
            // parameters of SoundWave1 and SoundWave2 are merged in a transform struct for convinience
            string prefix = "WaveParameter" + index.ToString();
            vfx.SetVector3(prefix + "_position", wave.origin);
            vfx.SetVector3(prefix + "_angles", wave.direction);
            vfx.SetVector3(prefix + "_scale", new Vector3(wave.range, wave.angle, wave.age_in_percentage));
        }
        vfx.SetFloat("WaveMinThickness", minWaveThickness);


        // Material
        rippleOriginList[index * 3] = wave.origin.x; rippleOriginList[index * 3 + 1] = wave.origin.y; rippleOriginList[index * 3 + 2] = wave.origin.z;
        rippleDirectionList[index * 3] = wave.direction.x; rippleDirectionList[index * 3 + 1] = wave.direction.y; rippleDirectionList[index * 3 + 2] = wave.direction.z;

        rippleAgeList[index] = wave.age_in_percentage;
        rippleRangeList[index] = wave.range;
        rippleAngleList[index] = wave.angle;
        rippleThicknessList[index] = wave.thickness;

        matMeshing.SetFloatArray("rippleOriginList", rippleOriginList);
        matMeshing.SetFloatArray("rippleDirectionList", rippleDirectionList);
        matMeshing.SetFloatArray("rippleAgeList", rippleAgeList);
        matMeshing.SetFloatArray("rippleRangeList", rippleRangeList);
        matMeshing.SetFloatArray("rippleAngleList", rippleAngleList);
        matMeshing.SetFloatArray("rippleThicknessList", rippleThicknessList);

        matMeshing.SetFloat("_WaveMinThickness", minWaveThickness);
        matMeshing.SetVector("_WaveOrigin", wave.origin);
    }

    void PushIteratedChanges()
    {

        // VFX, Update using latest sound wave
        for (int i = 0; i < MAX_SOUND_WAVE_COUNT; i++)
        {
            SoundWave wave = soundwaves[i];
            if (i == 0)
            {
                // parameters of SoundWave0 are separated
                vfx.SetFloat("WaveRange", wave.range);
                vfx.SetFloat("WaveAngle", wave.angle);
                vfx.SetFloat("WaveAge", wave.age_in_percentage);
            }
            else if (i == 1 || i == 2)
            {
                // parameters of SoundWave1 and SoundWave2 are merged in a transform struct for convinience
                string prefix = "WaveParameter" + i.ToString();
                vfx.SetVector3(prefix + "_scale", new Vector3(wave.range, wave.angle, wave.age_in_percentage));
            }
        }        

        // VFX, Update Depth Image
        if (depthImageProcessor != null)
        {
            Texture2D human_tex = depthImageProcessor.HumanStencilTexture;
            if (human_tex != null)
            {
                human_tex.wrapMode = TextureWrapMode.Repeat;
                vfx.SetTexture("HumanStencilTexture", human_tex);
                vfx.SetMatrix4x4("HumanStencilTextureMatrix", depthImageProcessor.DisplayRotatioMatrix);
                matMeshing.SetTexture("_HumanStencilTexture", human_tex);
                matMeshing.SetMatrix("_HumanStencilTextureMatrix", depthImageProcessor.DisplayRotatioMatrix);
            }
        }

        // Material
        for (int i = 0; i < MAX_SOUND_WAVE_COUNT; i++)
        {
            rippleAgeList[i] = soundwaves[i].age_in_percentage;
            rippleRangeList[i] = soundwaves[i].range;
            rippleThicknessList[i] = soundwaves[i].thickness;
        }
        matMeshing.SetFloatArray("rippleAgeList", rippleAgeList);
        matMeshing.SetFloatArray("rippleRangeList", rippleRangeList);
        matMeshing.SetFloatArray("rippleThicknessList", rippleThicknessList);

        matMeshing.SetFloat("_SoundVolume", smoothedSoundVolume);
        matMeshing.SetFloat("_SoundPitch", smoothedSoundPitch);
    }
#endregion


#region Other Functions
    bool IsWaveTotallyDead(SoundWave wave)
    {
        return wave.alive == 0 && wave.age == 0 && wave.thickness >= minWaveThickness && wave.range >= maxWaveRange/* to make it die far*/;
    }

    void DeactivateSoundWave(int index)
    {
        SoundWave wave = soundwaves[index];

        wave.alive = 0;
        wave.age = 0;
        wave.range = maxWaveRange;
        wave.thickness = minWaveThickness;
    }

    void ActivateSoundWave(int index)
    {
        SoundWave wave = soundwaves[index];

        wave.alive = 1;
        wave.age = 0;
        wave.range = 0;
        wave.thickness = 0;
    }

    int GetCurrentWaveIndex()
    {
        if (debugMode)
            return 0;

        return (nextEmitIndex == 0 ? MAX_SOUND_WAVE_COUNT - 1 : nextEmitIndex - 1);
    }

    int GetNextWaveIndex()
    {
        return nextEmitIndex;
    }

    int MoveWaveIndex()
    {
        nextEmitIndex = (nextEmitIndex == MAX_SOUND_WAVE_COUNT - 1 ? 0 : nextEmitIndex + 1);
        return nextEmitIndex;
    }

    float[] Vector3ToArray(Vector3 vec)
    {
        float[] array = new float[3];
        array[0] = vec.x;
        array[1] = vec.y;
        array[2] = vec.z;
        return array;
    }

    void PrintDebugInfo(string prefix, int index)
    {
        SoundWave wave = soundwaves[index];
        Debug.Log(prefix + string.Format("|{0}, alive:{1}, age:{2}, range:{3}, angle:{4}, thickness:{5}, origin:{6}, dir:{7}",
                index, wave.alive, wave.age, wave.range, wave.angle, wave.thickness, wave.origin, wave.direction));
    }
#endregion
}
