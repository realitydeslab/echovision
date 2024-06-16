using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.VFX;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using HoloKit;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation.Samples;


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
    
    [SerializeField] HoloKitCameraManager cameraManager;
    [SerializeField] HolokitAudioProcessor audioProcessor;
    [SerializeField] DepthImageProcessor depthImageProcessor;
    [SerializeField] TrackedPoseDriver trackedPoseDriver;

    [Header("References")]
    public VisualEffect vfx;
    public Material matMeshing;

    [Header("SoundWave Settings")]
    [Tooltip("Fade In/Out Duration")]
    public Vector2 soundwaveLife = new Vector2(4, 6);

    [Tooltip("March Speed")]
    public Vector2 soundwaveSpeed = new Vector2(1, 4);

    //[Tooltip("Strength")]
    //public Vector2 soundwaveStrength = new Vector2(0, 1);

    [Tooltip("Angle")]
    public Vector2 soundwaveAngle = new Vector2(90, 180);

    public float minWaveThickness = 1;
    public float emitVolumeThreshold = 0.02f;
    //public float particleSweepThickness = 0.36f;
    float maxWaveRange = 50;

    const int MAX_SOUND_WAVE_COUNT = 3;
    SoundWave[] soundwaves = new SoundWave[MAX_SOUND_WAVE_COUNT];

    int nextEmitIndex = 0;
    float[] rippleOriginList;
    float[] rippleDirectionList;
    //float[] rippleAliveList;
    float[] rippleAgeList;
    float[] rippleRangeList;
    float[] rippleAngleList;
    float[] rippleThicknessList;
    float lastSoundVolume = 0;
    float lastSoundPitch = 0;

    [Header("Debug")]
    public bool debugMode = false;
    [Range(0.0f, 1.0f)]
    public float testAge = 0;
    [Range(0.0f, 10.0f)]
    public float testRange = 1;
    [Range(0.0f, 180.0f)]
    public float testAngle = 90;
    [Range(0.01f, 5.0f)]
    public float testThickness = 2f;

    [Range(0f, 1f)]
    public float testAudioVolume = 0f;

    [Range(0f, 1f)]
    public float testAudioPitch = 0f;

        

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
        //rippleAliveList = new float[MAX_SOUND_WAVE_COUNT];
        rippleAgeList = new float[MAX_SOUND_WAVE_COUNT];
        rippleRangeList = new float[MAX_SOUND_WAVE_COUNT];
        rippleAngleList = new float[MAX_SOUND_WAVE_COUNT];
        rippleThicknessList = new float[MAX_SOUND_WAVE_COUNT];
    }

    void Update()
    {
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


        //// If in Debug Mode, rewrite data
        //if (debugMode)
        //{
        //    for (int i = 0; i < soundwaves.Length; i++)
        //    {
        //        SoundWave wave = soundwaves[i];
        //        // Activate Wave0
        //        if (i == 0)
        //        {
        //            wave.origin = head_transform.position;
        //            wave.direction = Quaternion.Euler(head_transform.eulerAngles) * Vector3.forward;

        //            wave.alive = 1;
        //            wave.age_in_percentage = testAge;
        //            wave.range = testRange;
        //            wave.angle = testAngle;
        //            wave.thickness = testThickness;

        //            PushInitialChanges(i);
        //        }

        //        // Deactivate others
        //        else
        //        {
        //            DeactivateSoundWave(i);
        //        }
        //    }
        //}
        

        // Push changes to VFX and Mat
        PushIteratedChanges();



        

        if (depthImageProcessor != null)
        {
            Texture2D human_tex = depthImageProcessor.HumanStencilTexture;
            if(human_tex != null)
            {
                human_tex.wrapMode = TextureWrapMode.Repeat;
                vfx.SetTexture("HumanStencilTexture", human_tex);
                vfx.SetMatrix4x4("HumanStencilTextureMatrix", depthImageProcessor.DisplayRotatioMatrix);
                matMeshing.SetTexture("_HumanStencilTexture", human_tex);
                matMeshing.SetMatrix("_HumanStencilTextureMatrix", depthImageProcessor.DisplayRotatioMatrix);
            }
        }

    }

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

        float volume = audioProcessor.AudioVolume;
        float pitch = audioProcessor.AudioPitch;
        new_wave.speed = Random.Range(soundwaveSpeed.x, soundwaveSpeed.y);// * pitch; 
        new_wave.life = Random.Range(soundwaveLife.x, soundwaveLife.y) * Utilities.Remap(pitch, 0f, 1f, 1f, 1.5f); // relative to pitch
        //new_wave.strength = Random.Range(soundwaveStrength.x, soundwaveStrength.y) * volume; // relative to volume
        new_wave.angle = Utilities.Remap(volume, 0, 1, soundwaveAngle.x, soundwaveAngle.y); // relative to volume


        ActivateSoundWave(next_emit_index);

        PushInitialChanges(next_emit_index);

        Debug.Log("Emit new SoundWave, Index:" + next_emit_index);

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

    

    void PushInitialChanges(int index)
    {
        // VFX
        SoundWave wave = soundwaves[index];
        vfx.SetVector3("WaveOrigin", wave.origin);
        vfx.SetVector3("WaveDirection", wave.direction);
        vfx.SetFloat("WaveAge", wave.age_in_percentage);
        vfx.SetFloat("WaveRange", wave.range);
        vfx.SetFloat("WaveAngle", wave.angle);
        //vfx.SetFloat("WaveThickness", particleSweepThickness);


        // Material
        rippleOriginList[index * 3] = wave.origin.x; rippleOriginList[index * 3 + 1] = wave.origin.y; rippleOriginList[index * 3 + 2] = wave.origin.z;
        rippleDirectionList[index * 3] = wave.direction.x; rippleDirectionList[index * 3 + 1] = wave.direction.y; rippleDirectionList[index * 3 + 2] = wave.direction.z;

        //rippleAliveList[index] = wave.alive;
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

        // set separately 
        matMeshing.SetVector("_WaveOrigin", wave.origin);
        //matMeshing.SetFloat("_WaveSpeed", wave.speed);

    }

    void PushIteratedChanges()
    {

        // VFX, Updated by the latest sound wave
        int cur_wave_index = GetCurrentWaveIndex();
        SoundWave wave = soundwaves[cur_wave_index];

        vfx.SetFloat("WaveRange", wave.range);
        vfx.SetFloat("WaveAge", wave.age_in_percentage);
        //vfx.SetFloat("WaveThickness", wave.thickness);



        // Material
        for (int i = 0; i < MAX_SOUND_WAVE_COUNT; i++)
        {
            //rippleAliveList[i] = soundwaves[i].alive;
            rippleAgeList[i] = soundwaves[i].age_in_percentage;
            rippleRangeList[i] = soundwaves[i].range;
            rippleThicknessList[i] = soundwaves[i].thickness;

            HelperModule.Instance.SetInfo($"Wave{i}_Range", rippleRangeList[i].ToString());
            HelperModule.Instance.SetInfo($"Wave{i}_Thickness", rippleThicknessList[i].ToString());
        }
        //matMeshing.SetFloatArray("rippleAliveList", rippleAliveList);
        matMeshing.SetFloatArray("rippleAgeList", rippleAgeList);
        matMeshing.SetFloatArray("rippleRangeList", rippleRangeList);
        matMeshing.SetFloatArray("rippleThicknessList", rippleThicknessList);

        float src_value = lastSoundVolume;
        float dst_value = debugMode ? testAudioVolume : audioProcessor.AudioVolume;
        float temp_vel = 0;
        matMeshing.SetFloat("_SoundVolume", Mathf.SmoothDamp(src_value, dst_value, ref temp_vel, 0.05f));

        src_value = lastSoundPitch;
        dst_value = debugMode ? testAudioPitch : audioProcessor.AudioPitch;
        temp_vel = 0;
        matMeshing.SetFloat("_SoundPitch", Mathf.SmoothDamp(src_value, dst_value, ref temp_vel, 0.05f));

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
}
