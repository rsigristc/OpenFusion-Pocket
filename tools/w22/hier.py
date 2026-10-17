import sys,json
sys.path.insert(0,'.')
from sf import SF
f=sys.argv[1]; pat=sys.argv[2]
b=open(f,'rb').read(); s=SF(b)
obj={o['pid']:o for o in s.objs}
cache={}
def rd(pid):
    if pid not in cache:
        o=obj[pid];t=s.types.get(o['tid']) or s.types.get(o['cid']);cache[pid]=(o['cid'],s.read(t,[o['off']]))
    return cache[pid]
gos={pid:rd(pid)[1] for pid,o in obj.items() if o['cid']==1}
def comps(go): return [(c.get('m_Component') or c.get('component') or c) for c in go.get('m_Component',[])]
def show(gpid,ind=0,seen=None):
    g=gos[gpid]
    cl=[]
    tr=None
    for c in g['m_Component']:
        p=c.get('component') or c.get('second') or c
        pid=p['m_PathID'] if isinstance(p,dict) and 'm_PathID' in p else None
        if pid in obj:
            cid=obj[pid]['cid']; cl.append((cid,pid))
            if cid==4: tr=pid
    print(' '*ind+g['m_Name'],'active',g.get('m_IsActive'),'comps',cl)
    if tr:
        t=rd(tr)[1]
        for ch in t['m_Children']:
            cp=ch['m_PathID']
            if cp in obj:
                g2=rd(cp)[1]['m_GameObject']['m_PathID']; show(g2,ind+2)
for pid,g in gos.items():
    if pat in g['m_Name'] and 'springcooler' in g['m_Name']:
        print('===',pid); show(pid)
        for c in g['m_Component']:
            p=c.get('component') or c
            pp=p['m_PathID']
            if obj[pp]['cid']==111:
                print(json.dumps(rd(pp)[1],indent=1)[:1500])
