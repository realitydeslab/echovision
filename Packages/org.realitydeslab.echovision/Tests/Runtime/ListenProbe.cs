#if UNITY_IOS && DEVELOPMENT_BUILD && ECHOVISION_DEVICETEST
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// Modes "listen" and "listen-mics": no walker. The screen tells the person what to do while the
// analysis input is logged ten times a second, every emitted wave is logged and each phase is
// marked, so Tests/Tools~/run_listen.py can compare the levels of each phase with the wave trigger.
// "listen": quiet, talk, talk loudly, clap, tap the phone. "listen-mics": talk into each edge of
// the phone at 5 cm (which built-in microphone is in use, and does voice reach it), then music.
// "listen-gravity": poses for PowerSaver's upside-down sleep (Stereo worn / flipped, portrait
// upright / flipped, flat), logging screen-space gravity, the upside-down verdict and sleep.
public class ListenProbe : MonoBehaviour
{
    static readonly (string name, string prompt, float seconds)[] Sounds =
    {
        ("quiet", "QUIET", 8), ("talk", "TALK NORMALLY", 12), ("loud", "TALK LOUDLY", 8),
        ("clap", "CLAP", 8), ("tap", "TAP THE PHONE", 8), ("quiet-end", "QUIET", 6),
    };

    static readonly (string name, string prompt, float seconds)[] Microphones =
    {
        ("quiet", "QUIET", 6), ("bottom", "TALK INTO THE\nBOTTOM EDGE", 8), ("top", "TALK INTO THE\nTOP EDGE", 8),
        ("back", "TALK INTO THE\nBACK (CAMERAS)", 8), ("music", "PLAY MUSIC", 12), ("quiet-end", "QUIET", 6),
    };

    static readonly (string name, string prompt, float seconds)[] Poses =
    {
        ("stereo-worn", "STEREO:\nHOLD AS WORN", 8), ("stereo-flipped", "TURN IT\nUPSIDE DOWN", 9),
        ("stereo-back", "TURN IT BACK", 6), ("portrait-upright", "MONO:\nHOLD UPRIGHT", 7),
        ("portrait-flipped", "TURN IT\nUPSIDE DOWN", 9), ("portrait-back", "TURN IT BACK", 6),
        ("flat", "LAY IT FLAT,\nSCREEN UP", 8),
    };

    public string Mode = "listen";
    PowerSaver powerSaver;
    float gravityLogTime;

    AudioProcessor audioProcessor;
    SoundWaveEmitter emitter;
    FieldInfo waveIndex;
    int lastWaveIndex;
    Text prompt;

    IEnumerator Start()
    {
        audioProcessor = FindFirstObjectByType<AudioProcessor>();
        emitter = FindFirstObjectByType<SoundWaveEmitter>();
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(AudioProcessor).GetField("_logAnalysis", Private).SetValue(audioProcessor, true);
        waveIndex = typeof(SoundWaveEmitter).GetField("nextEmitIndex", Private);
        lastWaveIndex = (int)waveIndex.GetValue(emitter);
        prompt = CreatePrompt();
        Debug.Log("[LISTEN] READY");
        powerSaver = FindFirstObjectByType<PowerSaver>();
        var phases = Mode == "listen-mics" ? Microphones : Mode == "listen-gravity" ? Poses : Sounds;
        foreach (var phase in phases)
        {
            if (Mode == "listen-gravity" && FindFirstObjectByType<HoloKit.HoloKitCameraManager>() is { } manager)
                manager.ScreenRenderMode = phase.name.StartsWith("stereo") ? HoloKit.ScreenRenderMode.Stereo : HoloKit.ScreenRenderMode.Mono;
            Debug.Log($"[LISTEN] PHASE {phase.name}");
            for (float left = phase.seconds; left > 0; left -= Time.deltaTime)
            {
                prompt.text = $"{phase.prompt}\n{Mathf.CeilToInt(left)}";
                yield return null;
            }
        }
        prompt.text = "DONE";
        Debug.Log("[LISTEN] DONE");
    }

    void Update()
    {
        if (Mode == "listen-gravity" && powerSaver != null && Time.unscaledTime >= gravityLogTime)
        {
            gravityLogTime = Time.unscaledTime + .25f;
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var gravity = ((System.Func<Vector3>)typeof(PowerSaver).GetField("readScreenGravity", Private).GetValue(powerSaver))();
            bool upsideDown = (bool)typeof(PowerSaver).GetMethod("IsUpsideDown", Private).Invoke(powerSaver, new object[] { gravity });
            bool asleep = (bool)typeof(PowerSaver).GetField("sleepMode", Private).GetValue(powerSaver);
            Debug.Log($"[LISTEN] GRAVITY x={gravity.x:F2} y={gravity.y:F2} z={gravity.z:F2} upsideDown={upsideDown} asleep={asleep} screen={Screen.orientation} device={Input.deviceOrientation}");
        }
        if (waveIndex == null) return;
        int current = (int)waveIndex.GetValue(emitter);
        if (current == lastWaveIndex) return;
        lastWaveIndex = current;
        Debug.Log($"[LISTEN] EMIT volume={audioProcessor.AudioVolume:F3}");
    }

    static Text CreatePrompt()
    {
        var canvas = new GameObject("Listen prompt").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var text = new GameObject("Prompt").AddComponent<Text>();
        text.rectTransform.SetParent(canvas.transform, false);
        text.rectTransform.anchorMin = new Vector2(0, .55f);
        text.rectTransform.anchorMax = new Vector2(1, .85f);
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 90;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.yellow;
        text.raycastTarget = false;
        return text;
    }
}
#endif
