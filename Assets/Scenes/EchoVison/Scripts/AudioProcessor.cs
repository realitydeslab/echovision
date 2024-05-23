using HoloKit.iOS;
using UnityEngine;
using UnityEngine.Audio;

public class AudioProcessor : MonoBehaviour
{
    [Header("Auido Source")]
    public AudioSource microphoneAudioSource;
    public AudioSource masterAudioSource;

    [Header("Auido Mixer")]
    public AudioMixerGroup masterAudioGroup;
    public AudioMixerGroup microphoneAudioGroup;

    /// <summary>
    /// To know the knowledge behind methons, go Notion
    /// </summary>
    public bool useAudioMixerMethod = true;

    bool isInitialized = false;
    
    AudioClip clipRecord;
    string deviceName;

    float audioVolume;
    public float AudioVolume { get { return audioVolume; } }

    float audioPitch;
    public float AudioPitch { get { return audioPitch; } }

    [Header("Holokit Recorder")]
    public HoloKitVideoRecorder videoRecorder;
    public UnityEngine.UI.Text recordingText;

    void Start()
    {
        if(useAudioMixerMethod)
        {
            _samples = new float[QSamples];
            _spectrum = new float[QSamples];
            _fSample = AudioSettings.outputSampleRate;
        }
    }
    float Remap(float v, float src_min, float src_max, float dst_min, float dst_max, bool need_clamp = false)
    {
        if(need_clamp)
            v = Mathf.Clamp(v, src_min, src_max);

        if (src_min == src_max)
            v = 0;
        else
            v = (v - src_min) / (src_max - src_min);

        return v * (dst_max - dst_min) + dst_min;
    }

    
    void Update()
    {
        // Method 1
        if(useAudioMixerMethod == false)
        {
            float level = LevelMax();
            float level_decimal = 20 * Mathf.Log10(Mathf.Abs(level));

            audioVolume = Remap(level_decimal, -30, 0, 0, 1, true);
            audioPitch = 0;
            //Debug.Log(string.Format("LevelMax:{0}, Decimal:{1}", level.ToString("0.0000"), level_decimal.ToString("0.000")));
            //GameManager.Instance.SetInfo("level", level_decimal.ToString("0.000"));
        }
        

        // Method 2
        if(useAudioMixerMethod)
        {
            AnalyzeSound();

            audioVolume = Remap(DbValue, -10, 10, 0, 1, true);
            audioPitch = Remap(PitchValue, 0, 600, 0, 1, true);
            //GameManager.Instance.SetInfo("db", DbValue.ToString("0.000"));
            //GameManager.Instance.SetInfo("pitch", PitchValue.ToString("0.000"));
        }
        //GameManager.Instance.SetInfo("FinalVolume", audioVolume.ToString("0.000"));
    }

    #region Methon One
    int _sampleWindow = 32;
    float LevelMax()
    {
        float levelMax = 0;
        float[] waveData = new float[_sampleWindow];
        int micPosition = Microphone.GetPosition(null) - (_sampleWindow + 1); // null means the first microphone
        if (micPosition < 0) return 0;
        clipRecord.GetData(waveData, micPosition);

        // Getting a peak on the last 128 samples
        for (int i = 0; i < _sampleWindow; i++)
        {
            float wavePeak = waveData[i] * waveData[i];
            if (levelMax < wavePeak)
            {
                levelMax = wavePeak;
            }
        }
        return levelMax;
    }
    #endregion

    #region Method Two
    float RmsValue;
    float DbValue;
    float PitchValue;

    private const int QSamples = 1024;
    private float RefValue = 0.1f;
    private float Threshold = 0.02f;

