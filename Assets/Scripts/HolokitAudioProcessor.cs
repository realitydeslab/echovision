using UnityEngine;
using UnityEngine.Audio;

public class HolokitAudioProcessor : MonoBehaviour
{
    [SerializeField]  AudioSource audioSource;
    public AudioSource AudioSource { get => audioSource; set => audioSource = value; }

    float audioVolume = 0;
    public float AudioVolume { get { return audioVolume; } }

    float audioPitch = 0;
    public float AudioPitch { get { return audioPitch; } }

    

    void Start()
    {
        _samples = new float[QSamples];
        _spectrum = new float[QSamples];
        _fSample = AudioSettings.outputSampleRate;
    }
    
    void Update()
    {
        if (audioSource == null)
            return;

        AnalyzeSound();

        audioVolume = Remap(DbValue, -10, 10, 0, 1, true);
        audioPitch = Remap(PitchValue, 0, 600, 0, 1, true);       
    }

    #region Analysis Theory

    float RmsValue;
    float DbValue;
    float PitchValue;

    private const int QSamples = 1024;
    private float RefValue = 0.1f;
    private float Threshold = 0.02f;

    float[] _samples;
    float[] _spectrum;
    float _fSample;

    /// <summary>
    /// The code is from https://stackoverflow.com/questions/53030560/read-microphone-decibels-and-pitch-frequency
    /// </summary>
    void AnalyzeSound()
    {
        audioSource.GetOutputData(_samples, 0); // fill array with samples
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
        audioSource.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
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


    float Remap(float v, float src_min, float src_max, float dst_min, float dst_max, bool need_clamp = false)
    {
        if (need_clamp)
            v = Mathf.Clamp(v, src_min, src_max);

        if (src_min == src_max)
            v = 0;
        else
            v = (v - src_min) / (src_max - src_min);

        return v * (dst_max - dst_min) + dst_min;
    }
}