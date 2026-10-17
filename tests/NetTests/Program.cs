using System; using System.IO; using OpenFusion.Net;
var rng = new Random(1); int fails = 0;
Console.WriteLine($"DefaultKey=0x{PacketCrypto.DefaultKey:X16}");
for (int size = 4; size < 600; size++)
for (int gs = 0; gs < 2; gs++) {
  var c = new PacketCrypto(); c.FEKey = c.EKey = 0x1122334455667788UL * 3;
  var d = new byte[size]; rng.NextBytes(d);
  BitConverter.GetBytes(gs == 1 ? 0x13000001u : 0x12000001u).CopyTo(d, 0);
  var o = (byte[])d.Clone();
  c.Encrypt(d, size);              // fija GameServer segun tipo
  c.Decrypt(d, size);
  for (int i = 0; i < size; i++) if (d[i] != o[i]) { fails++; Console.WriteLine($"FAIL size={size} gs={gs}"); break; }
}
Console.WriteLine(fails == 0 ? "roundtrip OK (596 tamaños x 2 claves)" : $"{fails} fallos");
// trama completa por MemoryStream
var ms = new MemoryStream(); var cc = new PacketCrypto();
var fs = new FramedStream(ms, cc);
var p = new byte[40]; rng.NextBytes(p); BitConverter.GetBytes(301989889u).CopyTo(p, 0);
fs.SendAsync(p).Wait(); ms.Position = 0;
var q = new FramedStream(ms, cc).ReceiveAsync().Result;
uint t = BitConverter.ToUInt32(q, 0);
Console.WriteLine($"frame len={ms.Length} tipo recibido=0x{t:X8} (low12=0x{t&0xFFF:X}, cat=0x{t>>24:X2}) cuerpo igual={q.AsSpan(4).SequenceEqual(p.AsSpan(4))}");
if (!q.AsSpan(4).SequenceEqual(p.AsSpan(4))) fails++;
return fails;
