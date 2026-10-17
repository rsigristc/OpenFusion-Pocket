import glob,os,sys,json,collections
sys.path.insert(0,'.')
from sf import SF
root=os.path.expanduser('~/mnt/OpenFusionLauncher/ffcache/')
files=sorted(glob.glob(root+'*/map_*/BuildPlayer-Map_*')); files=[f for f in files if not f.endswith('sharedAssets')]
sig=collections.Counter(); ex={}; modeNames=collections.Counter(); cids=collections.Counter()
for f in files:
    s=SF(open(f,'rb').read()); obj={o['pid']:o for o in s.objs}
    for pid,o in obj.items():
        cids[o['cid']]+=1
        if o['cid']!=114: continue
        t=s.types.get(o['tid']) or s.types.get(o['cid']); v=s.read(t,[o['off']])
        k=tuple(sorted(v.keys())); sig[k]+=1
        if k not in ex:
            g=s.read(s.types.get(obj[v['m_GameObject']['m_PathID']]['tid']) or s.types.get(1),[obj[v['m_GameObject']['m_PathID']]['off']]) if v['m_GameObject']['m_PathID'] in obj else {}
            ex[k]=(os.path.basename(f)[-9:],g.get('m_Name'))
        if 'mode' in v and len(v)<=6:
            g=s.read(s.types.get(obj[v['m_GameObject']['m_PathID']]['tid']) or s.types.get(1),[obj[v['m_GameObject']['m_PathID']]['off']])
            modeNames[(g['m_Name'],v['mode'])]+=1
for k,c in sig.most_common(): print(c,k,ex[k])
print(modeNames.most_common(40))
print(cids)
