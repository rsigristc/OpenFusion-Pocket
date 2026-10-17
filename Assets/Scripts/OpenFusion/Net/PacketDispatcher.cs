// Despachador de paquetes del game server: decodifica el cuerpo y llama al evento correspondiente. Puro; sin Unity.
// Se llama desde el hilo de red: quien se suscriba debe pasar el trabajo al hilo principal (ver WorldEntities, que usa una cola).
using System;
using System.Collections.Generic;

namespace OpenFusion.Net
{
    public sealed class PacketDispatcher
    {
        public event Action<EnterSucc> SelfEnter; public event Action<byte[]> SelfEnterRaw;   // cuerpo crudo de ENTER_SUCC (sin los 4 bytes del tipo), para el HUD
        public event Action<PcAppearance> PcEnter;            // PC_NEW y cada elemento de PC_AROUND
        public event Action<int> PcExit;                       // PC_EXIT y AROUND_DEL_PC
        public event Action<PcMove> PcMove; public event Action<PcStop> PcStop; public event Action<PcJump> PcJump;
        public event Action<NpcAppearance> NpcEnter;           // NPC_ENTER, NPC_NEW y cada elemento de NPC_AROUND
        public event Action<int> NpcExit;                      // NPC_EXIT y AROUND_DEL_NPC
        public event Action<NpcMove> NpcMove;
        public event Action<int, int, int> Teleport;           // REP_PC_GOTO_SUCC (x, y, z del servidor)
        public event Action<byte[]> WarpUseNpcSuccRaw;        // REP_PC_WARP_USE_NPC_SUCC cuerpo completo (36 B: x,y,z,eIL,slot,Item,iCandy)
        public event Action WarpUseNpcSucc;                    // REP_PC_WARP_USE_NPC_SUCC (sin datos: ademas de Teleport)
        public event Action<int> WarpFail;                     // REP_PC_WARP_USE_NPC_FAIL (código de error)
        public event Action<int> LiveCheck;                    // FE2CL_REQ_LIVE_CHECK: hay que contestar con CL2FE_REP_LIVE_CHECK
        public event Action<int> EnterFail;                    // REP_PC_ENTER_FAIL (código de error)
        public event Action<uint, byte[]> Unhandled;           // tipo (sin checksum) y cuerpo
        public event Action<uint, Exception> Malformed;        // paquete conocido pero ilegible: el servidor no debe poder tumbar al cliente
        public event Action<uint, byte[]> Raw;                 // CADA paquete (tipo sin checksum + cuerpo), tambien los que Route() consume, ANTES de despacharlo. Solo lectura (el cuerpo se comparte). Hilo de red. Lo usa RemoteCombatAnim.

        public int Handled, UnhandledCount, MalformedCount;

        /// packet = tipo (4 B, con checksum) + cuerpo, ya descifrado (lo que devuelve FramedStream.ReceiveAsync).
        public void Dispatch(byte[] packet)
        {
            if (packet == null || packet.Length < 4) return;
            uint type = PacketIds.Unstamp(BitConverter.ToUInt32(packet, 0));
            var body = new byte[packet.Length - 4]; Buffer.BlockCopy(packet, 4, body, 0, body.Length);
            try { Raw?.Invoke(type, body); } catch (Exception) { }   // un suscriptor roto no debe tumbar el despacho
            try { if (Route(type, body)) Handled++; else { UnhandledCount++; Unhandled?.Invoke(type, body); } }
            catch (Exception e) when (e is ArgumentException || e is IndexOutOfRangeException) { MalformedCount++; Malformed?.Invoke(type, e); }
        }

        bool Route(uint type, byte[] b)
        {
            switch (type)
            {
                case PacketIds.P_FE2CL_REQ_LIVE_CHECK: LiveCheck?.Invoke(b.Length >= 4 ? BitConverter.ToInt32(b, 0) : 0); return true;
                case PacketIds.P_FE2CL_REP_PC_ENTER_FAIL: if (b.Length < 4) throw new ArgumentException("PC_ENTER_FAIL"); EnterFail?.Invoke(BitConverter.ToInt32(b, 0)); return true;
                case PacketIds.P_FE2CL_REP_PC_ENTER_SUCC: SelfEnter?.Invoke(WorldPackets.ParseEnterSucc(b)); SelfEnterRaw?.Invoke(b); return true;
                case PacketIds.P_FE2CL_PC_NEW: PcEnter?.Invoke(PcAppearance.ParseAppearance(b)); return true;
                case PacketIds.P_FE2CL_PC_AROUND: foreach (var a in WorldPackets.ParsePcAround(b)) PcEnter?.Invoke(a); return true;
                case PacketIds.P_FE2CL_PC_EXIT: if (b.Length < 4) throw new ArgumentException("PC_EXIT"); PcExit?.Invoke(BitConverter.ToInt32(b, 0)); return true;
                case PacketIds.P_FE2CL_AROUND_DEL_PC: foreach (var id in WorldPackets.ParseIdList(b, "AROUND_DEL_PC")) PcExit?.Invoke(id); return true;
                case PacketIds.P_FE2CL_PC_MOVE: PcMove?.Invoke(WorldPackets.ParsePcMove(b)); return true;
                case PacketIds.P_FE2CL_PC_STOP: PcStop?.Invoke(WorldPackets.ParsePcStop(b)); return true;
                case PacketIds.P_FE2CL_PC_JUMP: PcJump?.Invoke(WorldPackets.ParsePcJump(b)); return true;
                case PacketIds.P_FE2CL_NPC_ENTER:
                case PacketIds.P_FE2CL_NPC_NEW: NpcEnter?.Invoke(WorldPackets.ParseNpc(b)); return true;
                case PacketIds.P_FE2CL_NPC_AROUND: foreach (var n in WorldPackets.ParseNpcAround(b)) NpcEnter?.Invoke(n); return true;
                case PacketIds.P_FE2CL_NPC_EXIT: if (b.Length < 4) throw new ArgumentException("NPC_EXIT"); NpcExit?.Invoke(BitConverter.ToInt32(b, 0)); return true;
                case PacketIds.P_FE2CL_AROUND_DEL_NPC: foreach (var id in WorldPackets.ParseIdList(b, "AROUND_DEL_NPC")) NpcExit?.Invoke(id); return true;
                case PacketIds.P_FE2CL_NPC_MOVE: NpcMove?.Invoke(WorldPackets.ParseNpcMove(b)); return true;
                case PacketIds.P_FE2CL_REP_PC_WARP_USE_NPC_SUCC: if (b.Length < 12) throw new ArgumentException("WARP_USE_NPC_SUCC"); WarpUseNpcSucc?.Invoke(); WarpUseNpcSuccRaw?.Invoke(b); Teleport?.Invoke(BitConverter.ToInt32(b, 0), BitConverter.ToInt32(b, 4), BitConverter.ToInt32(b, 8)); return true;
                case PacketIds.P_FE2CL_REP_PC_WARP_USE_NPC_FAIL: WarpFail?.Invoke(b.Length >= 4 ? BitConverter.ToInt32(b, 0) : 0); return true;
                case PacketIds.P_FE2CL_REP_PC_GOTO_SUCC: if (b.Length < 12) throw new ArgumentException("PC_GOTO_SUCC"); Teleport?.Invoke(BitConverter.ToInt32(b, 0), BitConverter.ToInt32(b, 4), BitConverter.ToInt32(b, 8)); return true;
                default: return false;
            }
        }
    }
}