    float[] _samples;
    float[] _spectrum;
    float _fSample;
    void AnalyzeSound()
    {
        microphoneAudioSource.GetOutputData(_samples, 0); // fill array with samples
        int i;
        float sum = 0;
        for (i = 0; i < QSamples; i++)
        {
            sum += _samples[i] * _samples[i]; // sum squared samples
        }
        RmsValue = Mathf.Sqrt(sum / QSamples); // rms = square root of average
        DbValue = 20 * Mathf.Log10(RmsValue / RefValue); // calculate dB
        if (DbValue < -160) DbValue = -160; // clamp it to -160dB min
                                            // get sound spectrum
        microphoneAudioSource.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        float maxV = 0;
        var maxN = 0;
        for (i = 0; i < QSamples; i++)
        {
            // find max 
            if (!(_spectrum[i] > maxV) || !(_spectrum[i] > Threshold))
                continue;

            maxV = _spectrum[i];
            maxN = i; // maxN is the index of max
        }
        float freqN = maxN; // pass the index to a float variable
        if (maxN > 0 && maxN < QSamples - 1)
        { // interpolate index using neighbours
            var dL = _spectrum[maxN - 1] / _spectrum[maxN];
            var dR = _spectrum[maxN + 1] / _spectrum[maxN];
            freqN += 0.5f * (dR * dR - dL * dL);
        }
        PitchValue = freqN * (_fSample / 2) / QSamples; // convert index to frequency
    }
    #endregion



    void StartMicrophone()
    {
        if (isInitialized)
            return;

        // List devices
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("No microphone device found!");
            return;
        }            
        //for (int i = 0; i < Microphone.devices.Length; i++)
        //{
        //    Debug.Log(string.Format("Mic{0}:{1}", i, Microphone.devices[i]));
        //}

        if (deviceName == null)
            deviceName = Microphone.devices[0];

        // Start microphone
        clipRecord = Microphone.Start(deviceName, true, 1, 44100);
        if(clipRecord == null)
        {
            Debug.Log("Failed to start microphone");
            return;
        }

        // Send clip to audio source
        //microphoneAudioSource = GetComponent<AudioSource>();
        microphoneAudioSource.clip = clipRecord;
        microphoneAudioSource.loop = true;
        microphoneAudioSource.volume = 1;
        
        masterAudioSource.clip = clipRecord;
        masterAudioSource.loop = true;
        masterAudioSource.volume = 0.01f; // Can not be set to zero cause that will cut off the data stream

        if (useAudioMixerMethod)
        {
            microphoneAudioSource.outputAudioMixerGroup = microphoneAudioGroup;
            masterAudioSource.outputAudioMixerGroup = masterAudioGroup;

            // loop to clear buffer
            while (!(Microphone.GetPosition(null) > 0))
            {
            }

            microphoneAudioSource.Play();
            //masterAudioSource.Play();
        }


        isInitialized = Microphone.IsRecording(null);
        //Debug.Log("Started. IsRecording:" + Microphone.IsRecording(null));
    }

    void StopMicrophone()
    {
        Microphone.End(deviceName);

        isInitialized = Microphone.IsRecording(null);
        //Debug.Log("Stopped. IsRecording:" + Microphone.IsRecording(null));
    }


    void OnEnable()
    {
        //Debug.Log("OnEnable:Start");
        StartMicrophone();

    }

    void OnDisable()
    {
        //Debug.Log("OnDisable:End");
        StopMicrophone();
    }

    void OnDestroy()
    {
        //Debug.Log("OnDestroy:End");
        StopMicrophone();
    }
    /// <summary>
    /// Caution!
    /// Will execute even the script is disabled.
    /// </summary>
    void OnApplicationFocus(bool focus)
    {
        if (focus)
        {
            //Debug.Log("OnFocus:Start");
            StartMicrophone();
        }

        else
        {
            //Debug.Log("OnLoseFocus:End");
            StopMicrophone();
        } 
    }
    

    public void ToggleRecording()
    {
        if (videoRecorder.IsRecording == false)
        {
            //videoRecorder._microphoneAudioSource = microphoneAudioSource;
            videoRecorder._microphoneAudioSource = masterAudioSource;

            //masterAudioSource.Play();
            //if (useAudioMixerMethod)
            //{
            //    microphoneAudioSource.outputAudioMixerGroup = masterAudioGroup;
            //}
            //else
            //{
            //    while (!(Microphone.GetPosition(null) > 0))
            //    {
            //    }
            //    if (microphoneAudioSource.isPlaying == false)
            //        microphoneAudioSource.Play();
            //}
        }
        else
        {
            if(useAudioMixerMethod)
            {
                //microphoneAudioSource.outputAudioMixerGroup = microphoneAudioGroup;
                //masterAudioSource.Stop();
            }
            else
            {
                if (microphoneAudioSource.isPlaying)
                    microphoneAudioSource.Stop();
            }
        }
        videoRecorder.ToggleRecording();
        recordingText.text = videoRecorder.IsRecording ? "Stop Recording" : "Start Recording";
    }
}