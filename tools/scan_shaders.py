import struct,sys,os,re,glob,json,collections
def parse(b):
    be=True
    def u32(p): return struct.unpack_from('>I' if be else '<I',b,p)[0]
    ms,fs,ver,do=struct.unpack_from('>IIII',b,0)
    if ver!=6: return None
    p=fs-ms; be=b[p]!=0; p+=1
    def cstr():
        nonlocal p
        e=b.index(0,p); s=b[p:e]; p=e+1; return s
    def node():
        nonlocal p
        stack=[]; 
        def rd():
            nonlocal p
            t=cstr();n=cstr(); p+=20; return t,n,u32(p-0) if False else None
        # iterative read: type,name,size,index,flags,version,meta,childcount
        remaining=[1]
        while remaining:
            if remaining[-1]==0: remaining.pop(); continue
            remaining[-1]-=1
            cstr();cstr(); p+=20; cc=u32(p); p+=4; remaining.append(cc)
    nt=u32(p); p+=4
    for i in range(nt): p+=4; node()
    no=u32(p); p+=4; objs=[]
    for i in range(no):
        pid,off,size,tid=struct.unpack_from('>iIIi' if be else '<iIIi',b,p); p+=16
        cid=struct.unpack_from('>H' if be else '<H',b,p)[0]; p+=4
        objs.append((pid,off+do,size,tid,cid))
    return objs
root=sys.argv[1]; out={}
for f in sorted(glob.glob(root+'/*/*')):
    b=open(f,'rb').read()
    try: objs=parse(b)
    except Exception as e: out[f]={'err':str(e)}; continue
    if objs is None: continue
    cnt=collections.Counter(o[4] for o in objs); sh=[]
    for pid,off,size,tid,cid in objs:
        if cid==48:
            m=re.search(rb'Shader\s+"([^"]+)"',b[off:off+size]); sh.append((pid,m.group(1).decode() if m else '?',size))
    out[os.path.basename(os.path.dirname(f))+'/'+os.path.basename(f)[-12:]]={'classes':dict(cnt),'shaders':sh}
json.dump(out,open(sys.argv[2],'w'))
print(len(out),'archivos')
