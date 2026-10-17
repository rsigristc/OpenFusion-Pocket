import sys,os,math,json
sys.path.insert(0,'.')
from sf import SF
exec(open('scanclips.py').read().split("for o in s.objs")[0].split("f=sys.argv")[0])
def bits(d,n,st):
    x=0;b=0
    while b<n:
        x|=(d[st[0]]>>st[1])<<b
        num=min(n-b,8-st[1]); st[1]+=num; b+=num
        if st[1]==8: st[0]+=1; st[1]=0
    return x&((1<<n)-1)
def quats(v):
    d=v['m_Data'];st=[0,0];out=[]
    for _ in range(v['m_NumItems']):
        fl=bits(d,3,st);q=[0.0]*4;s=0
        for j in range(4):
            if (fl&3)!=j:
                bs=9 if ((fl&3)+1)%4==j else 10
                x=bits(d,bs,st);q[j]=x/(0.5*((1<<bs)-1))-1;s+=q[j]**2
        l=fl&3;q[l]=math.sqrt(max(0,1-s))
        if fl&4:q[l]=-q[l]
        out.append(q)
    return out
def unpack_ints(v):
    d=v['m_Data']; bs=v['m_BitSize']; n=v['m_NumItems']; st=[0,0]
    return [bits(d,bs,st) for _ in range(n)]
def floats(v):
    d=v['m_Data'];st=[0,0];n=v['m_NumItems'];bs=v['m_BitSize'];rng=v.get('m_Range') or 0;start=v.get('m_Start') or 0
    if not bs: return [start]*n
    sc=1/rng if rng else 0;mx=(1<<bs)-1
    return [bits(d,bs,st)/(sc*mx)+start if sc else start for _ in range(n)]
f=sys.argv[1];pid=int(sys.argv[2])
b=open(f,'rb').read();s=SF(b)
for o in s.objs:
    if o['pid']==pid:
        t=s.types.get(o['tid']) or s.types.get(o['cid']);v=s.read(t,[o['off']])
        for c in v['m_CompressedRotationCurves']:
            dt=unpack_ints(c['m_Times']);tt=0;ts=[]
            for x in dt: tt+=x;ts.append(tt*.01)
            q=quats(c['m_Values']);sl=floats(c['m_Slopes'])
            print(c['m_Path'],ts)
            for i,k in enumerate(q):
                e=math.degrees(2*math.atan2(k[1],k[3])) 
                print(' ',ts[i],[round(x,4) for x in k],'yaw~',round(e,1))
            print(' slopes',[round(x,3) for x in sl])
        print('scale',len(v['m_ScaleCurves']),'pos',len(v['m_PositionCurves']),'float',[ (fc['path'],fc['attribute']) for fc in v['m_FloatCurves']][:20])
