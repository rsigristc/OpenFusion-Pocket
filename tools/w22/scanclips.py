import sys,glob,os,math
sys.path.insert(0,'.'); sys.path.insert(0,os.path.expanduser('~/mnt/OpenFusionLauncher/OpenFusionAndroid/tools'))
from sf import SF
f=sys.argv[1]; pat=sys.argv[2]
b=open(f,'rb').read(); s=SF(b)
class N:
    def __init__(s,d): s.__dict__.update(d)
def unpack_ints(v):
    d=v['m_Data']; bs=v['m_BitSize']; n=v['m_NumItems']
    out=[];bit=0
    for _ in range(n):
        x=0
        for k in range(bs):
            x|=((d[bit>>3]>>(bit&7))&1)<<k; bit+=1
        out.append(x)
    return out
for o in s.objs:
    if o['cid']!=74: continue
    t=s.types.get(o['tid']) or s.types.get(o['cid']); v=s.read(t,[o['off']])
    for c in v['m_CompressedRotationCurves']:
        if pat in c['m_Path']:
            dt=unpack_ints(c['m_Times']); tt=0;times=[]
            for x in dt: tt+=x; times.append(tt*0.01)
            print(o['pid'],v['m_Name'],c['m_Path'],'times',times,'pre/post',c['m_PreInfinity'],c['m_PostInfinity'],'vals',c['m_Values']['m_NumItems'],'slopes',c['m_Slopes']['m_NumItems'],c['m_Slopes']['m_BitSize'],'dtbits',c['m_Times']['m_BitSize'], 'dt',dt)
