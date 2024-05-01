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

public class SoundWaveEmitter : MonoBehaviour
{

    [Header("References")]
    public VisualEffect vfx;
    public Material matMeshing;
    public Volume volume;
    Bloom bloom;

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


#if USE_SOUNDWAVE_ATTRACTOR
    public GameObject attractorPrefab;
    public bool useAttractor = false;
#endif
#if USE_SHIELD
    public Material matShield;
    public Transform shieldRoot;
    public GameObject prefabShield;
    Material[] shieldMaterialList;
#endif

    void Start()
    {
        VolumeProfile profile = volume.sharedProfile;        
        profile.TryGet<Bloom>(out bloom);
        


        // Init Soundwave and Attractors
        for (int i= 0; i < soundwaves.Length; i++)
        {
            soundwaves[i] = new SoundWave();

#if USE_SOUNDWAVE_ATTRACTOR
            for(int k=0; k< soundwaves[i].attactors.Length; k++)
            {
                WaveAttractor attractor = new WaveAttractor();
                attractor.sphere = Instantiate(attractorPrefab, this.transform).transform;
                attractor.sphere.name = string.Format("Wave{0}_Attractor{1}", i, k);
                attractor.sphere.gameObject.SetActive(false);

                soundwaves[i].attactors[k] = attractor;
            }
#endif
            DeactivateSoundWave(i);
        }


#if USE_SHIELD
        // init shield
        shieldMaterialList = new Material[MAX_SOUND_WAVE_COUNT];
        for (int i= shieldRoot.childCount; i< MAX_SOUND_WAVE_COUNT; i++)
        {
            GameObject new_shield = Instantiate(prefabShield, shieldRoot);
            MeshRenderer shield_mat = new_shield.GetComponent<MeshRenderer>();
            shield_mat.material = new Material(shield_mat.material);
        }
        for (int i = 0; i < shieldRoot.childCount; i++)
        {
            shieldMaterialList[i] = shieldRoot.GetChild(i).GetComponent<MeshRenderer>().material;
        }
#endif


        // Init Mat Related Parameters
        rippleOriginList = new float[MAX_SOUND_WAVE_COUNT * 3];
        rippleDirectionList = new float[MAX_SOUND_WAVE_COUNT * 3];
        //rippleAliveList = new float[MAX_SOUND_WAVE_COUNT];
        rippleAgeList = new float[MAX_SOUND_WAVE_COUNT];
        rippleRangeList = new float[MAX_SOUND_WAVE_COUNT];
        rippleAngleList = new float[MAX_SOUND_WAVE_COUNT];
        rippleThicknessList = new float[MAX_SOUND_WAVE_COUNT];

      
                


        // Check if need to enter debug mode
        //vfx.SetBool("DebugMode", debugMode);
        //matMeshing.SetInt("DebugMode", debugMode?1:0);
        //matShield.SetInt("DebugMode", debugMode ? 1 : 0);

#if UNITY_IOS
        vfx.SetBool("DebugMode", false);
        matMeshing.SetInt("_DebugMode", 0);
#endif

#if UNITY_IOS && !UNITY_EDITOR
        //matMeshing.SetInt("_DebugMode", 0);
        //matShield.SetInt("_DebugMode", 0);
#endif

    }

