# Extrae los MonoBehaviours de tabledata.resourceFile a blobs/*.bin (pip install UnityPy)
import UnityPy, struct, sys, os
env = UnityPy.load(sys.argv[1]); os.makedirs('blobs', exist_ok=True)
for o in env.objects:
    if o.type.name == 'MonoBehaviour':
        raw = o.get_raw_data(); n = struct.unpack_from('<i', raw, 33)[0]
        open(f'blobs/{raw[37:37+n].decode()}.bin', 'wb').write(raw)
