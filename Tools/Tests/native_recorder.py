#!/usr/bin/env python3
"""Exercise the actual AVAssetWriter plugin; inspect every produced movie with ffprobe."""
import array, ctypes as c, json, math, pathlib, subprocess, sys, time
root = pathlib.Path(__file__).resolve().parents[2]
out = pathlib.Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
lib = c.CDLL(str(root/'Packages/io.holokit.unity-sdk/Runtime/macOS/UnityHoloKit.bundle/Contents/MacOS/UnityHoloKit'))
start = lib.HoloKitVideoRecorder_StartRecording
start.argtypes = [c.c_char_p,c.c_int,c.c_int,c.c_float,c.c_int]
video = lib.HoloKitVideoRecorder_AppendVideoFrame; video.argtypes = [c.c_void_p,c.c_int,c.c_double]
audio = lib.HoloKitVideoRecorder_AppendAudioFrame; audio.argtypes = video.argtypes
stop = lib.HoloKitVideoRecorder_EndRecording
finishing = lib.HoloKitVideoRecorder_IsFinishing
# Failure must not poison the next start.
assert start(b'/missing-directory/invalid.mp4',320,240,48000,2) != 0
results = []
for index,(width,height,channels,rate) in enumerate([(240,320,2,48000),(320,240,2,48000),(240,320,1,48000),(320,240,1,48000),(240,320,2,24000),(320,240,2,24000)]):
    path = out/f'native-{index}.mp4'
    assert start(str(path).encode(),width,height,rate,channels) == 0
    assert start(str(out/'double-start.mp4').encode(),width,height,rate,channels) != 0
    pixels = (c.c_ubyte*(width*height*4))(*([255,40,10,255]*(width*height)))
    began = time.monotonic()
    for frame in range(45):
        pcm = array.array('f', (0.2*math.sin(2*math.pi*300*(frame*(rate//30)+i//channels)/rate) for i in range((rate//30)*channels)))
        samples = (c.c_float*len(pcm)).from_buffer(pcm)
        assert video(pixels,len(pixels),frame/30) == 0
        assert audio(samples,len(pcm)*4,frame/30) == 0
        time.sleep(max(0,began+(frame+1)/30-time.monotonic()))
    stop(); stop()
    deadline=time.monotonic()+15
    while finishing() and time.monotonic()<deadline: time.sleep(.05)
    assert not finishing(), 'finalization timeout'
    probe = json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-show_streams','-show_format','-of','json',str(path)]))
    v=next(s for s in probe['streams'] if s['codec_type']=='video')
    a=next(s for s in probe['streams'] if s['codec_type']=='audio')
    assert (v['width'],v['height'])==(width,height)
    assert int(v['nb_read_frames'])==45
    assert a['channels']==channels
    assert int(a['sample_rate'])==rate
    decoded = array.array('f')
    decoded.frombytes(subprocess.check_output(['ffmpeg','-v','error','-i',str(path),'-f','f32le','-acodec','pcm_f32le','-']))
    rms = math.sqrt(sum(x*x for x in decoded)/len(decoded))
    assert .08 < rms < .22, f'unexpected audio RMS: {rms}'
    rgb = subprocess.check_output(['ffmpeg','-v','error','-i',str(path),'-frames:v','1','-vf','scale=1:1','-f','rawvideo','-pix_fmt','rgb24','-'])
    assert rgb[0]>220 and rgb[1]<75 and rgb[2]<45, f'colour swapped or corrupted: {rgb}'
    assert abs(float(v['duration'])-float(a['duration']))<.2
    results.append({'path':str(path),'frames':45,'dimensions':[width,height],'channels':channels,'sample_rate':rate,'audio_rms':rms,'rgb':list(rgb),'video_duration':v['duration'],'audio_duration':a['duration']})
(out/'results.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
