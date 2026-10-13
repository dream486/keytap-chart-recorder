import sys,json,hashlib
from pathlib import Path
work=Path(__file__).resolve().parents[2]/'.tmp/musynx-research'
sys.path.insert(0,str(work/'python'))
import UnityPy
source,target=map(Path,sys.argv[1:3])
env=UnityPy.load(str(source))
new_chart=(work/'new-chart.txt').read_text(encoding='utf8')
changed=[]
original={o.path_id:hashlib.sha256(o.get_raw_data()).hexdigest() for o in env.objects}
for o in env.objects:
    if o.type.name!='TextAsset': continue
    d=o.read()
    if d.m_Name in ['lazhuren4T_easy','lazhuren4T_hard','lazhuren6T_easy','lazhuren6T_hard']:
        d.m_Script=new_chart; d.save(); changed.append(o.path_id)
    elif d.m_Name=='songList_org':
        text=d.m_Script
        if isinstance(text,bytes):text=text.decode('utf8')
        entries=text.replace('\r','').split('--++--')
        count=0
        for i,entry in enumerate(entries):
            if '\nlazhuren_easy\n' in entry or '\nlazhuren_hard\n' in entry:
                lines=entry.strip().split('\n');lines[1]='蜡烛人 [TEST]';lines[4]='27'
                entries[i]='\n'+'\n'.join(lines)+'\n';count+=1
        assert count==2,count
        d.m_Script='--++--'.join(entries);d.save();changed.append(o.path_id)
assert len(changed)==5,changed
target.write_bytes(env.file.save())
check=UnityPy.load(str(target))
for o in check.objects:
    if o.path_id not in changed:
        assert hashlib.sha256(o.get_raw_data()).hexdigest()==original[o.path_id],o.path_id
    elif o.type.name=='TextAsset' and o.read().m_Name.startswith('lazhuren'):
        d=o.read(); s=d.m_Script
        if isinstance(s,bytes):s=s.decode('utf8')
        assert s==new_chart
print(json.dumps({'changed_object_ids':changed,'unchanged_objects_verified':len(original)-len(changed),'output_bytes':target.stat().st_size}))
