#if UNITY_IOS && DEVELOPMENT_BUILD && ECHOVISION_STENCILTEST
using System.Collections;
using System.IO;
using System.Reflection;
using HoloKit;
using HoloKit.iOS;
using System.Runtime.InteropServices;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;
using UnityEngine.XR.ARFoundation;

// Temporary device diagnostics. Copy into Assets only for the probe build.
public class HumanStencilProbe : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() => new GameObject(nameof(HumanStencilProbe)).AddComponent<HumanStencilProbe>();

    DepthImageProcessor depth;
    ARCameraBackground background;
    ARSession session;
    ARMeshManager meshing;
    MeshVFX upload;
    VisualEffect particles;
    PowerSaver powerSaver;
    Material material;
    RawImage overlay;
    AppleNativeProvider native;
    ProfilerRecorder gc;
    int mode = 0, frames;
    float nextLog, lastLog;
    long gcTotal;
    string phase = "stencil", status = "Waiting for AR";
    bool profiling;
    readonly FieldInfo sleep = typeof(PowerSaver).GetField("sleepMode", BindingFlags.Instance | BindingFlags.NonPublic);
    readonly FieldInfo idle = typeof(PowerSaver).GetField("idleTime", BindingFlags.Instance | BindingFlags.NonPublic);

    void Start()
    {
        depth = FindFirstObjectByType<DepthImageProcessor>();
        background = depth.cameraManager.GetComponent<ARCameraBackground>();
        session = FindFirstObjectByType<ARSession>();
        meshing = FindFirstObjectByType<ARMeshManager>();
        upload = FindFirstObjectByType<MeshVFX>();
        particles = FindFirstObjectByType<VisualEffect>();
        powerSaver = FindFirstObjectByType<PowerSaver>();
        native = new AppleNativeProvider();
        gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        material = new Material(Resources.Load<Shader>("HumanStencilDebug"));
        var canvas = new GameObject("Stencil Overlay", typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
        overlay = new GameObject("Mask", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        overlay.transform.SetParent(canvas.transform, false);
        overlay.rectTransform.anchorMin = Vector2.zero; overlay.rectTransform.anchorMax = Vector2.one;
        overlay.rectTransform.offsetMin = overlay.rectTransform.offsetMax = Vector2.zero;
        overlay.material = material; overlay.raycastTarget = false;
        lastLog = Time.unscaledTime;
        Debug.Log("[PROBE] READY: cyan=background mapping; red=current production mapping; UI overlay is screenshot-only, not included in GPU movies");
    }

    void OnEnable() => Application.onBeforeRender += Refresh;
    void OnDisable() => Application.onBeforeRender -= Refresh;
    [BeforeRenderOrder(200)]
    void Refresh()
    {
        if (material == null || !depth.isActiveAndEnabled) return;
        var texture = depth.HumanStencilTexture;
        overlay.enabled = texture != null && !(bool)sleep.GetValue(powerSaver);
        if (texture == null) return;
        overlay.texture = texture;
        material.SetMatrix("_CurrentMatrix", depth.DisplayRotatioMatrix);
        if (background.material != null)
            material.SetMatrix("_CameraMatrix", background.material.GetMatrix("_UnityDisplayTransform"));
        material.SetFloat("_Mode", mode);
    }

    void Update()
    {
        if (native == null) return;
        frames++; gcTotal += gc.LastValue;
        if (Time.unscaledTime < nextLog) return;
        var temperature = native.GetThermalState();
        bool sleeping = (bool)sleep.GetValue(powerSaver);
        if (sleeping) overlay.enabled = false;
        if (temperature >= AppleThermalState.Serious && profiling)
        {
            Debug.LogWarning("[PERF] ABORT thermal state="+temperature);
            Application.Quit(); return;
        }
        status = $"{Screen.orientation} / physical {Input.deviceOrientation}\n{temperature} | {frames / (Time.unscaledTime-lastLog):F0} fps | sleep {sleeping} ({(float)idle.GetValue(powerSaver):F0}s)";
        Debug.Log($"[PERF] phase={phase} {status.Replace('\n',' ')} gcBytesPerFrame={gcTotal/Mathf.Max(1,frames)} session={session.subsystem?.running} mesh={meshing.enabled} upload={upload.enabled} stencil={depth.occlusionManager.currentHumanStencilMode} charging={SystemInfo.batteryStatus}");
        LogFov();
        gcTotal=0; frames=0; lastLog=Time.unscaledTime; nextLog=Time.unscaledTime+1;
        string commandPath=Path.Combine(Application.persistentDataPath,"probe-command.txt");
        if (File.Exists(commandPath))
        {
            string command=File.ReadAllText(commandPath).Trim(); File.Delete(commandPath);
            if (command=="profile" && !profiling) StartCoroutine(Profile());
            else if (command=="sleep" && !profiling) StartCoroutine(SleepTest());
            else if (command=="fov" && !profiling) StartCoroutine(FovTest());
            else if (command=="sleep-now") ForceSleep(true);
            else if (command=="wake-now") ForceSleep(false);
            else if (command=="quick-sleep" && !profiling) StartCoroutine(QuickSleepTest());
            else if (command=="meshcheck") StartCoroutine(CheckGpuMesh());
            else if (command=="record") FindFirstObjectByType<MicrophoneAPI>().ToggleRecording();
            else if (command=="snapshot") StartCoroutine(Snapshot());
            else if (command=="quit") Application.Quit();
            else if (int.TryParse(command,out int selected)) mode=Mathf.Clamp(selected,0,3);
        }
    }

    void OnGUI()
    {
        float scale=Mathf.Min(Screen.width/800f, Screen.height/1000f);
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        GUI.skin.label.fontSize=18; GUI.skin.button.fontSize=18;
        GUI.Box(new Rect(10,55,780,112),"");
        GUI.Label(new Rect(20,58,760,55),status);
        string[] labels={"Current (red)","Camera (cyan)","Compare","Off"};
        for(int i=0;i<4;i++) if(GUI.Button(new Rect(20+i*190,115,185,42),labels[i])) mode=i;
        if(GUI.Button(new Rect(20,175,185,42),"Save screenshot")) StartCoroutine(Snapshot());
        if(GUI.Button(new Rect(210,175,185,42),"Profile 30s") && !profiling) StartCoroutine(Profile());
        if(GUI.Button(new Rect(400,175,185,42),"Close / cool")) Application.Quit();
    }

    IEnumerator Snapshot()
    {
        yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        string path=Path.Combine(Application.persistentDataPath,$"stencil-{Screen.orientation}-{mode}-{System.DateTime.UtcNow:HHmmss}.png");
        File.WriteAllBytes(path,image.EncodeToPNG()); Destroy(image);
        Debug.Log("[PROBE] SCREENSHOT path="+path);
    }

    IEnumerator Profile()
    {
        if(native.GetThermalState()>=AppleThermalState.Serious) { Debug.LogWarning("[PERF] Cool device before profiling"); yield break; }
        profiling=true; mode=3; powerSaver.enabled=false;
        phase="baseline"; yield return new WaitForSecondsRealtime(5);
        phase="no-mesh-upload"; upload.enabled=false; yield return new WaitForSecondsRealtime(5);
        phase="no-particles"; particles.enabled=false; yield return new WaitForSecondsRealtime(5);
        phase="no-meshing"; meshing.enabled=false; yield return new WaitForSecondsRealtime(5);
        phase="no-occlusion"; depth.occlusionManager.enabled=false; yield return new WaitForSecondsRealtime(5);
        phase="no-ar-session"; session.enabled=false; yield return new WaitForSecondsRealtime(5);
        session.enabled=true; depth.occlusionManager.enabled=true; meshing.enabled=true; particles.enabled=true; upload.enabled=true;
        powerSaver.enabled=true; profiling=false; phase="stencil"; mode=1;
        Debug.Log("[PERF] COMPLETE (cumulative ablation; repeat key comparisons before attributing causality)");
    }

    IEnumerator CheckGpuMesh()
    {
        yield return new WaitForEndOfFrame();
        var type=typeof(MeshVFX);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var head=((Component)type.GetField("trackedPoseDriver",flags).GetValue(upload)).transform.position;
        var meshes=meshing.meshes;
        if(meshes.Count==0) { Debug.LogError("[MESH-GPU] FAIL no meshes"); yield break; }
        MeshFilter nearest=null; float distance=float.PositiveInfinity;
        foreach(var filter in meshes)
        {
            float d=(head-filter.transform.TransformPoint(filter.sharedMesh.bounds.center)).sqrMagnitude;
            if(d<distance) { distance=d; nearest=filter; }
        }
        type.GetField("dirty",flags).SetValue(upload,true);
        type.GetMethod("LateUpdate",flags).Invoke(upload,null);
        var buffer=(GraphicsBuffer)type.GetField("vertexBuffer",flags).GetValue(upload);
        int n=Mathf.Min(8,nearest.sharedMesh.vertexCount);
        var actual=new Vector3[n]; buffer.GetData(actual,0,0,n);
        // Test-only CPU reference/readback, never used by the app's mesh packing path.
        var expected=nearest.sharedMesh.vertices;
        var transform=meshes[0].transform.worldToLocalMatrix*nearest.transform.localToWorldMatrix;
        float error=0;
        for(int i=0;i<n;i++) error=Mathf.Max(error,Vector3.Distance(actual[i],transform.MultiplyPoint3x4(expected[i])));
        Debug.Log($"[MESH-GPU] {(error<.0001f ? "PASS" : "FAIL")} points={particles.GetInt("MeshPointCount")} meshes={meshes.Count} sampleCount={n} maxError={error:F7}");
    }

    void ForceSleep(bool sleeping)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        powerSaver.enabled=false;
        typeof(PowerSaver).GetMethod(sleeping ? "EnterSleepMode" : "ExitSleepMode",flags).Invoke(powerSaver,null);
        typeof(PowerSaver).GetField("animationTime",flags).SetValue(powerSaver,sleeping ? 2.01f : -.01f);
        typeof(PowerSaver).GetMethod("UpdateAnimation",flags).Invoke(powerSaver,null);
        if (!sleeping) idle.SetValue(powerSaver,0f);
        powerSaver.enabled=!sleeping;
        Debug.Log($"[QUICK-SLEEP] forced={sleeping} session={session.subsystem.running} mic={Microphone.IsRecording(null)} fps={Application.targetFrameRate}");
    }

    IEnumerator QuickSleepTest()
    {
        profiling=true; mode=0;
        var manager=FindFirstObjectByType<HoloKitCameraManager>();
        var mic=FindFirstObjectByType<MicrophoneAPI>();
        var recorder=FindFirstObjectByType<HoloKitVideoRecorder>();
        int failures=0;
        var orientations=new[] {ScreenOrientation.Portrait,ScreenOrientation.LandscapeLeft,ScreenOrientation.LandscapeRight,ScreenOrientation.LandscapeLeft};
        for(int i=0;i<orientations.Length;i++)
        {
            phase="quick-sleep-"+i;
            manager.ScreenRenderMode=i==3 ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
            Screen.orientation=orientations[i];
            yield return new WaitForSecondsRealtime(1);
            if(i%2==1) mic.StartRecording();
            yield return new WaitForSecondsRealtime(1);
            yield return Snapshot();
            ForceSleep(true);
            yield return new WaitForSecondsRealtime(.5f);
            bool stopped=!session.subsystem.running && !Microphone.IsRecording(null) && !recorder.IsRecording && !recorder.IsFinishing;
            if(!stopped) failures++;
            Debug.Log($"[QUICK-SLEEP] {(stopped ? "PASS" : "FAIL")} stopped cycle={i} recordingWas={i%2==1}");
            ForceSleep(false);
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            var view=new float[16];var projection=new float[16];var display=new float[8];
            bool valid=HoloKit_GetARCameraReference(session.subsystem.nativePtr,(int)Screen.orientation,Screen.width,Screen.height,view,projection,display)!=0;
            var nativeView=new Matrix4x4();for(int j=0;j<16;j++)nativeView[j]=view[j];
            var flip=Matrix4x4.Scale(new Vector3(1,1,-1));
            float angle=Quaternion.Angle((flip*nativeView.inverse*flip).rotation,depth.cameraManager.transform.localRotation);
            var expected=ARKitCameraCompatibility.DisplayMatrix(display);
            var actual=background.material.GetMatrix("_UnityDisplayTransform");float error=0;
            for(int j=0;j<16;j++)error=Mathf.Max(error,Mathf.Abs(expected[j]-actual[j]));
            var stencilMatrix=depth.DisplayRotatioMatrix;
            var particleMatrix=particles.GetMatrix4x4("HumanStencilTextureMatrix");
            float stencilError=0;
            for(int j=0;j<16;j++)
            {
                stencilError=Mathf.Max(stencilError,Mathf.Abs(actual[j]-stencilMatrix[j]));
                stencilError=Mathf.Max(stencilError,Mathf.Abs(actual[j]-particleMatrix[j]));
            }
            bool aligned=valid && angle<2 && error<.02f && stencilError<.0001f && session.subsystem.running && Microphone.IsRecording(null);
            if(!aligned) failures++;
            Debug.Log($"[QUICK-SLEEP] {(aligned ? "PASS" : "FAIL")} resumed cycle={i} poseError={angle:F3} displayMatrixError={error:F5} stencilMatrixError={stencilError:F5} orientation={Screen.orientation} image={display[6]}x{display[7]}");
            yield return Snapshot(); yield return CheckGpuMesh();
        }
        manager.ScreenRenderMode=ScreenRenderMode.Mono;Screen.orientation=ScreenOrientation.AutoRotation;
        profiling=false;phase="stencil";mode=1;
        Debug.Log($"[QUICK-SLEEP] COMPLETE failures={failures}");
    }

    [DllImport("__Internal")]
    static extern int HoloKit_GetARCameraReference(System.IntPtr session,int orientation,int width,int height,
        [Out] float[] view,[Out] float[] projection,[Out] float[] display);

    void LogFov()
    {
        var camera=depth.cameraManager.GetComponent<Camera>();
        var p=camera.projectionMatrix;
        var texture=depth.HumanStencilTexture;
        if(depth.cameraManager.TryGetIntrinsics(out var k))
            Debug.Log($"[FOV] phase={phase} image={k.resolution} focal={k.focalLength} principal={k.principalPoint} projection={p.m00:F5},{p.m11:F5},{p.m02:F5},{p.m12:F5} stencil={texture?.width}x{texture?.height} mode={mode}");
    }

    IEnumerator FovTest()
    {
        profiling=true; mode=3;
        var mic=FindFirstObjectByType<MicrophoneAPI>();
        phase="fov-before"; yield return new WaitForSecondsRealtime(2); yield return Snapshot();
        mic.StartRecording(); phase="fov-4k";
        yield return new WaitForSecondsRealtime(3); yield return Snapshot();
        mic.StopRecording(); phase="fov-restored";
        yield return new WaitForSecondsRealtime(3); yield return Snapshot();
        profiling=false; phase="stencil"; mode=1;
        Debug.Log("[FOV] COMPLETE");
    }

    IEnumerator SleepTest()
    {
        profiling=true; mode=3; phase="sleep-timer";
        var orientation=typeof(PowerSaver).GetField("idleOrientation",BindingFlags.Instance|BindingFlags.NonPublic);
        object original=orientation.GetValue(powerSaver);
        orientation.SetValue(powerSaver,Input.deviceOrientation);
        idle.SetValue(powerSaver,0f);
        var mic=FindFirstObjectByType<MicrophoneAPI>();
        var recorder=FindFirstObjectByType<HoloKitVideoRecorder>();
        // Exercise the real 30-second timer at the current physical orientation.
        // This does not claim that the mounted mask was physically inverted.
        yield return new WaitForSecondsRealtime(28);
        mic.StartRecording();
        yield return new WaitForSecondsRealtime(6);
        bool entered=(bool)sleep.GetValue(powerSaver) && !session.subsystem.running && !Microphone.IsRecording(null)
            && !recorder.IsRecording && !recorder.IsFinishing && Application.targetFrameRate==5;
        Debug.Log($"[SLEEP] {(entered ? "PASS" : "FAIL")} enter: asleep={sleep.GetValue(powerSaver)} session={session.subsystem.running} mic={Microphone.IsRecording(null)} recording={recorder.IsRecording} finishing={recorder.IsFinishing} fps={Application.targetFrameRate}");
        phase="sleep-wake";
        orientation.SetValue(powerSaver,Input.deviceOrientation==DeviceOrientation.Unknown ? DeviceOrientation.FaceUp : DeviceOrientation.Unknown);
        yield return new WaitForSecondsRealtime(6);
        bool woke=!(bool)sleep.GetValue(powerSaver) && session.subsystem.running && Microphone.IsRecording(null);
        Debug.Log($"[SLEEP] {(woke ? "PASS" : "FAIL")} wake: session={session.subsystem.running} mic={Microphone.IsRecording(null)} fps={Application.targetFrameRate}");
        orientation.SetValue(powerSaver,original); profiling=false; phase="stencil"; mode=1;
    }

    void OnDestroy() { gc.Dispose(); native?.Dispose(); if(material!=null) Destroy(material); }
}
#endif
