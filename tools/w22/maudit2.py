import glob,os,sys,json,collections
sys.path.insert(0,'.')
from sf import SF
root=os.path.expanduser('~/mnt/OpenFusionLauncher/ffcache/')
idx={}
for p in glob.glob(root+'*/*/CustomAssetBundle-*'): idx[os.path.basename(p).lower()]=p
files=sorted(glob.glob(root+'*/map_*/BuildPlayer-Map_*')); files=[f for f in files if not f.endswith('sharedAssets')]
if len(sys.argv)>1: files=[f for f in files if sys.argv[1] in f]
sfc={}
def getsf(name):
    if name not in sfc:
        p=idx.get(name)
        sfc[name]=None if not p else SF(open(p,'rb').read())
        if sfc[name] is not None:
            sfc[name]._obj={o['pid']:o for o in sfc[name].objs}; sfc[name]._c={}
    return sfc[name]
def rdx(s,pid):
    if pid not in s._c:
        o=s._obj[pid];t=s.types.get(o['tid']) or s.types.get(o['cid']);s._c[pid]=s.read(t,[o['off']])
    return s._c[pid]
out=[]
for f in files:
    s=SF(open(f,'rb').read()); s._obj={o['pid']:o for o in s.objs}; s._c={}
    obj=s._obj
    go2tr={}
    for pid,o in obj.items():
        if o['cid']==4: go2tr[rdx(s,pid)['m_GameObject']['m_PathID']]=pid
    def pathset(trpid,prefix,acc):
        t=rdx(s,trpid)
        for ch in t['m_Children']:
            if ch['m_PathID'] in obj:
                c=rdx(s,ch['m_PathID']); g=rdx(s,c['m_GameObject']['m_PathID']); p=(prefix+'/' if prefix else '')+g['m_Name']
                acc[p]=(ch['m_PathID'],g['m_IsActive']); pathset(ch['m_PathID'],p,acc)
    for pid,o in obj.items():
        if o['cid']!=111: continue
        a=rdx(s,pid); gp=a['m_GameObject']['m_PathID']; g=rdx(s,gp)
        acc={}
        if gp in go2tr: pathset(go2tr[gp],'',acc)
        # components of the go & subtree: renderer kinds
        rec={'map':os.path.basename(f)[-9:],'go':g['m_Name'],'wrap':a['m_WrapMode'],'clips':[],'inactive':[p for p,(t,ac) in acc.items() if not ac][:3]}
        for ap in a['m_Animations']:
            fid=ap['m_FileID']; cp=ap['m_PathID']
            if fid==0: ss=s
            else: ss=getsf(s.ext[fid-1]) if fid-1<len(s.ext) else None
            if ss is None or cp not in ss._obj: rec['clips'].append({'unresolved':(fid,s.ext[fid-1] if fid and fid-1<len(s.ext) else None)}); continue
            c=rdx(ss,cp)
            cd=dict(cid=ss._obj[cp]['cid'])
            if cd['cid']!=74: rec['clips'].append({'notclip':cd}); continue
            miss=[]
            for cc in c['m_CompressedRotationCurves']:
                if cc['m_Path'] not in acc and cc['m_Path']!='': miss.append(('crot',cc['m_Path']))
            for k,nm in (('m_PositionCurves','pos'),('m_ScaleCurves','scale'),('m_RotationCurves','rot'),('m_FloatCurves','float')):
                for cc in c[k]:
                    if cc['path'] not in acc and cc['path']!='': miss.append((nm,cc['path']))
            fl=[]
            for x in c['m_FloatCurves']:
                nk=len(x['curve']['m_Curve']); fl.append((x['path'],x['attribute'],x['classID'],nk,x['curve']['m_PreInfinity'],x['curve']['m_PostInfinity']))
            def dur(c):
                m=0
                for x in c['m_PositionCurves']+c['m_ScaleCurves']+c['m_RotationCurves']:
                    ks=x['curve']['m_Curve']; 
                    if ks: m=max(m,ks[-1]['time'])
                for x in c['m_FloatCurves']:
                    ks=x['curve']['m_Curve']
                    if ks: m=max(m,ks[-1]['time'])
                return m
            rec['clips'].append({'name':c['m_Name'],'crot':len(c['m_CompressedRotationCurves']),'rot':len(c['m_RotationCurves']),'pos':len(c['m_PositionCurves']),'scale':len(c['m_ScaleCurves']),
              'float':fl,'ev':[(e.get('functionName'),e.get('data')) for e in c.get('m_Events',[])],'miss':miss[:6],'nmiss':len(miss),'dur':dur(c),
              'crot_pp':sorted(set((x['m_PreInfinity'],x['m_PostInfinity']) for x in c['m_CompressedRotationCurves'])),'bundle':s.ext[fid-1] if fid else 'self','pid':cp})
        out.append(rec)
json.dump(out,open('maudit2.json','w'))
print(len(out))
