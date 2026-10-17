import struct,sys,os,re,glob,json,collections
class SF:
    def __init__(s,b):
        s.b=b; ms,fs,ver,do=struct.unpack_from('>IIII',b,0)
        if ver!=6: raise ValueError('ver%d'%ver)
        p=fs-ms; s.be=b[p]!=0; p+=1; s.p=p; s.do=do
        e='>' if s.be else '<'; s.e=e
        s.types={}; nt=s.u32()
        for i in range(nt):
            cid=s.i32(); s.types[cid]=s.node()
        no=s.u32(); s.objs=[]
        for i in range(no):
            pid,off,size,tid=struct.unpack_from(e+'iIIi',b,s.p); s.p+=16
            cid=struct.unpack_from(e+'H',b,s.p)[0]; s.p+=4
            s.objs.append(dict(pid=pid,off=off+do,size=size,tid=tid,cid=cid))
        ne=s.u32(); s.ext=[]
        for i in range(ne):
            s.cstr(); s.p+=20; s.ext.append(s.cstr().decode('utf8','replace'))
    def u32(s): v=struct.unpack_from(s.e+'I',s.b,s.p)[0]; s.p+=4; return v
    def i32(s): v=struct.unpack_from(s.e+'i',s.b,s.p)[0]; s.p+=4; return v
    def cstr(s):
        e=s.b.index(0,s.p); r=s.b[s.p:e]; s.p=e+1; return r
    def node(s):
        def mk():
            t=s.cstr().decode(); n=s.cstr().decode(); s.p+=4; s.i32(); s.i32(); s.i32(); meta=s.i32(); cc=s.u32()
            return {'t':t,'n':n,'m':meta,'c':[]},cc
        root,cc=mk(); stack=[(root,cc)]
        while stack:
            nd,rem=stack[-1]
            if rem==0: stack.pop(); continue
            stack[-1]=(nd,rem-1); ch,cc=mk(); nd['c'].append(ch); stack.append((ch,cc))
        return root
    def read(s,n,st):
        t=n['t']; e=s.e
        if t in('SInt8','UInt8','char','bool'): v=s.b[st[0]]; st[0]+=1; v=bool(v) if t=='bool' else v
        elif t in('short','SInt16','unsigned short','UInt16'): v=struct.unpack_from(e+('h' if 'S' in t or t=='short' else 'H'),s.b,st[0])[0]; st[0]+=2
        elif t in('int','SInt32','unsigned int','UInt32'): v=struct.unpack_from(e+('i' if t in('int','SInt32') else 'I'),s.b,st[0])[0]; st[0]+=4
        elif t in('long long','SInt64','unsigned long long','UInt64'): v=struct.unpack_from(e+'q',s.b,st[0])[0]; st[0]+=8
        elif t=='float': v=struct.unpack_from(e+'f',s.b,st[0])[0]; st[0]+=4
        elif t=='double': v=struct.unpack_from(e+'d',s.b,st[0])[0]; st[0]+=8
        elif t=='string':
            l=struct.unpack_from(e+'i',s.b,st[0])[0]; st[0]+=4; v=s.b[st[0]:st[0]+l].decode('utf8','replace'); st[0]+=l; st[0]=(st[0]+3)&~3
        elif t=='TypelessData':
            l=struct.unpack_from(e+'i',s.b,st[0])[0]; st[0]+=4; v=None; st[0]+=l
        elif n['c'] and n['c'][0]['t']=='Array':
            a=n['c'][0]; l=struct.unpack_from(e+'i',s.b,st[0])[0]; st[0]+=4; sub=a['c'][1]
            if sub['t'] in('UInt8','char','SInt8'): st[0]+=l; v=None
            else: v=[s.read(sub,st) for _ in range(l)]
            if a['m']&0x4000: st[0]=(st[0]+3)&~3
        else: v={c['n']:s.read(c,st) for c in n['c']}
        if n['m']&0x4000: st[0]=(st[0]+3)&~3
        return v
def shname(b,o):
    raw=b[o['off']:o['off']+o['size']]; m=re.search(rb'Shader\s+"([^"]+)"',raw); return m.group(1).decode() if m else None
root=sys.argv[1]; files=sorted(glob.glob(root+'/*/*')); byhash={}; shaders={}
skipped=collections.Counter()
for f in files:
    b=open(f,'rb').read()
    try: s=SF(b)
    except Exception as ex: skipped[str(ex)[:30]]+=1; continue
    h=os.path.basename(f).lower()
    byhash[h]=f
    for o in s.objs:
        if o['cid']==48:
            n=shname(b,o)
            if n: shaders[(h,o['pid'])]=n
stat=collections.defaultdict(lambda:{'n':0,'tex':collections.Counter(),'flt':collections.Counter(),'col':collections.Counter(),'bundles':set()})
unresolved=collections.Counter(); total=0
for f in files:
    b=open(f,'rb').read()
    try: s=SF(b)
    except Exception: continue
    h=os.path.basename(f).lower()
    for o in s.objs:
        if o['cid']!=21: continue
        t=s.types.get(o['tid']) or s.types.get(o['cid'])
        try: v=s.read(t,[o['off']])
        except Exception as ex: unresolved['parse:'+type(ex).__name__]+=1; continue
        total+=1; sp=v['m_Shader']; fid,pid=sp['m_FileID'],sp['m_PathID']
        if fid==0: key=(h,pid)
        else:
            if fid-1<len(s.ext): key=(os.path.basename(s.ext[fid-1]).lower(),pid)
            else: key=None
        nm=shaders.get(key) if key else None
        if nm is None: unresolved[str(fid)+':'+('none' if key is None else key[0][:24])]+=1; nm='?unresolved'
        st=stat[nm]; st['n']+=1; st['bundles'].add(os.path.basename(os.path.dirname(f))[:20])
        sv=v['m_SavedProperties']
        for k,tx in sv['m_TexEnvs']:
            nmk=k['name'] if isinstance(k,dict) and 'name' in k else str(k)
            st['tex'][nmk]+=1
        for k,_ in sv['m_Floats']: st['flt'][k['name'] if isinstance(k,dict) else str(k)]+=1
        for k,_ in sv['m_Colors']: st['col'][k['name'] if isinstance(k,dict) else str(k)]+=1
out={k:{'n':v['n'],'tex':dict(v['tex']),'flt':dict(v['flt']),'col':dict(v['col']),'bundles':sorted(v['bundles'])[:6],'nb':len(v['bundles'])} for k,v in stat.items()}
json.dump({'total':total,'stat':out,'unresolved':dict(unresolved),'skipped':dict(skipped)},open(sys.argv[2],'w'))
print('omitidos',dict(skipped)); print(total,'materiales',len(out),'shaders usados; sin resolver',sum(unresolved.values()))
