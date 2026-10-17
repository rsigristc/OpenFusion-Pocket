// Stub parcial de UnityEngine/Newtonsoft para comprobar tipos en el sandbox (NO es el código del proyecto).
using System; using System.Collections; using System.Collections.Generic;
namespace UnityEngine {
 public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} public static Vector2 one => new Vector2(1,1); public static Vector2 operator*(Vector2 a,float b)=>a; }
 public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public static Vector3 one => new Vector3(1,1,1); public static Vector3 zero => new Vector3(0,0,0); public static Vector3 operator*(Vector3 a,float b)=>a; public static Vector3 operator-(Vector3 a)=>a; public static Vector3 operator+(Vector3 a,Vector3 b)=>a; public static Vector3 operator-(Vector3 a,Vector3 b)=>a; public Vector3 normalized => this; public void Normalize(){} public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a; public static implicit operator Vector4(Vector3 v)=>new Vector4(v.x,v.y,v.z,0); public float magnitude => 0; public float sqrMagnitude => 0; }
 public struct Vector4 { public float x,y,z,w; public Vector4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;} }
 public struct Quaternion { public static Vector3 operator *(Quaternion q, Vector3 v)=>v; public float x,y,z,w; public Quaternion(float a,float b,float c,float d){x=a;y=b;z=c;w=d;} public static Quaternion identity => new Quaternion(0,0,0,1); public static Quaternion Euler(float a,float b,float c)=>identity; public static Quaternion LookRotation(Vector3 f)=>identity; public static Quaternion RotateTowards(Quaternion a,Quaternion b,float d)=>a; }
 public struct Color { public float r,g,b,a; public Color(float R,float G,float B,float A){r=R;g=G;b=B;a=A;} public static Color white => new Color(1,1,1,1); public static Color cyan => new Color(0,1,1,1); public static Color yellow => new Color(1,0.92f,0.016f,1); public static Color operator*(Color a,float b)=>a; public static Color operator*(Color a,Color b)=>a; }
 public struct Color32 { public byte r,g,b,a; }
 public struct Rect { public float x,y,w,h; public Rect(float a,float b,float c,float d){x=a;y=b;w=c;h=d;} }
 public struct Matrix4x4 { public float this[int r,int c]{get=>0;set{}} public float this[int i]{get=>0;set{}} }
 public struct BoneWeight { public int boneIndex0,boneIndex1,boneIndex2,boneIndex3; public float weight0,weight1,weight2,weight3; }
 public struct Keyframe { public Keyframe(float t,float v,float i,float o){} }
 public class AnimationCurve { public AnimationCurve(params Keyframe[] k){} }
 public struct Bounds { public Vector3 center,size; }
 public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad } public enum WrapMode { Default=0, Once=1, Loop=2, PingPong=4, ClampForever=8 }
 public enum AnimationBlendMode { Blend, Additive }
 public enum TextureWrapMode { Repeat, Clamp } public enum FilterMode { Point, Bilinear, Trilinear }
 public enum TextureFormat { RGBA32 = 4, DXT1 = 10 }
 public class Object { public string name; public static void Destroy(Object o){} public static void DestroyImmediate(Object o){} public static T Instantiate<T>(T o) where T:Object => o; public static implicit operator bool(Object o)=>o!=null; }
 public class Component : Object { public GameObject gameObject; public Transform transform; public T GetComponent<T>() => default(T); public T[] GetComponentsInChildren<T>(bool inc=false)=>null; public T GetComponentInChildren<T>()=>default(T); }
 public class Behaviour : Component { public bool enabled; }
 public class MonoBehaviour : Behaviour { public Coroutine StartCoroutine(IEnumerator e)=>null; public void StopCoroutine(Coroutine c){} }
 public class Coroutine {} public class YieldInstruction {} public class AsyncOperation : YieldInstruction {}
 public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s){} }
 public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string s){} } public sealed class RangeAttribute : Attribute { public RangeAttribute(float a,float b){} }
 public class GameObject : Object { public Transform transform; public int layer; public bool activeInHierarchy; public GameObject(string n){} public GameObject(){} public void SetActive(bool b){} public T AddComponent<T>() where T:Component,new() => new T(); public T GetComponent<T>() => default(T); public T[] GetComponentsInChildren<T>(bool inc=false)=>null; public static GameObject CreatePrimitive(PrimitiveType t)=>null; }
 public class Transform : Component, IEnumerable { public Vector3 forward; public Vector3 position, localPosition, localScale; public Quaternion rotation, localRotation; public Vector3 eulerAngles; public void LookAt(Vector3 p){} public Transform parent; public void SetParent(Transform p,bool w){} public Transform Find(string n)=>null; public IEnumerator GetEnumerator()=>null; }
 public class Renderer : Component { public Material[] materials, sharedMaterials; public Material material, sharedMaterial; }
 public class MeshRenderer : Renderer {} public class MeshFilter : Component { public Mesh sharedMesh; }
 public class SkinnedMeshRenderer : Renderer { public Mesh sharedMesh; public Transform[] bones; public Transform rootBone; public bool updateWhenOffscreen; }
 public class Collider : Component {} public class MeshCollider : Collider { public Mesh sharedMesh; }
 public class Camera : Behaviour { public Color backgroundColor; public CameraClearFlags clearFlags; } public enum CameraClearFlags { Skybox, SolidColor }
 public class Animation : Behaviour { public AnimationClip clip; public bool playAutomatically; public WrapMode wrapMode; public void AddClip(AnimationClip c,string n){} public AnimationClip GetClip(string n)=>null; public bool Play()=>true; public bool Play(string n)=>true; public void Sample(){} public AnimationState this[string n]=>null; }
 public class AnimationState { public bool enabled; public float normalizedTime, weight; public int layer; public AnimationBlendMode blendMode; public WrapMode wrapMode; }
 public class AnimationClip : Object { public bool legacy; public WrapMode wrapMode; public float frameRate; public void SetCurve(string p,Type t,string prop,AnimationCurve c){} }
 public class Mesh : Object { public Vector3[] vertices, normals; public Vector2[] uv, uv2; public Vector4[] tangents; public BoneWeight[] boneWeights; public Matrix4x4[] bindposes; public int subMeshCount; public Rendering.IndexFormat indexFormat; public void SetTriangles(int[] t,int s,bool b){} public void RecalculateNormals(){} public void RecalculateBounds(){} }
 public class Texture : Object {} public class Texture2D : Texture { public int width,height; public Color32[] GetPixels32()=>null; public Texture2D(int w,int h,TextureFormat f,bool m){} public TextureWrapMode wrapMode; public FilterMode filterMode; public void SetPixelData<T>(T[] d,int l){} public void Apply(bool a,bool b){} public void Compress(bool h){} public void SetPixels32(object[] c){} }
 public class Shader : Object { public static Shader Find(string n)=>null; public static int PropertyToID(string n)=>0; public static void SetGlobalColor(string n,Color c){} public static void SetGlobalColor(int n,Color c){} public static void SetGlobalVector(int n,Vector4 v){} public static void SetGlobalFloat(int n,float f){} public static void SetGlobalFloat(string n,float f){} public static void SetGlobalVector(string n,Vector4 v){} }
 public class Material : Object { public Material(Shader s){} public Material(Material m){} public int renderQueue; public Color color; public Texture mainTexture; public bool HasProperty(string p)=>true; public void SetFloat(string n,float f){} public float GetFloat(string n)=>0; public void SetColor(string n,Color c){} public Color GetColor(string n)=>default(Color); public void SetTexture(string n,Texture t){} public void SetTextureScale(string n,Vector2 v){} public void SetTextureOffset(string n,Vector2 v){} public void EnableKeyword(string k){} public void DisableKeyword(string k){} }
 public class Terrain : Behaviour { public static GameObject CreateTerrainGameObject(TerrainData d)=>null; public TerrainData terrainData; public void SetNeighbors(Terrain a,Terrain b,Terrain c,Terrain d){} public Material materialTemplate; public static implicit operator bool(Terrain t)=>t!=null; }
 public class TerrainData : Object { public int heightmapResolution; public Vector3 size; public void SetHeights(int x,int y,float[,] h){} public int alphamapResolution; public TerrainLayer[] terrainLayers; public void SetAlphamaps(int x,int y,float[,,] a){} public Vector3 heightmapScale; }
 public class TerrainLayer : Object { public Texture2D diffuseTexture; public Vector2 tileSize; } public class TerrainCollider : Collider { public TerrainData terrainData; }
 public class Light : Behaviour { public Color color; public float intensity; }
 public static class RenderSettings { public static bool fog; public static Color fogColor, ambientLight; public static float fogDensity, fogStartDistance, fogEndDistance; public static Rendering.AmbientMode ambientMode; public static FogMode fogMode; } public enum FogMode { Linear, Exponential }
 public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string s){} }
 public enum KeyCode { LeftShift, Q, E }
 public struct Touch { public Vector2 deltaPosition; }
 public static class Input { public static float GetAxisRaw(string a)=>0f; public static float GetAxis(string a)=>0f; public static bool GetKey(KeyCode k)=>false; public static bool GetMouseButton(int b)=>false; public static int touchCount; public static Touch GetTouch(int i)=>default(Touch); }
 public static class Mathf { public const float Rad2Deg = 57.29578f; public static float Atan2(float y,float x)=>0f; public static int RoundToInt(float f)=>0; public static float Clamp(float v,float a,float b)=>v; public static int Clamp(int v,int a,int b)=>v; public static float Clamp01(float v)=>v; public static int FloorToInt(float f)=>0; public static float Floor(float f)=>f; public static float Lerp(float a,float b,float t)=>a; public static float Max(float a,float b)=>a; public static float Min(float a,float b)=>a; public static int Max(int a,int b)=>a; public static int Min(int a,int b)=>a; public static float Abs(float a)=>a; public static float Sqrt(float a)=>a; }
 public static class Time { public static float time, deltaTime; }
 public static class Debug { public static void Log(object o){} public static void LogWarning(object o){} public static void LogError(object o){} }
 public static class Application { public static string persistentDataPath; }
 public static class Resources { public static AsyncOperation UnloadUnusedAssets()=>null; }
 public class Graphics {}
}
namespace UnityEngine.Rendering { public enum BlendMode { Zero, One, DstColor, SrcColor, OneMinusDstColor, SrcAlpha, OneMinusSrcColor, DstAlpha, OneMinusDstAlpha, SrcAlphaSaturate, OneMinusSrcAlpha } public enum CullMode { Off, Front, Back } public enum IndexFormat { UInt16, UInt32 } public enum AmbientMode { Skybox, Trilight, Flat, Custom } }
namespace Newtonsoft.Json { public class JsonPropertyAttribute : Attribute { public JsonPropertyAttribute(string n){} } public static class JsonConvert { public static T DeserializeObject<T>(string s)=>default(T); public static string SerializeObject(object o)=>""; } }
