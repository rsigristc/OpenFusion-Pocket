// Paletas de color del avatar del jugador. Copiadas literalmente del constructor estático de UserContainer del cliente original
// (AvatarCustomizationOptions: skin 48, hair 54, eye 10; formato 0xRRGGBB). Puro; sin Unity.
// Los NPC humanoides (HNPCLook) usan bsddata (male.bsd.asset); si sus paletas difieren, hay que leerlas de ese asset.
namespace OpenFusion.World
{
    public struct Rgb { public float R, G, B; public Rgb(float r, float g, float b) { R = r; G = g; B = b; } public static Rgb FromHex(int h) => new Rgb(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f); }

    public static class ActorPalettes
    {
        public static readonly int[] SkinHex =
        {
            0x3A2719, 0x5B4B29, 0x654F3A, 0x976854, 0x8A7762, 0x978153, 0xB78168, 0xB5936E, 0xE5BA8D, 0xEDD9A0, 0xF8D6CC, 0xD3DBE0,
            0x472B20, 0x5A372B, 0x6C483A, 0x917568, 0x9E7C5F, 0xC7916F, 0xB57C43, 0xBA885D, 0xD4A57F, 0xEBBA7D, 0xFFC8BE, 0xFAE6D1,
            0x4D3A49, 0x5C4A58, 0x826979, 0x916B76, 0xAF7577, 0xDA9D9D, 0x672D22, 0x7C372E, 0xA15140, 0xB16040, 0xB47B54, 0xDDA077,
            0xFCFF38, 0xFF6A00, 0xC12D24, 0xB8E11D, 0x9FD29D, 0x51A45F, 0x5DFBFF, 0x5DBFFF, 0x237CDE, 0x8669B6, 0xF593F3, 0xF2F2F7
        };
        public static readonly int[] HairHex =
        {
            0xFEFEFE, 0x8A909C, 0x0E0E0E, 0xDB929B, 0x934931, 0xD73E09, 0xBAC7E7, 0x89139F, 0x410A9E, 0x6FCEB3, 0x0FD10A, 0x197D2A,
            0xEFE9AB, 0xEFD869, 0x0A99F6, 0xF18507, 0x7D5144, 0x502F2A, 0xF5EFE0, 0x9A8E80, 0x1E1A18, 0xC8848C, 0x7A4228, 0xB84818,
            0x8AAAC0, 0x6A4880, 0x381868, 0x5A9878, 0x789828, 0x384A18, 0xE8D898, 0xC89020, 0x2878C0, 0xC45010, 0x8A6040, 0x483020,
            0xE6FFFF, 0xC5ADBC, 0x221B2F, 0x00F0FF, 0x00A6FF, 0x0051FF, 0x7A00FF, 0xD400FF, 0xFF00C8, 0xFF0066, 0xFF2400, 0xFF6A00,
            0xFFB000, 0xFFF200, 0xC6FF00, 0x5BFF00, 0x00FF85, 0x00FFCC
        };
        public static readonly int[] EyeHex =
        {
            0xC8DC46, 0x0852CA, 0x0D8D27, 0x7F4820, 0x000000, 0x28FFE7, 0xAA28FF, 0xFF28D9, 0xFF284A, 0xFFFFFF
        };
        /// UserContainer.GetSkinColor/GetHairColor: índice fuera de rango = blanco. (El cliente pasa iSkinColor-1; un índice negativo lo trata aquí como "sin color" = null.)
        public static Rgb? Skin(int idx) => idx < 0 ? (Rgb?)null : idx >= SkinHex.Length ? new Rgb(1, 1, 1) : Rgb.FromHex(SkinHex[idx]);
        public static Rgb? Hair(int idx) => idx < 0 ? (Rgb?)null : idx >= HairHex.Length ? new Rgb(1, 1, 1) : Rgb.FromHex(HairHex[idx]);
    }
}
