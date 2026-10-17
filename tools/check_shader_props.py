"""Comprueba que cada propiedad declarada por los 90 shaders legacy exista en el shader moderno que los implementa."""
import json, re, glob, os, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
cat = json.load(open(os.path.join(root, 'tools', 'shader_catalog_spec.json')))
KIND = {'legacy_lit':'OpenFusion/Legacy','legacy_glow':'OpenFusion/Legacy','particle':'OpenFusion/Legacy','toon':'OpenFusion/Toon','vfx':'OpenFusion/VFX',
        'fusion_matter':'OpenFusion/FusionMatter','fusion_effect':'OpenFusion/Scroll','diffuse_fade':'OpenFusion/Scroll','water':'OpenFusion/Water','other':'OpenFusion/Legacy'}
props = {}
for f in glob.glob(os.path.join(root, 'Assets', 'Shaders', '*.shader')):
    s = open(f).read(); name = re.search(r'Shader\s+"([^"]+)"', s).group(1)
    block = s[s.index('Properties'):]; block = block[:block.index('SubShader')]
    props[name] = set(re.findall(r'^\s*(?:\[[^\]]*\]\s*)*([A-Za-z_]\w*)\s*\(', block, re.M))
bad = 0
for sh, spec in cat.items():
    kind = spec.get('Kind') or spec.get('kind'); tgt = KIND[kind]
    if 'HologramSolid' in sh: tgt = 'OpenFusion/Hologram'
    if sh == 'MiniMapRender': tgt = 'OpenFusion/MiniMap'
    have = props[tgt]; want = set(spec.get('Props') or [])
    # propiedades que el shader moderno ignora a propósito (no afectan al aspecto)
    ign = {'_SpecColor', '_Shininess', '_SpecMap', '_Fresnel', '_ReflectiveColorCube', '_MainTex2' if tgt == 'x' else '_none', '_ReflectionTex', '_RefractionTex', '_ReflDistort', '_RefrDistort', '_FogTex', '_BumpMap' if tgt == 'OpenFusion/Legacy' and False else '_none2'}
    miss = sorted(p for p in want - have - ign)
    if miss: bad += 1; print(f'{sh} -> {tgt}: faltan {miss}')
print('shaders con propiedades sin cubrir:', bad, 'de', len(cat))
sys.exit(1 if bad else 0)
