"""Export Unity 2.5 Mesh objects (as parsed by UnityPy) to Wavefront OBJ."""
import struct

def mesh_to_obj(m):
    V=[(v.x,v.y,v.z) for v in m.m_Vertices]
    N=[(v.x,v.y,v.z) for v in m.m_Normals]
    UV=[(u.x,u.y) for u in m.m_UV]
    raw=bytes(m.m_IndexBuffer)
    idx=struct.unpack('<%dH'%(len(raw)//2),raw) if m.m_Use16BitIndices else struct.unpack('<%dI'%(len(raw)//4),raw)
    if not len(m.m_Vertices):
        return None, {'verts':0}, []   # empty placeholder mesh
    out=['o %s'%(m.m_Name or 'mesh')]
    out+=['v %.6f %.6f %.6f'%v for v in V]
    out+=['vt %.6f %.6f'%u for u in UV]
    out+=['vn %.6f %.6f %.6f'%n for n in N]
    stats={'verts':len(V),'indices':len(idx),'max_index':max(idx) if idx else -1,'submeshes':len(m.m_SubMeshes)}
    for sm in m.m_SubMeshes:
        first=sm.firstByte//2
        ii=idx[first:first+sm.indexCount]
        tris=[]
        if sm.isTriStrip:
            for i in range(len(ii)-2):
                a,b,c=ii[i],ii[i+1],ii[i+2]
                if a!=b and b!=c and a!=c: tris.append((a,b,c) if i%2==0 else (a,c,b))
        else:
            tris=[tuple(ii[i:i+3]) for i in range(0,len(ii)-2,3)]
        for a,b,c in tris:
            f=[]
            for k in (a,b,c):
                f.append('%d/%d/%d'%(k+1,k+1,k+1) if UV and N else str(k+1))
            out.append('f '+' '.join(f))
        stats['tris']=stats.get('tris',0)+len(tris)
    return '\n'.join(out), stats, V
