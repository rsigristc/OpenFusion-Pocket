import glob,os,sys,json,collections
sys.path.insert(0,'.')
from sf import SF
root=os.path.expanduser('~/mnt/OpenFusionLauncher/ffcache/')
idx={}
for p in glob.glob(root+'*/*/CustomAssetBundle-*'): idx[os.path.basename(p).lower()]=p
d=json.load(open('maudit2.json'))
uniq=set()
for r in d:
    for c in r['clips']:
        if 'pid' in c: uniq.add((c['bundle'],c['pid']))
print(len(uniq),'clips unicos')
by=collections.defaultdict(list)
for b,p in uniq: by[b].append(p)
stat=collections.Counter(); ex=collections.defaultdict(list)
for b,pids in by.items():
    if b not in idx: continue
    s=SF(open(idx[b],'rb').read()); obj={o['pid']:o for o in s.objs}
    for p in pids:
        o=obj[p];t=s.types.get(o['tid']) or s.types.get(o['cid']);c=s.read(t,[o['off']])
        loops=[];  # (kind,lastTime,firstTime)
        import math
        # crot times
        for cc in c['m_CompressedRotationCurves']:
            dt=[];bs=cc['m_Times']['m_BitSize'];n=cc['m_Times']['m_NumItems'];dd=cc['m_Times']['m_Data']
            bit=0;tt=0;ts=[]
            for _ in range(n):
                x=0
                for k in range(bs): x|=((dd[bit>>3]>>(bit&7))&1)<<k; bit+=1
                tt+=x; ts.append(tt*.01)
            if cc['m_PostInfinity']==1: loops.append(('rot',ts[-1]))
            elif len(ts)>2 and ts[-1]>0.1: stat['rot-moving-clamp']+=1; ex['rot-moving-clamp'].append((b[-6:],p,cc['m_Path'],ts[-1],cc['m_PreInfinity'],cc['m_PostInfinity']))
        for k,nm in (('m_PositionCurves','pos'),('m_ScaleCurves','scale'),('m_RotationCurves','rotu')):
            for cc in c[k]:
                ks=cc['curve']['m_Curve']
                if cc['curve']['m_PostInfinity']==1: loops.append((nm,ks[-1]['time'] if ks else 0))
        for cc in c['m_FloatCurves']:
            ks=cc['curve']['m_Curve']
            if cc['curve']['m_PostInfinity']==1: loops.append(('float',ks[-1]['time']))
            elif len(ks)>2: stat['float-moving-clamp']+=1; ex['float-moving-clamp'].append((b[-6:],p,cc['attribute'],ks[-1]['time'],cc['curve']['m_PreInfinity'],cc['curve']['m_PostInfinity']))
        ts=sorted(set(round(x[1],2) for x in loops))
        stat['clips']+=1
        if len(ts)>1:
            stat['mixed-loop-durations']+=1
            if len(ex['mixed'])<8: ex['mixed'].append((b[-6:],p,collections.Counter((k,round(t,2)) for k,t in loops).most_common(6)))
        # pingpong/other infinity
        for cc in c['m_FloatCurves']:
            if cc['curve']['m_PostInfinity'] not in (1,2): stat['float-post-'+str(cc['curve']['m_PostInfinity'])]+=1
print(stat)
for k,v in ex.items(): print(k,len(v),v[:6])
