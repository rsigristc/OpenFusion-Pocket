using System;
using System.Reflection;
using OpenFusion.World;
using OpenFusion.Net;
using UnityEditor;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class PlatformLandingRegressionChecks
    {
        public static void Batch()
        {
            try { CameraGroundingRegressionChecks.Run(); Run(); EditorApplication.Exit(0); }
            catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
        }
        static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        public static void Run()
        {
            var host=new GameObject("Platform landing regression");var player=new GameObject("Warped player");
            var geometry=new GameObject("Landing geometry");var previous=GameClient.FindSelf;var oldFlow=WarpFlow.Instance;
            bool water=Physics.GetIgnoreLayerCollision(0,2),preview=Physics.GetIgnoreLayerCollision(0,30);
            try
            {
                GameObject Box(string name,Vector3 position,Vector3 scale,Transform parent)
                {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=scale;return go;}
                Box("Platform top at 12m",new Vector3(15000,11.75f,15000),new Vector3(10,.5f,10),geometry.transform);
                Box("Roof must not win",new Vector3(15000,12.6f,15000),new Vector3(10,.2f,10),geometry.transform);
                var trigger=Box("Trigger must not act as floor",new Vector3(15000,11.89f,15000),new Vector3(10,.02f,10),geometry.transform);trigger.GetComponent<Collider>().isTrigger=true;
                player.transform.position=new Vector3(15000,11.8f,15000);
                var own=Box("Self collider must not act as floor",new Vector3(15000,11.84f,15000),new Vector3(1,.02f,1),player.transform);
                Physics.SyncTransforms();
                Check(Ground.TrySupport(player.transform.position,player.transform,1,2,50,out float floor)&&Mathf.Abs(floor-12)<.0001f,"Select the nearby real platform, excluding the actor, triggers and higher roof.");
                UnityEngine.Object.DestroyImmediate(own);
                var client=host.AddComponent<GameClient>();client.enabled=false;client.SendMovement=false;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var ensure=typeof(GameClient).GetMethod("EnsureCC",flags);var cc=(CharacterController)ensure.Invoke(client,new object[]{player.transform});
                client.QueueServerRelocation(1500000,1500000,1180);
                Check((bool)typeof(GameClient).GetMethod("ApplyServerRelocation",flags).Invoke(client,new object[]{player.transform}),"The server relocation must be applied.");
                var bootstrap=host.AddComponent<WorldBootstrap>();bootstrap.AutoStart=false;client.Bootstrap=bootstrap;GameClient.FindSelf=client;
                var loading=typeof(WorldBootstrap).GetField("mapLoading",flags);loading.SetValue(bootstrap,true);
                var flow=WarpFlow.Ensure(host);var dispatcher=new PacketDispatcher();typeof(WarpFlow).GetMethod("Bind",flags).Invoke(flow,new object[]{dispatcher});
                foreach(uint type in new[]{PacketIds.P_FE2CL_REP_PC_GOTO_SUCC,PacketIds.P_FE2CL_REP_PC_WARP_USE_NPC_SUCC})
                {
                    WarpFlow.Sent();Check(WarpFlow.Blocking,"Warp request must initially block movement.");
                    dispatcher.Dispatch(ClientPackets.New(type,12));typeof(WarpFlow).GetMethod("Update",flags).Invoke(flow,null);
                    Check((float)typeof(WarpFlow).GetField("awaitUntil",flags).GetValue(flow)==0&&WarpFlow.Blocking,"A valid relocation clears the 6s wait while destination loading still blocks movement.");
                }
                var settle=typeof(GameClient).GetMethod("ResolveRelocationSupport",flags);
                settle.Invoke(client,new object[]{player.transform});Check(Mathf.Abs(player.transform.position.y-11.8f)<.0001f,"Do not resolve against the departing map while destination assets are loading.");
                loading.SetValue(bootstrap,false);Check(!WarpFlow.Blocking,"Movement must resume after acknowledgement and map readiness, not the fallback timeout.");settle.Invoke(client,new object[]{player.transform});
                Check(Mathf.Abs(player.transform.position.y-12)<.0001f&&player.transform.position.x==15000&&player.transform.position.z==15000&&cc.enabled,"After loading, lift only vertical shallow penetration and retain the controller/XZ.");
                UnityEngine.Object.DestroyImmediate(GameObject.Find("Roof must not win"));
                var walk=typeof(GameClient).GetMethod("WalkCC",flags);
                for(int frame=0;frame<180;frame++)
                {walk.Invoke(client,new object[]{player.transform,Vector3.zero,Vector3.zero});Check(Mathf.Abs(player.transform.position.y-12)<.0001f,"Platform support must not create an idle sinking/reset camera loop.");}
                typeof(GameClient).GetField("airborne",flags).SetValue(client,true);player.transform.position=new Vector3(15000,14,15000);
                walk.Invoke(client,new object[]{player.transform,Vector3.zero,Vector3.zero});Check(player.transform.position.y>13.5f,"Do not snap a legitimate airborne player down to the platform.");
                Debug.Log("[platform landing regression] streamed warp support, shallow penetration, actor/trigger/roof exclusions, stable 180 idle probes and airborne preservation passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(geometry);
                GameClient.FindSelf=previous;WarpFlow.Instance=oldFlow;Physics.IgnoreLayerCollision(0,2,water);Physics.IgnoreLayerCollision(0,30,preview);
            }
        }
    }
}
