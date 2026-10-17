using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFusion.Net
{
    /// Trama: [uint32 LE tamaño (incluye tipo de 4 B)] + datos cifrados.
    public sealed class FramedStream
    {
        readonly Stream _s; readonly PacketCrypto _c; uint seq;   // contador de envío (m_PacketQueue del original)
        public FramedStream(Stream s, PacketCrypto c) { _s = s; _c = c; }

        /// payload = tipo(4 B LE) + cuerpo. Estampa el tipo con checksum y cifra.
        public async Task SendAsync(byte[] payload, CancellationToken ct = default)
        {
            var buf = (byte[])payload.Clone();
            // csSocketData.InitPacket: el contador de paquetes de la conexión (0..0xFFF, empieza en 0 al conectar) va en los bits 12..23 del tipo; csSocket.Send lo incrementa tras escribir.
            BitConverter.GetBytes((BitConverter.ToUInt32(buf, 0) & 0xFF000FFFu) | ((seq & 0xFFFu) << 12)).CopyTo(buf, 0);
            uint sum = PacketCrypto.DataSum(buf, buf.Length);
            uint type = PacketCrypto.StampType(BitConverter.ToUInt32(buf, 0), sum);
            BitConverter.GetBytes(type).CopyTo(buf, 0);
            _c.Encrypt(buf, buf.Length);
            var frame = new byte[4 + buf.Length];
            BitConverter.GetBytes((uint)buf.Length).CopyTo(frame, 0);
            Buffer.BlockCopy(buf, 0, frame, 4, buf.Length);
            await _s.WriteAsync(frame, 0, frame.Length, ct);
            await _s.FlushAsync(ct);
            seq = (seq + 1) & 0xFFFu;
        }

        public async Task<byte[]> ReceiveAsync(CancellationToken ct = default)
        {
            var h = new byte[4]; await ReadFull(h, ct);
            uint n = BitConverter.ToUInt32(h, 0);
            if (n < 4 || n > 1 << 20) throw new InvalidDataException("tamaño de paquete inválido: " + n);
            var d = new byte[n]; await ReadFull(d, ct);
            _c.Decrypt(d, d.Length);
            return d;
        }

        async Task ReadFull(byte[] b, CancellationToken ct)
        {
            int o = 0;
            while (o < b.Length)
            {
                int r = await _s.ReadAsync(b, o, b.Length - o, ct);
                if (r == 0) throw new EndOfStreamException();
                o += r;
            }
        }
    }
}