    void Update()
    {

#if USE_SHIELD
        // Shield can not be occuluded in Stereo Mode and it's not been resloved
        //if (renderMode != m_HoloKitCameraManager.ScreenRenderMode)
        //{
        //    renderMode = m_HoloKitCameraManager.ScreenRenderMode;
        //    if (renderMode == ScreenRenderMode.Mono)
        //    {
        //        shieldRoot.gameObject.SetActive(true);
        //    }
        //    else
        //    {
        //        shieldRoot.gameObject.SetActive(false);
        //    }
        //}

        // shieldRoot.gameObject.SetActive(false); // remove shield anyway
#endif

        // Emit New SoundWave
        if (Input.GetMouseButton(0) || GameManager.Instance.AudioVolume > emitVolumeThreshold)
        {
            //Debug.Log("Start");
            EmitSoundWave(GameManager.Instance.AudioVolume, GameManager.Instance.AudioPitch);
        }
        else
        {
            //Debug.Log("End");
            EndSoundWave();
        }

        
        // Update Extant SoundWave        
        Transform head_transform = GameManager.Instance.HeadTransform;
        float max_bloom = 0;
        for (int i = 0; i < soundwaves.Length; i++)
        {
#if USE_SOUNDWAVE_ATTRACTOR
            // update attractor
            if (useAttractor)
            {
                foreach (WaveAttractor attractor in wave.attactors)
                {
                    if (attractor.age >= attractor.life)
                        continue;

                    attractor.age += Time.deltaTime;
                    if (attractor.age > attractor.life)
                    {
                        attractor.age = attractor.life;
                        attractor.sphere.gameObject.SetActive(false);
                    }
                }
            }
#endif
            SoundWave wave = soundwaves[i];

            //PrintDebugInfo("Update", i);

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
                    wave.age = wave.life;
            }

            // IF Wave need to die (player stopped making sound)
            if (wave.alive == 0)
            {
                // if sound span is too short, force wave to last for at least a minimum thickness
                if(wave.thickness < minWaveThickness)
                {
                    wave.thickness = wave.range;
                }

                wave.age -= Time.deltaTime;
                if (wave.age < 0)
                    wave.age = 0;
            }
        }


        // If in Debug Mode, rewrite data
        if (debugMode)
        {
            for (int i = 0; i < soundwaves.Length; i++)
            {
                SoundWave wave = soundwaves[i];
                // Activate Wave0
                if (i == 0)
                {
                    wave.origin = head_transform.position;
                    wave.direction = Quaternion.Euler(head_transform.eulerAngles) * Vector3.forward;

                    wave.alive = 1;
                    wave.age_in_percentage = testAge;
                    wave.range = testRange;
                    wave.angle = testAngle;
                    wave.thickness = testThickness;

#if USE_SHIELD
                    shieldRoot.GetChild(i).position = head_transform.position;
                    shieldRoot.GetChild(i).eulerAngles = head_transform.eulerAngles;
#endif
                    PushInitialChanges(i);
                }

                // Deactivate others
                else
                {
                    DeactivateSoundWave(i);
                }
            }
        }
        

        // Push changes to VFX and Mat
        PushIteratedChanges();



        // Alter Post-Processing effects
        //bloom.intensity.value = max_bloom * 2f;

        Texture2D human_tex = GameManager.Instance.OcclusionManager.humanStencilTexture;

