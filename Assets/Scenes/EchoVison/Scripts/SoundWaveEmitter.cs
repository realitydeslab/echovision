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

public class SoundWaveEmitter : MonoBehaviour
{
    public Volume volume;
    Bloom bloom;

    public VisualEffect vfx;
    public Material matMeshing;
    public Material matShield;
    public Transform shieldRoot;
    public GameObject prefabShield;


    public Vector2 soundwaveLife = new Vector2(4, 6);
    public Vector2 soundwaveSpeed = new Vector2(1, 4);
    public Vector2 soundwaveStrength = new Vector2(0, 1);
    public Vector2 soundwaveAngle = new Vector2(90, 180);


    public float minWaveThickness = 1;

    public float emitThreshold = 0.02f;

    public bool debugMode = false;
    [Range(0.0f, 1.0f)]
    public float testAge = 0;
    [Range(0.0f, 10.0f)]
    public float testRange = 1;
    [Range(0.0f, 180.0f)]
    public float testAngle = 90;
    [Range(0.01f, 5.0f)]
    public float testThickness = 2f;

#if USE_SOUNDWAVE_ATTRACTOR
    public GameObject attractorPrefab;
    public bool useAttractor = false;
#endif


    const int MAX_SOUND_WAVE_COUNT = 1;
    SoundWave[] soundwaves = new SoundWave[MAX_SOUND_WAVE_COUNT];

    int nextEmitIndex = -1;
    float[] rippleOriginList;
    float[] rippleDirectionList;
    float[] rippleAliveList;
    float[] rippleAgeList;
    float[] rippleRangeList;
    float[] rippleAngleList;
    float[] rippleThicknessList;

    Material[] shieldMaterialList;
    float[] tempVector3Array = new float[3];

    Transform tfHead;
    ARMeshManager m_MeshManager = null;
    HoloKitCameraManager m_HoloKitCameraManager;
    ScreenRenderMode renderMode;
    


    void Start()
    {
        tfHead = FindObjectOfType<TrackedPoseDriver>().transform;
        m_MeshManager = FindObjectOfType<ARMeshManager>();
        m_HoloKitCameraManager = FindObjectOfType<HoloKitCameraManager>();
        renderMode = m_HoloKitCameraManager.ScreenRenderMode;

        VolumeProfile profile = volume.sharedProfile;        
        profile.TryGet<Bloom>(out bloom);

        // init soundwave and attractors
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
        }

        /*
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
        */

        // init mat related parameters
        rippleOriginList = new float[MAX_SOUND_WAVE_COUNT * 3];
        rippleDirectionList = new float[MAX_SOUND_WAVE_COUNT * 3];
        rippleAliveList = new float[MAX_SOUND_WAVE_COUNT];
        rippleAgeList = new float[MAX_SOUND_WAVE_COUNT];
        rippleRangeList = new float[MAX_SOUND_WAVE_COUNT];
        rippleAngleList = new float[MAX_SOUND_WAVE_COUNT];
        rippleThicknessList = new float[MAX_SOUND_WAVE_COUNT];

        for (int i = 0; i < MAX_SOUND_WAVE_COUNT; i++)
        {
            rippleAgeList[i] = 1;
            rippleRangeList[i] = 0;
        }
        if(debugMode == false)
        {
            PushInitialChanges(0);
        }
        

