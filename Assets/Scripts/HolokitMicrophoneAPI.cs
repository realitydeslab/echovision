#if UNITY_IOS
using HoloKit.iOS;
#endif
using UnityEngine;
using UnityEngine.Audio;

public class HolokitMicrophoneAPI : MonoBehaviour
{
#if UNITY_IOS
    [Header("Holokit Recorder")]
    [SerializeField] HoloKitVideoRecorder videoRecorder;
    [SerializeField] UnityEngine.UI.Text recordingText;
#endif

    [Header("Auido Source")]
    [SerializeField] AudioSource audiosourceForRecording;       // is for audio recorder
    [SerializeField] AudioSource audioSourceForAnalysis;        // is for extracting audio spectrum
    public AudioSource MicrophoneAudioSource { get => audioSourceForAnalysis; }

    [Header("Auido Mixer")]
    [SerializeField] AudioMixerGroup audioGroupMaster;          // sound output pipeline for recording. set volume to 0 
    [SerializeField] AudioMixerGroup audioGroupForAnalysis;     // sound output pipeline for analysis. set volume to -80 so that user won't hear echo their sound being analyzing

    AudioClip microphoneAudioClip;

    bool verbose = false;
    public bool Verbose { get => verbose; set => verbose = value; }


    #region Microphone Related Functions
    void StartMicrophone()
    {
        // Pick default device
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError($"[{ this.GetType().Name}] No microphone device found!");
            return;
        }

        if (verbose)
        {
            for (int i = 0; i < Microphone.devices.Length; i++)
            {
                Debug.Log($"[{ this.GetType().Name}] Mic{i}: {Microphone.devices[i]}");
            }
        }


        // Start microphone
        microphoneAudioClip = Microphone.Start(null, true, 1, 44100);
        if (microphoneAudioClip == null)
        {
            Debug.LogError($"[{ this.GetType().Name}] Failed to start microphone");
            return;
        }

        // Send clip to audio source
        audioSourceForAnalysis.clip = microphoneAudioClip;
        audioSourceForAnalysis.loop = true;
        audioSourceForAnalysis.volume = 1;

        audiosourceForRecording.clip = microphoneAudioClip;
        audiosourceForRecording.loop = true;
        audiosourceForRecording.volume = 0.5f; // Can not be set to zero cause that will cut off the data stream


        audioSourceForAnalysis.outputAudioMixerGroup = audioGroupForAnalysis;
        audiosourceForRecording.outputAudioMixerGroup = audioGroupMaster;

        // loop to clear buffer
        while (!(Microphone.GetPosition(null) > 0))
        {
        }

        audioSourceForAnalysis.Play();

        if (verbose)
            Debug.Log($"[{ this.GetType().Name}] Microphone Started.");
    }

    void StopMicrophone()
    {
        Microphone.End(null);

        if (verbose)
            Debug.Log($"[{ this.GetType().Name}] Microphone Stopped.");
    }

    void OnEnable()
    {
        if (verbose)
            Debug.Log($"[{ this.GetType().Name}] OnEnable : StartMicrophone");
        StartMicrophone();

    }

    void OnDisable()
    {
        if (verbose)
            Debug.Log($"[{ this.GetType().Name}] OnDisable : StopMicrophone");
        StopMicrophone();
    }

    void OnDestroy()
    {
        if (verbose)
            Debug.Log($"[{ this.GetType().Name}] OnDestroy : StopMicrophone");

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
            if (verbose)
                Debug.Log($"[{ this.GetType().Name}] OnFocus : StartMicrophone");
            StartMicrophone();
        }

        else
        {
            if (verbose)
                Debug.Log($"[{ this.GetType().Name}] OnLoseFocus : StopMicrophone");
            StopMicrophone();
        }
    }
    #endregion

#if UNITY_IOS
    #region HolokitRecorder Related Functions / Temporal
    public void ToggleRecording()
    {
        if (videoRecorder.IsRecording == false)
        {
            // Set video recorder to use audiosource created here
            videoRecorder._microphoneAudioSource = audiosourceForRecording;
        }
        videoRecorder.ToggleRecording();
        recordingText.text = videoRecorder.IsRecording ? "Stop Recording" : "Start Recording";
    }

    public void StartRecording()
    {
        if (videoRecorder.IsRecording == false)
        {
            ToggleRecording();
        }
    }

    public void StopRecording()
    {
        if (videoRecorder.IsRecording == true)
        {
            ToggleRecording();
        }
    }
    #endregion
#endif
}
