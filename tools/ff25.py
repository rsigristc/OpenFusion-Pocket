"""Reader for Unity 2.5 MonoBehaviour tables, driven by field layouts parsed from decompiled C#."""
import re, struct, glob, os

PRIM = {'int':('<i',4),'uint':('<I',4),'float':('<f',4),'short':('<h',2),'ushort':('<H',2),
        'byte':('<B',1),'sbyte':('<b',1),'long':('<q',8),'ulong':('<Q',8),'double':('<d',8),
        'char':('<H',2)}
STRUCTS = {'Vector2':['x','y'],'Vector3':['x','y','z'],'Vector4':['x','y','z','w'],
           'Quaternion':['x','y','z','w'],'Color':['r','g','b','a'],'Rect':['x','y','w','h']}
UNITY_OBJ = {'GameObject','Texture','Texture2D','Material','Mesh','AudioClip','Shader','Transform',
             'Object','Animation','AnimationClip','Font','Camera','Component','MonoBehaviour','Renderer',
             'GUISkin','GUIStyle','Sprite','Cubemap','TextAsset','ScriptableObject'}

class Model:
    def __init__(self, src_dirs):
        self.classes = {}   # name -> (base, [(type,name)])
        self.enums = set()
        for d in src_dirs:
            for f in glob.glob(os.path.join(d,'*.cs')):
                self._scan(open(f, encoding='utf-8', errors='replace').read())
    def _scan(self, text):
        for m in re.finditer(r'\benum\s+(\w+)', text): self.enums.add(m.group(1))
        # top-level classes only (indent 0)
        for m in re.finditer(r'^(?:public\s+)?(?:abstract\s+|sealed\s+)?class\s+(\w+)(?:\s*:\s*([\w\.]+))?[^\n{]*\n\{(.*?)^\}', text, re.S|re.M):
            name, base, body = m.groups()
            fields=[]
            for line in body.split('\n'):
                fm = re.match(r'^\t(public)\s+([A-Za-z_][\w<>\[\],\.]*)\s+(\w+)\s*(?:=[^;]*)?;\s*$', line)
                if fm and 'static' not in line and 'const' not in line:
                    fields.append((fm.group(2), fm.group(3)))
            self.classes[name]=(base, fields)
    def fields_of(self, cls):
        base, fields = self.classes[cls]
        inherited = self.fields_of(base) if base in self.classes else []
        return inherited + fields

class Reader:
    def __init__(self, data, model, bool_align=False, pad=False):
        self.d=data; self.p=0; self.m=model; self.bool_align=bool_align; self.pad=pad
    def align(self):
        if self.pad: self.p=(self.p+3)&~3
    def prim(self, t):
        fmt,sz=PRIM[t]; v=struct.unpack_from(fmt,self.d,self.p)[0]; self.p+=sz; return v
    def string(self):
        n=struct.unpack_from('<i',self.d,self.p)[0]; self.p+=4
        if n<0 or n>1<<20: raise ValueError('bad string len %d at %d'%(n,self.p))
        s=self.d[self.p:self.p+n].decode('utf-8','replace'); self.p+=n; self.align(); return s
    def value(self, t):
        if t=='bool':
            v=bool(self.d[self.p]); self.p+=1
            if self.bool_align: self.align()
            return v
        if t in PRIM: return self.prim(t)
        if t=='string': return self.string()
        if t in STRUCTS: return {k:self.prim('float') for k in STRUCTS[t]}
        if t in self.m.enums: return self.prim('int')
        if t.endswith('[]'):
            n=struct.unpack_from('<i',self.d,self.p)[0]; self.p+=4
            if n<0 or n>5_000_000: raise ValueError('bad array len %d at %d'%(n,self.p))
            et=t[:-2]; out=[self.value(et) for _ in range(n)]
            if et=='bool' and not self.bool_align: self.align()   # byte arrays are padded
            return out
        if t in UNITY_OBJ:
            f,pid=struct.unpack_from('<ii',self.d,self.p); self.p+=8; return ('PPtr',f,pid)
        if t in self.m.classes: return self.obj(t)
        raise KeyError('unknown type '+t)
    def obj(self, cls):
        o={}
        for t,n in self.m.fields_of(cls): o[n]=self.value(t)
        return o
    def monobehaviour(self, cls):
        self.p=33                       # packed header: PPtrs + m_Enabled + m_Script (no alignment in 2.5)
        name=self.string()
        return name, self.obj(cls)
