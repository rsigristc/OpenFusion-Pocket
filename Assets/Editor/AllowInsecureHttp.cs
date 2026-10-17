// Los servidores OpenFusion sirven news.php / main.unity3d por http: Unity 2022 bloquea http por defecto ("Insecure connection not allowed").
#if UNITY_EDITOR
using UnityEditor;

namespace OpenFusion.EditorTools
{
    [InitializeOnLoad]
    static class AllowInsecureHttp
    {
        static AllowInsecureHttp()
        {
            if (PlayerSettings.insecureHttpOption != InsecureHttpOption.AlwaysAllowed)
            {
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
