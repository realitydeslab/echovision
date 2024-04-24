using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using HoloKit.iOS;

public class MicInput : MonoBehaviour
{

    public static float MicLoudness;

    private string _device;

    public UnityEvent<float, float> OnSoundPlay;

    public TextMeshProUGUI volumeText;
    public UnityEngine.UI.Text recordingText;

    //
    float _lastTriggerTime = 0f;
    float _triggerIntervel = 0.5f;
    public HoloKitVideoRecorder videoRecorder;

    //mic initialization
    void InitMic()
    {
        if (_device == null) _device = Microphone.devices[0];
         _clipRecord = Microphone.Start(_device, true, 1, 44100);
    }

    void StopMicrophone()
    {
        Microphone.End(_device);
    }


    public AudioClip _clipRecord;
    int _sampleWindow = 32;

    //get data from microphone into audioclip
    float LevelMax()
    {
        float levelMax = 0;
        float[] waveData = new float[_sampleWindow];
        int micPosition = Microphone.GetPosition(null) - (_sampleWindow + 1); // null means the first microphone
        if (micPosition < 0) return 0;
        _clipRecord.GetData(waveData, micPosition);
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



    void Update()
    {
        // levelMax equals to the highest normalized value power 2, a small number because < 1
        // pass the value to a static var so we can access it from anywhere
        MicLoudness = LevelMax();
        volumeText.text = "Vol:" + MicLoudness.ToString("0.00");
        if (MicLoudness > 0.05f)
        {
            if(Time.time - _lastTriggerTime > _triggerIntervel)
            {
                float volume = (Mathf.Clamp01(MicLoudness) / 0.20f);// 1;
                float pitch = 1;
                //OnSoundPlay?.Invoke(volume, pitch);
                //Debug.Log("OnSoundPlay with a volume: " + MicLoudness);
                _lastTriggerTime = Time.time;
            }

        }
    }

    bool _isInitialized;
    // start mic when scene starts
    void OnEnable()
    {
        InitMic();
        _isInitialized = true;
    }

    //stop mic when loading a new level or quit application
    void OnDisable()
    {
        StopMicrophone();
    }

    void OnDestroy()
    {
        StopMicrophone();
    }


    // make sure the mic gets started & stopped when application gets focused
    void OnApplicationFocus(bool focus)
    {
        if (focus)
        {
            //Debug.Log("Focus");

            if (!_isInitialized)
            {
                //Debug.Log("Init Mic");
                InitMic();
                _isInitialized = true;
            }
        }
        if (!focus)
        {
            //Debug.Log("Pause");
            StopMicrophone();
            //Debug.Log("Stop Mic");
            _isInitialized = false;

        }
    }

    float rmsVal;
    float dbVal;
    float pitchVal;
    void AnalyzeSound(float [] _samples, int QSamples, float[] _spectrum, int _fSample = 256, float RefValue = 0.1f, float Threshold = 0.02f)
    {
        GetComponent<AudioSource>().GetOutputData(_samples, 0); // fill array with samples
        int i;
        float sum = 0;
        for (i = 0; i < QSamples; i++)
        {
            sum += _samples[i] * _samples[i]; // sum squared samples
        }
        rmsVal = Mathf.Sqrt(sum / QSamples); // rms = square root of average
        dbVal = 20 * Mathf.Log10(rmsVal / RefValue); // calculate dB
        if (dbVal < -160) dbVal = -160; // clamp it to -160dB min
                                        // get sound spectrum
        GetComponent<AudioSource>().GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        float maxV = 0;
        var maxN = 0;
        for (i = 0; i < QSamples; i++)
        { // find max 
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
        pitchVal = freqN * (_fSample / 2) / QSamples; // convert index to frequency
    }

    public void ToggleRecording()
    {
        if(videoRecorder.IsRecording == false)
        {
            videoRecorder._microphoneAudioSource.clip = _clipRecord;
        }
        videoRecorder.ToggleRecording();
        recordingText.text = videoRecorder.IsRecording ? "Stop Recording" : "Start Recording";
        
    }
}
