import struct,sys,os,re,glob,json
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'scan_shaders.py')).read().split("root=sys.argv[1]")[0])
root,outdir=sys.argv[1],sys.argv[2]; os.makedirs(outdir,exist_ok=True); seen={}; index=[]
for f in sorted(glob.glob(root+'/*/*')):
    b=open(f,'rb').read(); objs=parse(b)
    if not objs: continue
    for pid,off,size,tid,cid in objs:
        if cid!=48: continue
        raw=b[off:off+size]; m=re.search(rb'Shader\s+"([^"]+)"',raw)
        if not m: continue
        name=m.group(1).decode(); st=m.start()
        src=None
        for back in range(0,9):
            q=st-back
            if q>=4:
                n=struct.unpack_from('<I',raw,q-4)[0]
                if q+n<=len(raw) and 100<n<400000: src=raw[q:q+n]; break
        if src is None: src=raw[st:]
        text=src.decode('utf8','replace'); key=(name,hash(text))
        index.append((name,os.path.basename(os.path.dirname(f)),pid,len(text)))
        if key in seen: continue
        seen[key]=1; fn=re.sub(r'[^A-Za-z0-9_.-]','_',name)+('' if sum(1 for k in seen if k[0]==name)==1 else '_v%d'%sum(1 for k in seen if k[0]==name))+'.shader'
        open(os.path.join(outdir,fn),'w',encoding='utf8',newline='').write(text)
json.dump(index,open(os.path.join(outdir,'_index.json'),'w'))
print(len(seen),'variantes únicas de',len(set(i[0] for i in index)),'nombres;',len(index),'instancias')