        // chech if need to enter debug mode
        vfx.SetBool("DebugMode", debugMode);
        //matMeshing.SetInt("DebugMode", debugMode?1:0);
        //matShield.SetInt("DebugMode", debugMode ? 1 : 0);

#if UNITY_IOS
        vfx.SetBool("DebugMode", false);
#endif

#if UNITY_IOS && !UNITY_EDITOR
        //matMeshing.SetInt("_DebugMode", 0);
        matShield.SetInt("_DebugMode", 0);
#endif

    }

    void Update()
    {
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

        // Emit
        if (Input.GetMouseButton(0) ||  MicInput.MicLoudness > emitThreshold)
        {
            EmitSoundWave();
        }
        else
        {
            EndSoundWave();
        }
        matMeshing.SetFloat("SoundVolume", MicInput.MicLoudness);

        //
        //UpdtaeMeshBounds();

        float max_bloom = 0;
        // Update sound wave
        for (int i=0; i<soundwaves.Length; i++)
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

            if (debugMode)
            {
                if (i == 0)
                {
                    wave.origin = tfHead.position;
                    wave.direction = Quaternion.Euler(tfHead.eulerAngles) * Vector3.forward;

                    wave.alive = 1;
                    wave.age_in_percentage = testAge;
                    wave.range = testRange;
                    wave.angle = testAngle;
                    wave.thickness = testThickness;

                    //shieldRoot.GetChild(i).position = tfHead.position;
                    //shieldRoot.GetChild(i).eulerAngles = tfHead.eulerAngles;

                    PushInitialChanges(i);
                }
                else
                {
                    wave.alive = 0;
                    wave.age = 0;
                    wave.range = 0;
                }
            }
            else
            {
                //Debug.Log(string.Format("Update{0}:alive:{1}, age:{2}, range:{3}, angle:{4}, thickness:{5}, origin:{6}, dir:{7}",
                //    i, wave.alive, wave.age, wave.range, wave.angle, wave.thickness, wave.origin, wave.direction));

                // If wave is dead
                if (wave.alive == 0)
                {
                    wave.age -= Time.deltaTime;
                    if (wave.age <= 0)
                    {
                        wave.age = 0;
                    }
                    else
                    {
                        //Debug.Log("dying"+i.ToString() + ","+ wave.age.ToString());

                        wave.range += wave.speed * Time.deltaTime;
                    }

                    // check if it's still spreading
                    if (wave.thickness > 0)
                    {
                        wave.thickness -= wave.speed * Time.deltaTime;// * 2;
                        if (wave.thickness < 0)
                            wave.thickness = 0;
                    }
                }

                if(wave.alive == 1)
                {
                    wave.range += wave.speed * Time.deltaTime;
                    wave.age += Time.deltaTime;
                    if (wave.age >= wave.life)
                        wave.age = wave.life;
                }

                //Debug.Log("growing" + i.ToString() + "," + wave.age.ToString());
                //float bloom_value = 1 - Mathf.Abs((wave.age_in_percentage - 0.5f) * 2);
                //max_bloom = Mathf.Max(max_bloom, bloom_value);
            }
        }

        // Change Bloom
        //bloom.intensity.value = max_bloom * 2f;

        // push changes to VFX and Mat
        PushIteratedChanges();
    }

  

    public void EmitSoundWave(float volume = 1, float pitch = 1)
    {
        EmitSoundWave(tfHead.position, Quaternion.Euler(tfHead.eulerAngles) * Vector3.forward, volume, pitch);
    }

    void EmitSoundWave(Vector3 pos, Vector3 dir, float volume = 1, float pitch = 1)
    {
        
        int cur_emit_index;
        if(nextEmitIndex == -1)
        {
            cur_emit_index = 0;
            nextEmitIndex = 0;
        }
        else
        {
            cur_emit_index = nextEmitIndex == 0 ? MAX_SOUND_WAVE_COUNT - 1 : nextEmitIndex - 1;
        }

        SoundWave wave = soundwaves[cur_emit_index];
        // If current wave is dead
        if(wave.alive == 0)
        {
            Debug.Log(string.Format("Emit New One. Cur{0}, Next{1}", cur_emit_index, nextEmitIndex));
            SoundWave new_wave = soundwaves[nextEmitIndex];
            new_wave.origin = pos;
            new_wave.direction = dir;
            new_wave.range = 0;
            new_wave.age = 0;
            new_wave.alive = 1;

            new_wave.speed = Random.Range(soundwaveSpeed.x, soundwaveSpeed.y) * pitch; // relative to pitch
            new_wave.life = Random.Range(soundwaveLife.x, soundwaveLife.y) * pitch; // relative to pitch

            new_wave.strength = Random.Range(soundwaveStrength.x, soundwaveStrength.y) * volume; // relative to volume
            new_wave.angle = Random.Range(soundwaveAngle.x, soundwaveAngle.y) * volume; // relative to volume
            //next_wave.thickness = Random.Range(soundwaveThickness.x, soundwaveThickness.y) * volume; // relative to volume
            new_wave.thickness = 0;


            PushInitialChanges(nextEmitIndex);


            nextEmitIndex = nextEmitIndex + 1;
            if (nextEmitIndex >= MAX_SOUND_WAVE_COUNT)
            {
                nextEmitIndex = 0;
            }

        }

        // If current wave still is still on going, then update thickness
        if (wave.alive == 1)
        {
            wave.thickness = wave.range;
        }


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
        /*
        // Emit Shield
        Transform shield_object = shieldRoot.GetChild(nextEmitIndex);
        shield_object.position = wave.origin;
        shield_object.eulerAngles = tfHead.eulerAngles;
        */




    }

    void EndSoundWave()
    {
        if (nextEmitIndex == -1)
            return;

        int cur_emit_index = nextEmitIndex == -1 ? 0 : (nextEmitIndex == 0 ? MAX_SOUND_WAVE_COUNT - 1 : nextEmitIndex - 1);

        // Set all waves to dead except for the wave that has not reached to minimum thickness
        for(int i =0; i< soundwaves.Length; i++)
        {
            SoundWave wave = soundwaves[i];
            if (wave.alive == 1)
            {
                if(wave.thickness < minWaveThickness)
                {
                    wave.thickness = wave.range;
                }
                if(wave.age < wave.life)
                {
                    wave.age += wave.speed * Time.deltaTime;
                }

                if(wave.thickness >= minWaveThickness && wave.age >= wave.life)
                {
                    wave.alive = 0;
                }
            }
            
        }
    }

    void UpdtaeMeshBounds()
    {
        if (m_MeshManager == null)
            return;

        IList<MeshFilter> mesh_list = m_MeshManager.meshes;

        if (mesh_list != null)
        {
            int mesh_count = mesh_list.Count;
            Vector3 min_pos = Vector3.zero;
            Vector3 max_pos = Vector3.zero;

            foreach (MeshFilter mesh in mesh_list)
            {
                min_pos = Vector3.Min(min_pos, mesh.sharedMesh.bounds.min);
                max_pos = Vector3.Max(max_pos, mesh.sharedMesh.bounds.max);
            }

            vfx.SetVector3("BoundsMin", min_pos);
            vfx.SetVector3("BoundsMax", max_pos);
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
        vfx.SetFloat("WaveThickness", testThickness);


        // Ripple
        rippleOriginList[index * 3] = wave.origin.x; rippleOriginList[index * 3 + 1] = wave.origin.y; rippleOriginList[index * 3 + 2] = wave.origin.z;
        rippleDirectionList[index * 3] = wave.direction.x; rippleDirectionList[index * 3 + 1] = wave.direction.y; rippleDirectionList[index * 3 + 2] = wave.direction.z;

        rippleAliveList[index] = wave.alive;
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

        matMeshing.SetVector("WaveOrigin", wave.origin);
        matMeshing.SetFloat("WaveSpeed", wave.speed);

        int i = index;
        Debug.Log(string.Format("Wave{0}:alive:{1}, range:{2}, angle:{3}, thickness:{4}, origin:{5}, dir:{6}",
            i, wave.alive, wave.range, wave.angle, wave.thickness, wave.origin, wave.direction));

        /*
        // Shield
        Material matShield = shieldMaterialList[index];
        matShield.SetVector("_Origin", wave.origin);
        matShield.SetVector("_Direction", wave.direction);

        matShield.SetFloat("_Range", wave.range);
        matShield.SetFloat("_Age", wave.age_in_percentage);
        matShield.SetFloat("_Angle", wave.angle);
        */
    }

    void PushIteratedChanges()
    {
        if (nextEmitIndex == -1)
            return;

        // VFX, only update one vfx effect according to the last emmission
        SoundWave wave = soundwaves[nextEmitIndex == 0 ? MAX_SOUND_WAVE_COUNT - 1 : nextEmitIndex - 1];
        if (debugMode)
        {
            wave = soundwaves[0];
        }
        vfx.SetFloat("WaveRange", wave.range);
        vfx.SetFloat("WaveAge", wave.age_in_percentage);
        //vfx.SetFloat("WaveThickness", wave.thickness);


        // Ripple
        for (int i = 0; i < MAX_SOUND_WAVE_COUNT; i++)
        {
            //rippleAliveList[i] = soundwaves[i].alive;
            rippleAgeList[i] = soundwaves[i].age_in_percentage;
            rippleRangeList[i] = soundwaves[i].range;
            rippleThicknessList[i] = soundwaves[i].thickness;
            //Debug.Log(string.Format("Wave{0}:age:{1}, range:{2}, angle:{3}, thickness:{4}, origin:{5}, dir:{6}",
            //    i, soundwaves[i].age_in_percentage, soundwaves[i].range, soundwaves[i].angle, soundwaves[i].thickness, soundwaves[i].origin, soundwaves[i].direction));
        }
        //matMeshing.SetFloatArray("rippleAliveList", rippleAliveList);
        matMeshing.SetFloatArray("rippleAgeList", rippleAgeList);
        matMeshing.SetFloatArray("rippleRangeList", rippleRangeList);
        matMeshing.SetFloatArray("rippleThicknessList", rippleThicknessList);



        /*
        // Shield
        for (int i = 0; i < MAX_SOUND_WAVE_COUNT; i++)
        {
            shieldMaterialList[i].SetFloat("_Age", soundwaves[i].age_in_percentage);
            shieldMaterialList[i].SetFloat("_Range", soundwaves[i].range);

            //Debug.Log(string.Format("Shield{0}:age:{1}, range:{2}, angle:{3}, thickness:{4}, origin:{5}, dir:{6}",
            //    i, soundwaves[i].age_in_percentage, soundwaves[i].range, soundwaves[i].angle, soundwaves[i].thickness, soundwaves[i].origin, soundwaves[i].direction));
        }
        */
    }

    float[] Vector3ToArray(Vector3 vec)
    {
        float[] array = new float[3];
        array[0] = vec.x;
        array[1] = vec.y;
        array[2] = vec.z;
        return array;
    }
}
