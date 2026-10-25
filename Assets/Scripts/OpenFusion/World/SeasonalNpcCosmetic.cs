using System;
using System.Linq;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class SeasonalNpcCosmetic : MonoBehaviour
    {
        public sealed class Hat
        {
            public string Model, Texture; public Vector3 Position, Scale, Rotation; public bool HideTail;
        }
        // NpcCustomization.HalloweenNPCCosmetics in 3.0.3: Billy is no longer in this list.
        public static readonly Hat[] Hats = {
            new Hat { Model="wear/hat_enid.nif", Texture="texture/hat_enid.dds", Position=new Vector3(0,.23f,-.065f), Scale=Vector3.one*-.25f, Rotation=new Vector3(-50,0,0), HideTail=true },
            new Hat { Model="wear/hat_eeneviking.nif", Texture="texture/hat_eeneviking_gbnmbraineatingmeteor.dds", Position=new Vector3(0,.23f,-.1f), Scale=Vector3.one*-.1f, Rotation=new Vector3(-55,0,0), HideTail=true },
            new Hat { Model="wear/hat_lincoln.nif", Texture="texture/hat_blackhat.dds", Position=new Vector3(0,.255f,-.12f), Scale=Vector3.one*-.2f, Rotation=new Vector3(-60,0,0), HideTail=true },
            new Hat { Model="wear/hat_elderflair.nif", Texture="texture/hatmelee_elderflair.dds", Position=new Vector3(0,.23f,-.11f), Scale=Vector3.one*-.16f, Rotation=new Vector3(-60,0,0), HideTail=true },
            new Hat { Model="wear/hat_devilhorns.nif", Texture="texture/pistolhat_rage_devilhorns.dds", Position=new Vector3(0,.15f,-.05f), Scale=Vector3.one*-.25f, Rotation=new Vector3(-20,0,0) }
        };
        SeasonalEvents owner; ModelFactory models; string path; int id; float retry; GameObject hat; Transform tail; Vector3 tailScale; Material material;
        public GameObject AttachedHat => hat;
        public static bool Supports(string model) => model != null && (model.EndsWith("/mob_sneakyspawn.kfm", StringComparison.OrdinalIgnoreCase) || model.EndsWith("/npc_frogwarp.kfm", StringComparison.OrdinalIgnoreCase));
        public static bool Frog(string model) => model != null && model.EndsWith("/npc_frogwarp.kfm", StringComparison.OrdinalIgnoreCase);
        public static int Pick(string model, int seed) => Frog(model) ? 2 : (int)((uint)seed % (uint)Hats.Length);
        public void Init(SeasonalEvents controller, ModelFactory factory, string model, int seed)
        { owner = controller; models = factory; path = model; id = seed; Refresh(); }
        Transform Bone(string name) => GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        public void Refresh()
        {
            if (owner == null || !owner.Halloween) { Clear(); return; }
            if (hat != null || models == null) return;
            retry = Time.unscaledTime + 1f;
            bool frog = Frog(path); var option = Hats[Pick(path, id)];
            var head = Bone(frog ? "Head03" : "Head01");
            var model = new ModelRef { Kind=ModelKind.Equip, Model=option.Model };
            if (head == null || !models.IsAvailable(model) || !models.Locate(option.Texture).Ok) return;
            var texture = models.Texture(option.Texture); if (texture == null) return;
            hat = models.Instantiate(model, head); if (hat == null) return;
            hat.name = "SeasonalHalloweenHat";
            hat.transform.localPosition = (frog ? new Vector3(0,-.18f,.1f) : new Vector3(0,.1f,.1f)) + option.Position;
            hat.transform.localScale = Vector3.one * (frog ? .65f : 2.2f) + option.Scale;
            hat.transform.localRotation = Quaternion.Euler((frog ? new Vector3(69,0,0) : new Vector3(60,0,0)) + option.Rotation);
            WorldEntities.ActivateAll(hat);
            foreach (var t in hat.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            var renderer = hat.GetComponentInChildren<Renderer>(true);
            if (renderer != null && renderer.sharedMaterial != null)
            {
                material = new Material(renderer.sharedMaterial); material.mainTexture = texture;
                var slots = renderer.sharedMaterials; slots[0] = material; renderer.sharedMaterials = slots;
            }
            if (option.HideTail) { tail = Bone("Tail01"); if (tail != null) { tailScale = tail.localScale; tail.localScale = Vector3.zero; } }
        }
        public void Clear()
        {
            if (tail != null) tail.localScale = tailScale; tail = null;
            if (hat != null) { hat.SetActive(false); Release(hat); } hat = null;
            if (material != null) Release(material); material = null;
        }
        static void Release(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        void LateUpdate()
        {
            if (tail != null && hat != null) tail.localScale = Vector3.zero;
            if (hat == null && owner != null && owner.Halloween && Time.unscaledTime >= retry) Refresh();
        }
        void OnDestroy() { Clear(); if (owner != null) owner.Forget(this); }
    }
}
