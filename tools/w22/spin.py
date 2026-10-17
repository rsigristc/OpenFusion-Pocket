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
st=collections.Counter(); ex=collections.defaultdict(list)
def dot(a,b): return sum(x*y for x,y in zip(a,b))
for b,pids in by.items():
    s=SF(open(idx[b],'rb').read()); obj={o['pid']:o for o in s.objs}
    for p in pids:
        o=obj[p];t=s.types.get(o['tid']) or s.types.get(o['cid']);c=s.read(t,[o['off']])
        for cc in c['m_CompressedRotationCurves']:
            n=cc['m_Values']['m_NumItems']
            if n<3: continue
            q=quats(cc['m_Values'])
            # continuity
            for i in range(1,n):
                if dot(q[i-1],q[i])<0: q[i]=[-x for x in q[i]]
            moving=max(1-abs(dot(q[0],qq)) for qq in q)>0.01
            if not moving: continue
            kind='spin360' if dot(q[0],q[-1])<-0.99 else ('closed' if dot(q[0],q[-1])>0.99 else 'open')
            st[(cc['m_PostInfinity'],kind)]+=1
            if len(ex[(cc['m_PostInfinity'],kind)])<3: ex[(cc['m_PostInfinity'],kind)].append((b[-6:],p,cc['m_Path']))
print(st); print(dict(ex))
