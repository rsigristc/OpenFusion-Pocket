"""Unity 2.5 animation helpers: packed-vector decoding and skeleton/curve validation (works on UnityPy parsed objects)."""
import math

def _bits(data, nbits, st):
    """read nbits from byte array, st=[idx,bitpos]"""
    x=0; bits=0
    while bits<nbits:
        x |= (data[st[0]]>>st[1])<<bits
        num=min(nbits-bits, 8-st[1]); st[1]+=num; bits+=num
        if st[1]==8: st[0]+=1; st[1]=0
    return x & ((1<<nbits)-1)

def unpack_ints(v):
    d=bytes(v.m_Data); st=[0,0]; n=int(v.m_NumItems); bs=int(v.m_BitSize)
    return [_bits(d,bs,st) for _ in range(n)] if bs else [0]*n

def unpack_floats(v):
    d=bytes(v.m_Data); st=[0,0]; n=int(v.m_NumItems); bs=int(v.m_BitSize); rng=float(v.m_Range or 0.0); start=float(v.m_Start or 0.0)
    if not bs: return [start]*n
    scale=(1.0/rng) if rng else 0.0
    mx=(1<<bs)-1; out=[]
    for _ in range(n):
        x=_bits(d,bs,st)
        out.append(x/(scale*mx)+start if scale else start)
    return out

def unpack_quats(v):
    d=bytes(v.m_Data); st=[0,0]; out=[]
    for _ in range(int(v.m_NumItems)):
        flags=_bits(d,3,st); q=[0.0]*4; s=0.0
        for j in range(4):
            if (flags&3)!=j:
                bs=9 if ((flags&3)+1)%4==j else 10
                x=_bits(d,bs,st); q[j]=x/(0.5*((1<<bs)-1))-1.0; s+=q[j]*q[j]
        last=flags&3; q[last]=math.sqrt(max(0.0,1.0-s))
        if flags&4: q[last]=-q[last]
        out.append(tuple(q))
    return out

def decode_compressed_curve(c):
    """-> (times_seconds, quats, slopes). times are cumulative deltas in 1/100 s."""
    dt=unpack_ints(c.m_Times); t=0; times=[]
    for x in dt: t+=x; times.append(t*0.01)
    return times, unpack_quats(c.m_Values), unpack_floats(c.m_Slopes)


def decompress_mesh(m):
    """Decode m_CompressedMesh (MeshCompression != 0) -> dict(positions, normals, uvs, indices, skin[(idx4, w4)])."""
    cm=m.m_CompressedMesh
    out={}
    vals=unpack_floats(cm.m_Vertices); nv=len(vals)//3
    out['positions']=[tuple(vals[3*i:3*i+3]) for i in range(nv)]
    if cm.m_Normals is not None and int(cm.m_Normals.m_NumItems):
        nd=unpack_floats(cm.m_Normals); sg=unpack_ints(cm.m_NormalSigns); ns=[]
        for i in range(len(sg)):
            x,y=nd[2*i],nd[2*i+1]; zs=1-x*x-y*y; z=math.sqrt(zs) if zs>=0 else 0.0
            if sg[i]==0: z=-z
            ns.append((x,y,z))
        out['normals']=ns
    if cm.m_UV is not None and int(cm.m_UV.m_NumItems):
        uv=unpack_floats(cm.m_UV); out['uvs']=[(uv[2*i],uv[2*i+1]) for i in range(min(nv,len(uv)//2))]
    out['indices']=unpack_ints(cm.m_Triangles)
    if cm.m_Weights is not None and int(cm.m_Weights.m_NumItems):
        wt=unpack_ints(cm.m_Weights); bi=unpack_ints(cm.m_BoneIndices)
        skin=[]; cur_i=[0]*4; cur_w=[0.0]*4; j=0; s=0; bp=0
        for w in wt:
            cur_w[j]=w/31.0; cur_i[j]=bi[bp]; bp+=1; j+=1; s+=w
            if s>=31:
                for k in range(j,4): cur_w[k]=0.0; cur_i[k]=0
                skin.append((tuple(cur_i),tuple(cur_w))); cur_i=[0]*4; cur_w=[0.0]*4; j=0; s=0
            elif j==3:
                cur_w[3]=(31-s)/31.0; cur_i[3]=bi[bp]; bp+=1
                skin.append((tuple(cur_i),tuple(cur_w))); cur_i=[0]*4; cur_w=[0.0]*4; j=0; s=0
        out['skin']=skin; out['_bone_indices_consumed']=bp; out['_bone_indices_total']=len(bi)
    return out
