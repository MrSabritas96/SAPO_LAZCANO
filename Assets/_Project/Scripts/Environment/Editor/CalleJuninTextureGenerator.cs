#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimuladorBloqueos.EditorTools
{
    /// <summary>
    /// Generador procedural de texturas urbanas, arquitectónicas e institucionales para la Calle Junín y Base UTOP.
    /// Incluye texturas de alta definición para:
    /// - Asfalto, losetas paceñas, piedra almohadillada, estuco, ladrillo y letrero fachada UTOP.
    /// - Cuadro oficial del Presidente, Bandera de Bolivia, Bandera Wiphala y madera fina de caoba (Oficina del Capitán).
    /// - Escudo dorado/negro de la Armería UTOP, cajas militares de munición y emblema circular del Patio de Honor.
    /// </summary>
    public static class CalleJuninTextureGenerator
    {
        private const string TexFolder = "Assets/_Project/Art/Textures/CalleJunin";

        public static void EnsureTexturesGenerated()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Textures"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Textures");
            if (!AssetDatabase.IsValidFolder(TexFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Textures", "CalleJunin");

            GenerateAsphaltTexture($"{TexFolder}/Tex_Asphalt_Road.png");
            GenerateSidewalkTexture($"{TexFolder}/Tex_Sidewalk_Tiles.png");
            GenerateRusticatedStoneTexture($"{TexFolder}/Tex_Rusticated_Stone.png");
            GenerateStuccoTexture($"{TexFolder}/Tex_Stucco_Detail.png");
            GenerateUTOPBannerTexture($"{TexFolder}/Tex_UTOP_Banner.png");
            GenerateBrickHillsideTexture($"{TexFolder}/Tex_Brick_LaPaz.png");
            GenerateBoliviaFlagTexture($"{TexFolder}/Tex_Flag_Bolivia.png");
            GenerateWiphalaFlagTexture($"{TexFolder}/Tex_Flag_Wiphala.png");
            GeneratePresidentPortraitTexture($"{TexFolder}/Tex_President_Portrait.png");
            GenerateArmoryShieldTexture($"{TexFolder}/Tex_UTOP_Armory_Shield.png");
            GeneratePatioEmblemTexture($"{TexFolder}/Tex_UTOP_Floor_Emblem.png");
            GenerateAmmoCrateTexture($"{TexFolder}/Tex_Ammo_Crate.png");
            GenerateFineWoodTexture($"{TexFolder}/Tex_Fine_Wood.png");
            GenerateBathroomTileTexture($"{TexFolder}/Tex_Bathroom_Tile.png");

            AssetDatabase.Refresh();
        }

        private static void GenerateAsphaltTexture(string path)
        {
            int w = 512, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    float ny = y / (float)h;
                    float grain = Mathf.PerlinNoise(nx * 90f, ny * 90f) * 0.08f
                                + Mathf.PerlinNoise(nx * 240f, ny * 240f) * 0.05f;
                    float macro = Mathf.PerlinNoise(nx * 8f, ny * 8f) * 0.06f;
                    float track1 = Mathf.Exp(-Mathf.Pow((nx - 0.32f) * 12f, 2f)) * 0.04f;
                    float track2 = Mathf.Exp(-Mathf.Pow((nx - 0.68f) * 12f, 2f)) * 0.04f;
                    float v = Mathf.Clamp01(0.24f + grain + macro - track1 - track2);
                    tex.SetPixel(x, y, new Color(v, v, v * 1.03f, 1f));
                }
            }
            SaveTexture(tex, path, wrapRepeat: true);
        }

        private static void GenerateSidewalkTexture(string path)
        {
            int w = 512, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int gx = x % 128;
                    int gy = y % 128;
                    int subX = x % 32;
                    int subY = y % 32;
                    bool mainJoint = (gx < 5 || gy < 5);
                    bool groove = (subX < 2 || subY < 2);
                    float noise = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.08f;
                    float val = 0.56f + noise;
                    if (mainJoint) val -= 0.22f;
                    else if (groove) val -= 0.08f;
                    tex.SetPixel(x, y, new Color(val, val * 1.01f, val * 1.03f, 1f));
                }
            }
            SaveTexture(tex, path, wrapRepeat: true);
        }

        private static void GenerateRusticatedStoneTexture(string path)
        {
            int w = 512, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            int rows = 8;
            int rowH = h / rows;

            for (int y = 0; y < h; y++)
            {
                int r = y / rowH;
                int ry = y % rowH;
                int offset = (r % 2 == 0) ? 0 : 64;
                for (int x = 0; x < w; x++)
                {
                    int rx = (x + offset) % 128;
                    bool hChannel = (ry < 6 || ry > rowH - 4);
                    bool vChannel = (rx < 6 || rx > 124);
                    bool bevel = (ry < 12 || ry > rowH - 10 || rx < 12 || rx > 118);
                    float stoneNoise = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.10f
                                     + Mathf.PerlinNoise(x * 0.45f, y * 0.45f) * 0.06f;
                    float baseVal = 0.72f + stoneNoise;
                    if (hChannel || vChannel) baseVal = 0.35f + stoneNoise * 0.4f;
                    else if (bevel) baseVal = 0.58f + stoneNoise * 0.6f;
                    tex.SetPixel(x, y, new Color(baseVal, baseVal * 0.96f, baseVal * 0.90f, 1f));
                }
            }
            SaveTexture(tex, path, wrapRepeat: true);
        }

        private static void GenerateStuccoTexture(string path)
        {
            int w = 512, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.10f;
                    float n2 = Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 0.06f;
                    float streak = Mathf.PerlinNoise(x * 0.12f, y * 0.015f) * 0.07f;
                    float v = Mathf.Clamp01(0.84f + n1 + n2 - streak);
                    tex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }
            SaveTexture(tex, path, wrapRepeat: true);
        }

        private static void GenerateUTOPBannerTexture(string path)
        {
            int w = 1024, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color greenBg = new Color(0.18f, 0.38f, 0.26f, 1f);
            Color lightGreen = new Color(0.24f, 0.48f, 0.34f, 1f);
            Color borderWhite = new Color(0.88f, 0.90f, 0.88f, 1f);
            Color gold = new Color(0.88f, 0.74f, 0.25f, 1f);
            Color darkEmblem = new Color(0.10f, 0.18f, 0.12f, 1f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    Color c = Color.Lerp(greenBg, lightGreen, ny * 0.5f);
                    if (x < 14 || x > w - 14 || y < 14 || y > h - 14) c = borderWhite;
                    else if (x < 22 || x > w - 22 || y < 22 || y > h - 22) c = darkEmblem;

                    float dxR = (nx - 0.86f) * (w / (float)h);
                    float dyR = (ny - 0.50f);
                    float distR = Mathf.Sqrt(dxR * dxR + dyR * dyR);
                    if (distR < 0.34f)
                    {
                        if (distR > 0.28f) c = borderWhite;
                        else if (distR > 0.22f) c = darkEmblem;
                        else
                        {
                            c = new Color(0.22f, 0.44f, 0.30f);
                            if (Mathf.Abs(dxR - dyR * 0.7f) < 0.03f || Mathf.Abs(dxR + dyR * 0.7f) < 0.03f)
                                c = borderWhite;
                        }
                    }

                    if (nx > 0.14f && nx < 0.72f)
                    {
                        float centerV = 0.43f;
                        float distFromCenter = Mathf.Abs(nx - centerV);
                        float vY = 0.26f + distFromCenter * 1.1f;
                        if (Mathf.Abs(ny - vY) < 0.12f && ((int)(nx * 48) % 2 == 0))
                        {
                            if (ny > vY + 0.04f) c = new Color(0.75f, 0.18f, 0.15f);
                            else if (ny > vY - 0.04f) c = borderWhite;
                            else c = darkEmblem;
                        }
                        if (ny > 0.72f && ny < 0.80f && nx > 0.24f && nx < 0.62f && (x % 6 < 4))
                            c = borderWhite;
                        if (ny > 0.60f && ny < 0.67f && nx > 0.18f && nx < 0.68f && (x % 5 < 3))
                            c = gold;
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            SaveTexture(tex, path, wrapRepeat: false);
        }

        private static void GenerateBrickHillsideTexture(string path)
        {
            int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool window = (x % 32 > 8 && x % 32 < 22) && (y % 32 > 10 && y % 32 < 24);
                    float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.1f;
                    Color c = window ? new Color(0.15f, 0.20f, 0.25f) : new Color(0.62f + n, 0.34f + n * 0.5f, 0.22f + n * 0.3f);
                    tex.SetPixel(x, y, c);
                }
            }
            SaveTexture(tex, path, wrapRepeat: true);
        }

        private static void GenerateBoliviaFlagTexture(string path)
        {
            int w = 512, h = 340;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color red = new Color(0.82f, 0.12f, 0.14f);
            Color yellow = new Color(0.96f, 0.82f, 0.08f);
            Color green = new Color(0.04f, 0.48f, 0.20f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                Color baseCol = ny > 0.666f ? red : (ny > 0.333f ? yellow : green);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    float fold = Mathf.Sin(nx * 18f) * 0.05f;
                    Color c = new Color(
                        Mathf.Clamp01(baseCol.r + fold),
                        Mathf.Clamp01(baseCol.g + fold),
                        Mathf.Clamp01(baseCol.b + fold), 1f);

                    float dx = (nx - 0.5f) * 2.4f;
                    float dy = (ny - 0.5f) * 2.4f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < 0.26f)
                    {
                        if (dist > 0.21f) c = new Color(0.75f, 0.55f, 0.12f);
                        else if (dy > 0.02f) c = new Color(0.35f, 0.68f, 0.90f);
                        else c = new Color(0.68f, 0.38f, 0.18f);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            SaveTexture(tex, path, wrapRepeat: false);
        }

        private static void GenerateWiphalaFlagTexture(string path)
        {
            int w = 512, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color[] colors = new Color[]
            {
                new Color(0.95f, 0.95f, 0.96f),
                new Color(0.96f, 0.84f, 0.06f),
                new Color(0.94f, 0.45f, 0.06f),
                new Color(0.84f, 0.10f, 0.14f),
                new Color(0.44f, 0.14f, 0.56f),
                new Color(0.10f, 0.32f, 0.78f),
                new Color(0.08f, 0.56f, 0.22f)
            };

            for (int y = 0; y < h; y++)
            {
                int row = Mathf.Clamp((int)((1f - (y / (float)h)) * 7f), 0, 6);
                for (int x = 0; x < w; x++)
                {
                    int col = Mathf.Clamp((int)((x / (float)w) * 7f), 0, 6);
                    int idx = (col - row + 7) % 7;
                    Color baseCol = colors[idx];
                    int rx = x % (w / 7);
                    int ry = y % (h / 7);
                    float seam = (rx < 2 || ry < 2) ? -0.06f : 0f;
                    tex.SetPixel(x, y, new Color(
                        Mathf.Clamp01(baseCol.r + seam),
                        Mathf.Clamp01(baseCol.g + seam),
                        Mathf.Clamp01(baseCol.b + seam), 1f));
                }
            }
            SaveTexture(tex, path, wrapRepeat: false);
        }

        private static void GeneratePresidentPortraitTexture(string path)
        {
            int w = 512, h = 640;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;

                    if (nx < 0.12f || nx > 0.88f || ny < 0.12f || ny > 0.88f)
                    {
                        bool innerGold = (nx > 0.105f && nx < 0.895f && ny > 0.105f && ny < 0.895f) &&
                                         (nx < 0.12f || nx > 0.88f || ny < 0.12f || ny > 0.88f);
                        tex.SetPixel(x, y, innerGold ? new Color(0.65f, 0.50f, 0.22f) : new Color(0.92f, 0.90f, 0.84f));
                        continue;
                    }

                    Color c = new Color(0.86f, 0.85f, 0.80f);
                    if (nx < 0.28f)
                    {
                        float flagStripe = (nx - 0.12f) / 0.16f + (ny - 0.5f) * 0.2f;
                        if (flagStripe < 0.35f) c = new Color(0.78f, 0.14f, 0.14f);
                        else if (flagStripe < 0.68f) c = new Color(0.92f, 0.78f, 0.12f);
                        else c = new Color(0.08f, 0.46f, 0.20f);
                    }

                    float dx = nx - 0.52f;
                    if (ny < 0.52f && Mathf.Abs(dx) < 0.28f - (ny - 0.12f) * 0.15f)
                    {
                        c = new Color(0.12f, 0.15f, 0.22f);
                        if (Mathf.Abs(dx) < (ny - 0.22f) * 0.32f && ny > 0.24f)
                        {
                            c = new Color(0.94f, 0.95f, 0.96f);
                            if (Mathf.Abs(dx) < 0.025f && ny < 0.48f)
                                c = new Color(0.18f, 0.24f, 0.38f);
                        }

                        float sashD = (nx - 0.36f) * 0.75f + (ny - 0.48f) * 0.65f;
                        if (sashD > -0.06f && sashD < 0.06f)
                        {
                            if (sashD < -0.02f) c = new Color(0.06f, 0.50f, 0.20f);
                            else if (sashD < 0.02f) c = new Color(0.95f, 0.82f, 0.10f);
                            else c = new Color(0.84f, 0.12f, 0.14f);
                        }
                    }

                    if (ny >= 0.48f && ny < 0.56f && Mathf.Abs(dx) < 0.055f)
                        c = new Color(0.82f, 0.64f, 0.52f);

                    float headDx = (nx - 0.52f) / 0.10f;
                    float headDy = (ny - 0.65f) / 0.13f;
                    if (headDx * headDx + headDy * headDy < 1.0f)
                    {
                        c = new Color(0.85f, 0.67f, 0.55f);
                        if (ny > 0.70f || (ny > 0.64f && Mathf.Abs(headDx) > 0.78f))
                            c = new Color(0.14f, 0.13f, 0.12f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            SaveTexture(tex, path, wrapRepeat: false);
        }

        private static void GenerateArmoryShieldTexture(string path)
        {
            int w = 512, h = 640;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color darkBg = new Color(0.08f, 0.09f, 0.10f);
            Color gold = new Color(0.86f, 0.68f, 0.20f);
            Color greenUTOP = new Color(0.10f, 0.32f, 0.18f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    float dx = Mathf.Abs(nx - 0.5f) * 2f;

                    float maxW = ny > 0.45f ? 0.82f : Mathf.Pow(Mathf.Max(0f, (ny - 0.08f) / 0.37f), 0.65f) * 0.82f;
                    bool insideShield = ny > 0.08f && ny < 0.92f && dx < maxW;

                    if (!insideShield)
                    {
                        tex.SetPixel(x, y, darkBg);
                        continue;
                    }

                    bool isGoldBorder = dx > maxW - 0.055f || ny > 0.885f || ny < 0.115f;
                    Color c = isGoldBorder ? gold : new Color(0.09f, 0.11f, 0.10f);

                    if (!isGoldBorder && ny > 0.72f && ny < 0.84f && dx < 0.62f)
                    {
                        float localX = ((nx - 0.19f) % 0.16f) / 0.16f;
                        if (localX > 0.15f && localX < 0.82f)
                            c = gold;
                    }
                    else if (!isGoldBorder && ny > 0.58f && ny < 0.68f && dx < 0.68f)
                    {
                        if (((int)(ny * 120f)) % 3 == 0 && ((int)(nx * 90f)) % 2 == 0)
                            c = new Color(0.92f, 0.88f, 0.72f);
                    }
                    else if (!isGoldBorder && ny > 0.18f && ny < 0.55f)
                    {
                        float edx = (nx - 0.5f) * 2.8f;
                        float edy = (ny - 0.36f) * 2.8f;
                        float r = Mathf.Sqrt(edx * edx + edy * edy);
                        if (r > 0.38f && r < 0.52f && edy < 0.25f)
                            c = gold;
                        else if (r < 0.34f)
                        {
                            if (edy > 0.10f) c = new Color(0.80f, 0.14f, 0.14f);
                            else if (edy > -0.10f) c = new Color(0.92f, 0.78f, 0.12f);
                            else c = greenUTOP;
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            SaveTexture(tex, path, wrapRepeat: false);
        }

        private static void GeneratePatioEmblemTexture(string path)
        {
            int w = 512, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color gold = new Color(0.90f, 0.74f, 0.18f, 1f);
            Color darkRing = new Color(0.12f, 0.16f, 0.14f, 1f);
            Color white = new Color(0.94f, 0.94f, 0.92f, 1f);

            for (int y = 0; y < h; y++)
            {
                float dy = (y / (float)h - 0.5f) * 2f;
                for (int x = 0; x < w; x++)
                {
                    float dx = (x / (float)w - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    if (r > 0.94f)
                    {
                        tex.SetPixel(x, y, new Color(0.50f, 0.52f, 0.53f, 1f));
                        continue;
                    }

                    Color c;
                    if (r > 0.88f) c = gold;
                    else if (r > 0.66f)
                    {
                        c = darkRing;
                        float ang = Mathf.Atan2(dy, dx);
                        if (r > 0.73f && r < 0.81f && Mathf.Sin(ang * 28f) > 0.1f)
                            c = white;
                    }
                    else if (r > 0.62f) c = gold;
                    else
                    {
                        c = white;
                        if (r > 0.42f && r < 0.54f && dy < 0.25f)
                            c = new Color(0.14f, 0.48f, 0.22f, 1f);
                        else if (r < 0.38f)
                        {
                            if (dy > 0.12f) c = new Color(0.84f, 0.14f, 0.14f, 1f);
                            else if (dy > -0.12f) c = new Color(0.95f, 0.80f, 0.10f, 1f);
                            else c = new Color(0.10f, 0.52f, 0.22f, 1f);
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            SaveTexture(tex, path, wrapRepeat: false);
        }

        private static void GenerateAmmoCrateTexture(string path)
        {
            int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color olive = new Color(0.24f, 0.29f, 0.16f);
            Color stencil = new Color(0.88f, 0.76f, 0.28f);

            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    float plank = (y % 64 < 3 || x < 10 || x > w - 10) ? -0.05f : 0f;
                    Color c = new Color(olive.r + plank, olive.g + plank, olive.b + plank, 1f);

                    if (nx > 0.15f && nx < 0.85f && ((ny > 0.55f && ny < 0.65f) || (ny > 0.35f && ny < 0.43f)))
                    {
                        if ((x / 6) % 2 == 0) c = stencil;
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            SaveTexture(tex, path, wrapRepeat: true);
        }

        private static void GenerateFineWoodTexture(string path)
        {
            int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    float ny = y / (float)h;
                    float grain = Mathf.Sin((nx * 14f + Mathf.PerlinNoise(nx * 6f, ny * 2f) * 1.8f) * Mathf.PI) * 0.04f;
                    Color c = new Color(0.38f + grain, 0.21f + grain * 0.6f, 0.11f + grain * 0.3f, 1f);
                    tex.SetPixel(x, y, c);
                }
            }
            SaveTexture(tex, path, wrapRepeat: true);
        }

        private static void GenerateBathroomTileTexture(string path)
        {
            int w = 512, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            int tileSize = 64;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int gx = x % tileSize;
                    int gy = y % tileSize;
                    bool isGrout = (gx < 3 || gy < 3 || gx > tileSize - 2 || gy > tileSize - 2);
                    if (isGrout)
                    {
                        tex.SetPixel(x, y, new Color(0.55f, 0.58f, 0.60f, 1f));
                    }
                    else
                    {
                        float nx = x / (float)w;
                        float ny = y / (float)h;
                        float marble = Mathf.Sin((nx * 10f + ny * 8f + Mathf.PerlinNoise(nx * 12f, ny * 12f) * 1.4f) * Mathf.PI) * 0.035f;
                        int tileIdx = (x / tileSize + y / tileSize) % 2;
                        float checker = (tileIdx == 0) ? 0.015f : -0.015f;
                        // Bisel sutil en el borde del azulejo
                        float bevel = (gx < 6 || gy < 6) ? 0.03f : ((gx > tileSize - 5 || gy > tileSize - 5) ? -0.03f : 0f);
                        float r = Mathf.Clamp01(0.84f + marble + checker + bevel);
                        float g = Mathf.Clamp01(0.89f + marble + checker + bevel);
                        float b = Mathf.Clamp01(0.92f + marble + checker + bevel);
                        tex.SetPixel(x, y, new Color(r, g, b, 1f));
                    }
                }
            }
            SaveTexture(tex, path, wrapRepeat: true);
        }

        private static void SaveTexture(Texture2D tex, string assetPath, bool wrapRepeat)
        {
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(assetPath, png);
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = wrapRepeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
