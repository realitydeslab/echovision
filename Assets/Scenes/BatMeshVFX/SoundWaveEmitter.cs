using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.VFX;
using UnityEngine.XR.ARFoundation;

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

public class SoundWave
{
    const int ATTACTOR_COUNT_EACH_WAVE = 5;

    public Vector3 origin;
    public Vector3 direction;
    public float range;
    public float speed;
    public float angle;
    public float strength;
    public float age;
    public float life;

    public WaveAttractor[] attactors;

    public SoundWave()
    {
        origin = direction = Vector3.zero;
        range = speed = angle = strength = age = 0;
        attactors = new WaveAttractor[ATTACTOR_COUNT_EACH_WAVE];
    }
}


public class SoundWaveEmitter : MonoBehaviour
{
    const int MAX_SOUND_WAVE_COUNT = 10;
    public VisualEffect vfx;

    public Vector2 soundwaveLife = new Vector2(4, 6);
    public Vector2 soundwaveStrength = new Vector2(0, 1);
    public Vector2 soundwaveAngle = new Vector2(90, 180);
    public Vector2 soundwaveSpeed = new Vector2(1, 4);

    public GameObject attractorPrefab;

    SoundWave[] soundwaves = new SoundWave[MAX_SOUND_WAVE_COUNT];
    int nextEmitIndex = 0;

    Transform tfHead;
    ARMeshManager m_MeshManager;

    public bool useAttractor = false;

    void Start()
    {
        tfHead = FindObjectOfType<TrackedPoseDriver>().transform;
        m_MeshManager = FindObjectOfType<ARMeshManager>();

        // init attractors
        for (int i= 0; i < soundwaves.Length; i++)
        {
            soundwaves[i] = new SoundWave();
            for(int k=0; k< soundwaves[i].attactors.Length; k++)
            {
                WaveAttractor attractor = new WaveAttractor();
                attractor.sphere = Instantiate(attractorPrefab, this.transform).transform;
                attractor.sphere.name = string.Format("Wave{0}_Attractor{1}", i, k);
                attractor.sphere.gameObject.SetActive(false);

                soundwaves[i].attactors[k] = attractor;
            }
        }

#if UNITY_IOS
        vfx.SetBool("DebugMode", false);
#endif
    }

    void Update()
    {
        // Emit
        if (Input.GetMouseButtonDown(0))
        {
            EmitSoundWave();
        }


        //
        UpdtaeMeshBounds();


        // Update sound wave
        foreach (SoundWave wave in soundwaves)
        {
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
            

            // update wave itself
            wave.age += Time.deltaTime;
            if(wave.age > wave.life)
            {
                wave.age = wave.life;
                continue;
            }

            wave.range += wave.speed * Time.deltaTime;


            vfx.SetFloat("WaveRange", wave.range);
            vfx.SetFloat("WaveAge", wave.range);
        }
    }

    public void EmitSoundWave()
    {
        EmitSoundWave(tfHead.position, Quaternion.Euler(tfHead.eulerAngles) * Vector3.forward);
    }

    void EmitSoundWave(Vector3 pos, Vector3 dir, float volume = 1, float pitch = 1)
    {

        // Emit Wave
        SoundWave wave = soundwaves[nextEmitIndex];

        wave.origin = pos;
        wave.direction = dir;
        wave.range = 0;
        wave.age = 0;

        wave.speed = Random.Range(soundwaveSpeed.x, soundwaveSpeed.y) * pitch; // relative to pitch
        wave.life = Random.Range(soundwaveLife.x, soundwaveLife.y) * pitch; // relative to pitch
        wave.strength = Random.Range(soundwaveStrength.x, soundwaveStrength.y) * volume; // relative to volume
        wave.angle = Random.Range(soundwaveAngle.x, soundwaveAngle.y) * volume; // relative to volume



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
        


        // Move index
        nextEmitIndex++;
        if(nextEmitIndex >= MAX_SOUND_WAVE_COUNT)
        {
            nextEmitIndex = 0;
        }


        // Set VFX
        vfx.SetVector3("WaveOrigin", wave.origin);
        vfx.SetVector3("WaveDirection", wave.direction);
        vfx.SetFloat("WaveRange", wave.range);
        vfx.SetFloat("WaveAge", wave.range);

        //vfx.SetFloat("WaveRange", wave.range);
        //vfx.SetFloat("WaveRange", wave.range);
        //vfx.SetFloat("WaveRange", wave.range);
        //vfx.SetFloat("WaveRange", wave.range);
    }

    void UpdtaeMeshBounds()
    {
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
}