        if (human_tex != null && GameManager.Instance.DepthImageProcessor != null)
        {
            human_tex.wrapMode = TextureWrapMode.Repeat;
            vfx.SetTexture("HumanStencilTexture", human_tex);
            vfx.SetMatrix4x4("HumanStencilTextureMatrix", GameManager.Instance.DepthImageProcessor.DisplayRotatioMatrix);
            matMeshing.SetTexture("_HumanStencilTexture", human_tex);
            matMeshing.SetMatrix("_HumanStencilTextureMatrix", GameManager.Instance.DepthImageProcessor.DisplayRotatioMatrix);
            //GameManager.Instance.SetInfo("Matrix", depthImageProcessor.DisplayRotatioMatrix.ToString());
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

    public void EmitSoundWave(float volume = 1, float pitch = 1)
    {
        Transform head_transform = GameManager.Instance.HeadTransform;
        EmitSoundWave(head_transform.position, Quaternion.Euler(head_transform.eulerAngles) * Vector3.forward, volume, pitch);
    }

    void EmitSoundWave(Vector3 pos, Vector3 dir, float volume = 1, float pitch = 1)
    {

        int cur_emit_index = GetCurrentWaveIndex();
        SoundWave wave = soundwaves[cur_emit_index];

        // If current wave still is still on going, then do nothing
        if (wave.alive == 1)
            return;

        // Emit New One
        int next_emit_index = GetNextWaveIndex();
        SoundWave new_wave = soundwaves[next_emit_index];
        new_wave.origin = pos;
        new_wave.direction = dir;

        new_wave.speed = Random.Range(soundwaveSpeed.x, soundwaveSpeed.y);// * pitch; 
        //new_wave.life = Random.Range(soundwaveLife.x, soundwaveLife.y) * pitch; // relative to pitch
        //new_wave.strength = Random.Range(soundwaveStrength.x, soundwaveStrength.y) * volume; // relative to volume
        new_wave.angle = Utilities.Remap(volume, 0, 1, soundwaveAngle.x, soundwaveAngle.y);// Random.Range(soundwaveAngle.x, soundwaveAngle.y) * volume; // relative to volume


        ActivateSoundWave(next_emit_index);

        PushInitialChanges(next_emit_index);

        MoveWaveIndex();

        


#if USE_SOUNDWAVE_ATTRACTOR
        // Emit Attractors
        if (useAttractor)
        {
            float attractor_angle = 0;
            float attractor_angle_interval = wave.angle / (wave.attactors.Length - 1);
            float wiggle_angle = 5;
            foreach (WaveAttractor attractor in wave.attactors)
            {
                attractor.position = pos;
                attractor.speed = dir;// Quaternion.Euler(Random.Range(0f, wiggle_angle), wave.angle * -0.5f + attractor_angle + Random.Range(0f, wiggle_angle), 0) * dir;
                attractor.speed.Normalize();
                attractor.speed *= Random.Range(soundwaveSpeed.x, soundwaveSpeed.y) * pitch;
                attractor.sphere.gameObject.SetActive(true);
                attractor.sphere.position = pos;
                attractor.sphere.GetComponent<Rigidbody>().velocity = attractor.speed;


                attractor.strength = Random.Range(soundwaveStrength.x, soundwaveStrength.y) * volume;
                attractor.life = Random.Range(soundwaveLife.x, soundwaveLife.y) * pitch;
                attractor.age = 0;


                attractor_angle += attractor_angle_interval;
            }
        }
#endif

#if USE_SHIELD
        /*
        // Emit Shield
        Transform shield_object = shieldRoot.GetChild(nextEmitIndex);
        shield_object.position = wave.origin;
        shield_object.eulerAngles = tfHead.eulerAngles;
        */
#endif

    }

    void EndSoundWave()
    {
        // Set ALL waves to dead. Except for the wave that has not reached to minimum thickness or minimum age
        for(int i =0; i< soundwaves.Length; i++)
        {
            SoundWave wave = soundwaves[i];
            wave.alive = 0;
        }
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
        matMeshing.SetFloat("_WaveSpeed", wave.speed);

#if USE_SHIELD
        // Shield
        Material matShield = shieldMaterialList[index];
        matShield.SetVector("_Origin", wave.origin);
        matShield.SetVector("_Direction", wave.direction);

        matShield.SetFloat("_Range", wave.range);
        matShield.SetFloat("_Age", wave.age_in_percentage);
        matShield.SetFloat("_Angle", wave.angle);
#endif
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
        }
        //matMeshing.SetFloatArray("rippleAliveList", rippleAliveList);
        matMeshing.SetFloatArray("rippleAgeList", rippleAgeList);
        matMeshing.SetFloatArray("rippleRangeList", rippleRangeList);
        matMeshing.SetFloatArray("rippleThicknessList", rippleThicknessList);

        float src_value = lastSoundVolume;
        float dst_value = debugMode ? testAudioVolume : GameManager.Instance.AudioVolume;
        float temp_vel = 0;
        matMeshing.SetFloat("_SoundVolume", Mathf.SmoothDamp(src_value, dst_value, ref temp_vel, 0.1f));

        src_value = lastSoundPitch;
        dst_value = debugMode ? testAudioPitch : GameManager.Instance.AudioPitch;
        temp_vel = 0;
        matMeshing.SetFloat("_SoundPitch", Mathf.SmoothDamp(src_value, dst_value, ref temp_vel, 0.1f));



#if USE_SHIELD
        // Shield
        for (int i = 0; i < MAX_SOUND_WAVE_COUNT; i++)
        {
            shieldMaterialList[i].SetFloat("_Age", soundwaves[i].age_in_percentage);
            shieldMaterialList[i].SetFloat("_Range", soundwaves[i].range);

            //Debug.Log(string.Format("Shield{0}:age:{1}, range:{2}, angle:{3}, thickness:{4}, origin:{5}, dir:{6}",
            //    i, soundwaves[i].age_in_percentage, soundwaves[i].range, soundwaves[i].angle, soundwaves[i].thickness, soundwaves[i].origin, soundwaves[i].direction));
        }
#endif
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
