import sys,glob,os
sys.path.insert(0,'.')
from sf import SF
pat=sys.argv[1]
for f in sorted(glob.glob(os.path.expanduser('~/mnt/OpenFusionLauncher/ffcache/*/*/*'))):
    try: s=SF(open(f,'rb').read())
    except Exception: continue
    obj={o['pid']:o for o in s.objs}
    for pid,o in obj.items():
        if o['cid']!=1: continue
        if o['size']>400: continue
        t=s.types.get(o['tid']) or s.types.get(o['cid'])
        try: g=s.read(t,[o['off']])
        except Exception: continue
        if pat in g['m_Name']:
            cids=[]
            for c in g['m_Component']:
                p=list(c.values())[-1]
                cids.append(obj[p['m_PathID']]['cid'] if p['m_PathID'] in obj else -1)
            print(os.path.basename(os.path.dirname(f))[:30],pid,g['m_Name'],g['m_IsActive'],cids)
