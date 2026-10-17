"""Analiza el ShaderLab de los shaders de Unity 2.5 (shader_dump/*.shader) y genera shader_catalog.json.
Valida que los parámetros derivados del NOMBRE (normal_*/particle_*) coincidan con los del código fuente."""
import re,glob,json,os,sys,collections
src_dir=sys.argv[1]; out=sys.argv[2]
FACT={'one':'One','zero':'Zero','srcalpha':'SrcAlpha','invsrcalpha':'OneMinusSrcAlpha','srccolor':'SrcColor','invsrccolor':'OneMinusSrcColor',
      'destalpha':'DstAlpha','invdestalpha':'OneMinusDstAlpha','destcolor':'DstColor','invdestcolor':'OneMinusDstColor'}
def strip(t):
    t=t.replace('\r','')
    t=re.sub(r'Program ""\s*\{.*?\n\}\n(?=\s*#LINE)','',t,flags=re.S)   # asm de vertex/fragment programs
    t=re.sub(r'//[^\n]*','',t); return t
def from_name(n):
    m=re.match(r'^(normal|particle|normal_glow)_blend([A-Z][a-z]+)([A-Z][a-z]+?)(Test)?((?:_[a-zA-Z]+)*)$',n)
    if not m: return None
    fam,s,d,test,rest=m.groups(); s=s.lower(); d=d.lower()
    if s not in FACT or d not in FACT: return None
    return {'src':FACT[s],'dst':FACT[d],'alphatest':bool(test),'zwrite':'zwriteOff' not in rest,'cull':'Off' if 'cullOff' in rest else 'Back','vertexColorAD':'vertexColorAD' in rest}
def parse(path):
    raw=open(path,encoding='utf8').read(); t=strip(raw)
    name=re.search(r'Shader\s+"([^"]+)"',t).group(1)
    props=re.findall(r'^\s*(_\w+)\s*\("([^"]*)"\s*,\s*(2D|Cube|Color|Float|Vector|Range\s*\([^)]*\))\s*\)',t,re.M)
    ss=re.split(r'\bSubShader\b',t); head=ss[0]; first=ss[1] if len(ss)>1 else ''
    def g(rx,s): m=re.search(rx,s); return m
    def state(s):
        d={}
        m=g(r'Blend\s+(\w+)\s+(\w+)',s)
        if m: d['blend']=[m.group(1),m.group(2)]
        m=g(r'Cull\s+(\w+)',s); 
        if m: d['cull']=m.group(1)
        m=g(r'ZWrite\s+(\w+)',s)
        if m: d['zwrite']=m.group(1)
        m=g(r'(?i)alphatest\s+(\w+)\s+(\[?[\w.]+\]?)',s)
        if m: d['alphatest']=[m.group(1),m.group(2)]
        m=g(r'Queue"?\s*=\s*"?([A-Za-z]+(?:[+-]\d+)?)',s)
        if m: d['queue']=m.group(1)
        d['lighting']=bool(g(r'Lighting\s+On',s)); d['colorMaterialAD']=bool(g(r'ColorMaterial\s+AmbientAndDiffuse',s))
        return d
    st=state(head+first)
    passes=len(re.findall(r'\bPass\b',first.split('SubShader')[0]))
    combiners=[re.sub(r'\s+',' ',c.strip()) for c in re.findall(r'(?i)combine\s+([^\n}]+)',first)]
    has_prog='Program ""' in raw
    pn=re.findall(r'Name\s+"(\w+)"',first)
    return name,{'state':st,'props':[[p[0],p[2].split('(')[0].strip()] for p in props],'passes':passes,'passNames':pn,'combiners':combiners,'hasVertexProgram':has_prog,
                 'fallback':(g(r'FallBack\s+"([^"]+)"',t).group(1) if g(r'FallBack\s+"([^"]+)"',t) else None),'subshaders':len(ss)-1}
cat={}; bad=[]; checked=0
for f in sorted(glob.glob(src_dir+'/*.shader')):
    name,info=parse(f)
    pn=from_name(name)
    if name.split('_')[0] in('normal','particle') and pn:
        st=info['state']; checked+=1
        bl=st.get('blend',['One','Zero'])
        # los 'normal_blend*' sin Blend explícito (p.ej. ...Test) usan alphatest sin mezcla
        got={'src':bl[0],'dst':bl[1],'zwrite':st.get('zwrite','On')!='Off','cull':st.get('cull','Back'),'alphatest':'alphatest' in st,'vertexColorAD':st['colorMaterialAD']}
        diffs={k:(pn[k],got[k]) for k in got if k in pn and pn[k]!=got[k]}
        info['nameParams']=pn
        if diffs: bad.append((name,diffs)); info['nameMismatch']={k:list(v) for k,v in diffs.items()}
    cat.setdefault(name,[]).append(info)
json.dump(cat,open(out,'w'),indent=1)
print(len(cat),'shaders;',sum(len(v) for v in cat.values()),'variantes; comprobados nombre-vs-fuente:',checked,'; discrepancias:',len(bad))
for n,d in bad[:40]: print('  ',n,d)

# ---------- normalización: 1 especificación por NOMBRE de shader (la variante con más instancias / primera) ----------
CANON={v.lower():v for v in FACT.values()}; CANON.update({k:v for k,v in FACT.items()})
QBASE={'background':1000,'geometry':2000,'alphatest':2450,'transparent':3000,'overlay':4000}
def queue(q):
    if not q: return 2000
    m=re.match(r'([A-Za-z]+)([+-]\d+)?',q); return QBASE.get(m.group(1).lower(),2000)+int(m.group(2) or 0)
def kind(n,info):
    if n.startswith('Custom/VFX'): return 'vfx'
    if 'SkinnedFusionMatter' in n: return 'fusion_matter'
    if 'FusionEffect' in n: return 'fusion_effect'
    if 'Toon' in n or n.startswith('Skin_DirLight') or n.startswith('Custom/Rim'): return 'toon'
    if n.startswith('particle'): return 'particle'
    if n.startswith('normal_glow'): return 'legacy_glow'
    if n.startswith('normal') or n=='RetroLit': return 'legacy_lit'
    if n=='ffWater': return 'water'
    if n=='retro_diffuseFade': return 'diffuse_fade'
    return 'other'
spec={}
for n,vs in cat.items():
    info=vs[0]; st=info['state']; bl=st.get('blend')
    at=st.get('alphatest')
    spec[n]={'kind':kind(n,info),'variants':len(vs),
      'src':CANON[bl[0].lower()] if bl else 'One','dst':CANON[bl[1].lower()] if bl else 'Zero',
      'zwrite':st.get('zwrite','On')!='Off','cull':st.get('cull','Back'),
      'alphaTest':at[0] if at else None,'alphaTestValue':at[1].strip('[]') if at else None,
      'queue':queue(st.get('queue')),'vertexColorAD':st['colorMaterialAD'],'lighting':st['lighting'],
      'props':[p[0] for p in info['props']],'hasProgram':info['hasVertexProgram'],'outlinePass':'OUTLINE' in info['passNames'],
      'alphaPrimary':any(re.search(r',\s*texture\s*\*\s*primary',c) for c in info['combiners'])}
    if len({json.dumps(v['state'],sort_keys=True) for v in vs})>1: spec[n]['variantStatesDiffer']=True
json.dump(spec,open(out.replace('.json','_spec.json'),'w'),indent=1)
print(collections.Counter(v['kind'] for v in spec.values()), 'variantes con estado distinto:',[n for n,v in spec.items() if v.get('variantStatesDiffer')])
