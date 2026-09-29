"""Read actual Unity SurfaceView present timestamps, not Android UI gfxinfo."""
import argparse, json, os, re, shutil, statistics, subprocess, time
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--seconds',type=float,default=8)
p.add_argument('--output',required=True)
p.add_argument('--minimum-fps',type=float,default=90)
p.add_argument('--adb',default=os.environ.get('ADB') or shutil.which('adb') or r'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe')
a=p.parse_args()
adb=a.adb
def shell(command):
    return subprocess.check_output([adb,'shell',command],text=True,encoding='utf-8',errors='replace')
layers=shell('dumpsys SurfaceFlinger --list')
matches=re.findall(r'(?:RequestedLayerState\{)?([^\n{}]*SurfaceView\[com\.geometryrhythm\.demo/[^\n]*\]\(BLAST\)#\d+)',layers)
if not matches: raise SystemExit('No active game SurfaceView')
layer=matches[-1]
frames=set()
cutoff=None
end=time.monotonic()+a.seconds
while time.monotonic()<end:
    data=shell('dumpsys SurfaceFlinger --latency "'+layer+'"').splitlines()
    if len(data)<2:
        raise SystemExit('Game SurfaceView disappeared during capture; discard this sample')
    for line in data[1:]:
        fields=line.split()
        if len(fields)==3:
            present=int(fields[1])
            if 0<present<9223372036854775807 and (cutoff is None or present>cutoff): frames.add(present)
    # The first snapshot is the compositor's recent history, not this measurement.
    if cutoff is None and frames:
        cutoff=max(frames)
        frames.clear()
    time.sleep(.4)
frames=sorted(frames)
intervals=[(b-a)/1e6 for a,b in zip(frames,frames[1:])]
if not intervals: raise SystemExit('No presented frames')
if (frames[-1]-frames[0])/1e9 < a.seconds*.7:
    raise SystemExit('Insufficient fresh frames; application may have stopped or been replaced')
ordered=sorted(intervals)
result={'layer':layer,'frames':len(frames),'fps':1000/statistics.mean(intervals),'median_ms':statistics.median(intervals),'p95_ms':ordered[int((len(ordered)-1)*.95)],'max_ms':max(intervals),'over_25ms':sum(x>25 for x in intervals)}
result['pass']=result['fps']>=a.minimum_fps
output=Path(a.output)
output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
raise SystemExit(0 if result['pass'] else 1)
