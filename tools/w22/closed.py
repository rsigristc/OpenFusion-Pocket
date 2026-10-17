import glob,os,sys,json,collections,math
sys.path.insert(0,'.')
exec(open('q.py').read().split("f=sys.argv[1]")[0])
from sf import SF
root=os.path.expanduser('~/mnt/OpenFusionLauncher/ffcache/')
idx={}
for p in glob.glob(root+'*/*/CustomAssetBundle-*'): idx[os.path.basename(p).lower()]=p
d=json.load(open('maudit2.json'))
uniq=set()
for r in d:
    for c in r['clips']:
        if 'pid' in c: uniq.add((c['bundle'],c['pid']))
by=collections.defaultdict(list)
for b,p in uniq: by[b].append(p)
st=collections.Counter()
for b,pids in by.items():
    s=SF(open(idx[b],'rb').read()); obj={o['pid']:o for o in s.objs}
    for p in pids:
        o=obj[p];t=s.types.get(o['tid']) or s.types.get(o['cid']);c=s.read(t,[o['off']])
        for cc in c['m_CompressedRotationCurves']:
            n=cc['m_Values']['m_NumItems']
            if n<3: continue
            q=quats(cc['m_Values']); dt=abs(sum(a*b for a,b in zip(q[0],q[-1])))
            moving=max(abs(abs(sum(a*b for a,b in zip(q[0],qq)))-1) for qq in q)>0.01
            if not moving: continue
            st[(cc['m_PostInfinity'],'closed' if dt>0.995 else 'open')]+=1
        for cc in c['m_FloatCurves']:
            ks=cc['curve']['m_Curve']
            if len(ks)<3: continue
            v=[k['value'] for k in ks]
            if max(v)-min(v)<1e-3: continue
            st[('f',cc['curve']['m_PostInfinity'],'closed' if abs(v[0]-v[-1])<0.02*(max(v)-min(v)+1e-9) else 'open')]+=1
print(st)
