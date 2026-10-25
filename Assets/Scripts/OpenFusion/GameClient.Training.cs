using System;
using System.IO;
using System.Threading.Tasks;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion
{
    public sealed partial class GameClient
    {
        async Task RunTraining(CharInfo character,LoadingScreen loading)
        {
            StartupStage(loading,"LOADING TECH SQUARE TRAINING");
            await Bootstrap.PrepareTutorialAsync(character.Appearance,Tables.Xdt);
            await Bootstrap.LoadWorldAsync(TutorialTraining.StartPosition);
            var local = new GameSession(Stream.Null,new PacketCrypto());
            var training = gameObject.AddComponent<TutorialTraining>();
            // Activate the packet firewall before any world component can send.
            var completed = training.Run(this,local.Dispatcher);
            Bootstrap.StartEntities(local.Dispatcher,Tables.Xdt,Tables.HNpc);
            int hp=HudData.DefaultMaxHp(1);
            foreach(var row in Tables.Xdt.m_pAvatarTable.m_pAvatarGrowData)if(row!=null&&row.m_iLevel==1)hp=row.m_iMaxHP;
            var packet=TutorialTraining.InitialPacket(character.Appearance,hp);
            var body=new byte[packet.Length-4];Buffer.BlockCopy(packet,4,body,0,body.Length);
            Shard_=new EnteredShard{Session=local,Enter=WorldPackets.ParseEnterSucc(body)};
            training.SetSnapshot(Shard_.Enter);
            shardAlive=true;local.Dispatcher.Dispatch(packet);
            camInit=true;camYaw=90;camPitch=CamDefaultPitch;camDist=CamDefaultDist;
            if(loading!=null)loading.Hide();
            if(!await completed){ReturnToLauncher();return;}
            // Only this boundary can change persistent tutorial state. Practice
            // inventory, health, quests and Nanos are discarded by scene reload.
            StartupStage(loading,"FINISHING TRAINING");
            DebugWalker=false;
            await Login.SaveTutorFlagAsync(character.Appearance.Uid,1);
            tutorialCompletionPending=true;
            resumePending=true;resumeUid=lastCharUid=character.Appearance.Uid;
            shardAlive=false;local.Dispose();Login.Dispose();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
