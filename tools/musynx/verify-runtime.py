"""Verify the original game's observed notes against the authoring fixture."""
import json,re,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
output=root/'artifacts/musynx-test'
log=(output/'runtime-final.log').read_text(encoding='utf8',errors='replace')
chart=json.loads((output/'test-chart.json').read_text(encoding='utf8'))
observed=[]
for kind,lane,head,tail in re.findall(r'\[CHART-IMPORT-TEST\] (Note|LongNote) track=(\d+) ticks=(\d+)(?: end=(\d+))?',log):
    observed.append((int(lane),int(head),int(tail) if tail else None))
# Original UIB applies -1 ms display adjustment, and compresses 6/7 to 5/6 in 4K.
expected=[([3,4,5,6][n['lane']],round((n['time']+2000)*10000)-10000,
           round((n['endTime']+2000)*10000)-10000 if 'endTime' in n else None)
          for n in chart['notes']]
assert sorted(observed)==sorted(expected),'Loaded note positions differ from exported fixture'
assert 'PASS chart and CRI audio loaded' in log
assert 'PASS native CRI audio playing and synchronized' in log
assert 'AUDIO end status=PlayEnd' in log
assert 'PASS playback reached native results scene' in log
assert '[CHART-IMPORT-TEST] FAIL' not in log
audio=re.search(r'AUDIO status=Playing timeMs=(\d+) expectedMs=(\d+)',log)
result={'gameBuildId':'25570400','slot':'lazhuren','mode':'4K easy',
        'notesVerified':len(expected),'holdsVerified':sum('endTime' in n for n in chart['notes']),
        'audioDurationMs':24000,'nativeAudioClockDifferenceMs':abs(int(audio[1])-int(audio[2])),
        'nativeResultsSceneReached':True,'manualKeyboardPlayTested':False}
(output/'verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(result,ensure_ascii=False))
