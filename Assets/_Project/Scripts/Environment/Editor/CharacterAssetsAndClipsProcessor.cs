using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SimuladorBloqueos;

namespace SimuladorBloqueos.Environment.Editor
{
    /// <summary>
    /// Procesador de modelos 3D, mallas decimadas de alto rendimiento (LOD sin costuras) y clips
    /// de animación In-Place para:
    /// 1. Oficial UTOP (Jugador) + 4 poses/acciones sintetizadas sobre el esqueleto de 52 huesos:
    ///    - HumanSpUprightIdle_InPlace (Postura Erguida Natural sin cadera ladeada)
    ///    - HumanSpMilitaryAttention_InPlace (Posición de Firmes Militar: pies juntos, manos en los muslos)
    ///    - HumanSpRightPunch_InPlace (Golpe con el Puño Derecho)
    ///    - HumanSpPullChair_InPlace (Gesto de jalar la silla con la mano al sentarse)
    /// 2. Capitán UTOP (skin capitan + movimientos capitan)
    /// 3. Mineros Bloqueadores (Skin minero + Movimientos minero)
    /// 4. Escuadrón de 30 Policías UTOP en la Cuadra de Tropa 1 y Formación 3x10 en el Patio de Honor.
    /// </summary>
    public static class CharacterAssetsAndClipsProcessor
    {
        public const string OFFICER_DIR = "Assets/_Project/Art/Characters/UTOP_Officer";
        public const string OFFICER_FBX = OFFICER_DIR + "/UTOP_Officer_Animations.fbx";
        public const string OFFICER_CLIPS_DIR = OFFICER_DIR + "/ProcessedClips";

        public const string CAPTAIN_DIR = "Assets/_Project/Art/Characters/UTOP_Captain";
        public const string CAPTAIN_FBX = CAPTAIN_DIR + "/UTOP_Captain_Animations.fbx";
        public const string CAPTAIN_CLIPS_DIR = CAPTAIN_DIR + "/ProcessedClips";

        public const string MINER_DIR = "Assets/_Project/Art/Characters/Miner_Blocker";
        public const string MINER_FBX = MINER_DIR + "/Miner_Blocker_Animations.fbx";
        public const string MINER_CLIPS_DIR = MINER_DIR + "/ProcessedClips";

        public const string CHOLITA_DIR = "Assets/_Project/Art/Characters/Cholita";
        public const string CHOLITA_FBX = CHOLITA_DIR + "/Cholita_Animations.fbx";
        public const string CHOLITA_CLIPS_DIR = CHOLITA_DIR + "/ProcessedClips";

        public const string PONCHO_DIR = "Assets/_Project/Art/Characters/PonchoRojo";
        public const string PONCHO_FBX = PONCHO_DIR + "/PonchoRojo_Animations.fbx";
        public const string PONCHO_CLIPS_DIR = PONCHO_DIR + "/ProcessedClips";

        public const string PROPS_DIR = "Assets/_Project/Art/Props";

        // =========================================================================================
        // 1. DECIMACIÓN DE MALLAS SKINNED SIN COSTURAS (SEAM-PRESERVING SPATIAL GRID DECIMATOR)
        // =========================================================================================

        public static Mesh GetOrCreateDecimatedMesh(string fbxPath, string outputAssetPath, int gridDivisions)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(outputAssetPath);
            if (existing != null && existing.vertexCount > 100)
            {
                return existing;
            }

            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab == null) return null;

            SkinnedMeshRenderer smr = fbxPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr == null || smr.sharedMesh == null) return null;

            Mesh src = smr.sharedMesh;
            Mesh decimated = DecimateSkinnedMeshSeamPreserving(src, gridDivisions);
            decimated.name = System.IO.Path.GetFileNameWithoutExtension(outputAssetPath);

            EnsureFolderExists(System.IO.Path.GetDirectoryName(outputAssetPath).Replace("\\", "/"));
            AssetDatabase.CreateAsset(decimated, outputAssetPath);
            AssetDatabase.SaveAssets();
            return decimated;
        }

        private static Mesh DecimateSkinnedMeshSeamPreserving(Mesh src, int gridDiv)
        {
            Vector3[] oldV = src.vertices;
            Vector3[] oldN = src.normals;
            Vector4[] oldT = src.tangents;
            Vector2[] oldUV = src.uv;
            BoneWeight[] oldBW = src.boneWeights;
            int[] oldTris = src.triangles;
            int vCount = oldV.Length;

            Bounds b = src.bounds;
            Vector3 min = b.min - Vector3.one * 0.001f;
            Vector3 size = b.size + Vector3.one * 0.002f;

            float invX = gridDiv / Mathf.Max(0.0001f, size.x);
            float invY = gridDiv / Mathf.Max(0.0001f, size.y);
            float invZ = gridDiv / Mathf.Max(0.0001f, size.z);

            var posCellMap = new Dictionary<long, int>(vCount / 8);
            int[] vToPosCluster = new int[vCount];
            int posClusterCount = 0;

            for (int i = 0; i < vCount; i++)
            {
                Vector3 p = oldV[i];
                long gx = (long)((p.x - min.x) * invX);
                long gy = (long)((p.y - min.y) * invY);
                long gz = (long)((p.z - min.z) * invZ);
                long key = (gx * 73856093L) ^ (gy * 19349663L) ^ (gz * 83492791L);

                int cIdx;
                if (!posCellMap.TryGetValue(key, out cIdx))
                {
                    cIdx = posClusterCount++;
                    posCellMap[key] = cIdx;
                }
                vToPosCluster[i] = cIdx;
            }

            Vector3[] clusterPosSum = new Vector3[posClusterCount];
            int[] clusterCounts = new int[posClusterCount];
            int[] clusterRepVert = new int[posClusterCount];

            for (int i = 0; i < vCount; i++)
            {
                int c = vToPosCluster[i];
                clusterPosSum[c] += oldV[i];
                if (clusterCounts[c] == 0)
                    clusterRepVert[c] = i;
                clusterCounts[c]++;
            }

            Vector3[] clusterAvgPos = new Vector3[posClusterCount];
            for (int c = 0; c < posClusterCount; c++)
            {
                clusterAvgPos[c] = clusterPosSum[c] / clusterCounts[c];
            }

            var newV = new List<Vector3>(posClusterCount * 2);
            var newN = new List<Vector3>(posClusterCount * 2);
            var newT = new List<Vector4>(posClusterCount * 2);
            var newUV = new List<Vector2>(posClusterCount * 2);
            var newBW = new List<BoneWeight>(posClusterCount * 2);

            var uvCellMap = new Dictionary<long, int>(posClusterCount * 2);
            int[] remap = new int[vCount];

            bool hasNormals = (oldN != null && oldN.Length == vCount);
            bool hasTangents = (oldT != null && oldT.Length == vCount);
            bool hasUV = (oldUV != null && oldUV.Length == vCount);
            bool hasBW = (oldBW != null && oldBW.Length == vCount);

            for (int i = 0; i < vCount; i++)
            {
                int c = vToPosCluster[i];
                Vector2 uv = hasUV ? oldUV[i] : Vector2.zero;
                long ux = (long)(uv.x * 160f);
                long uy = (long)(uv.y * 160f);
                long key = ((long)c << 32) ^ ((ux & 0xFFFFL) << 16) ^ (uy & 0xFFFFL);

                int newIdx;
                if (!uvCellMap.TryGetValue(key, out newIdx))
                {
                    newIdx = newV.Count;
                    uvCellMap[key] = newIdx;

                    newV.Add(clusterAvgPos[c]);
                    if (hasNormals) newN.Add(oldN[i]);
                    if (hasTangents) newT.Add(oldT[i]);
                    if (hasUV) newUV.Add(uv);
                    if (hasBW) newBW.Add(oldBW[clusterRepVert[c]]);
                }
                remap[i] = newIdx;
            }

            var newTris = new List<int>(oldTris.Length / 4);
            for (int i = 0; i < oldTris.Length; i += 3)
            {
                int a = remap[oldTris[i]];
                int bIdx = remap[oldTris[i + 1]];
                int c = remap[oldTris[i + 2]];
                if (a != bIdx && bIdx != c && a != c)
                {
                    newTris.Add(a);
                    newTris.Add(bIdx);
                    newTris.Add(c);
                }
            }

            Mesh m = new Mesh();
            if (newV.Count > 65000)
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = newV.ToArray();
            if (hasNormals) m.normals = newN.ToArray();
            if (hasTangents) m.tangents = newT.ToArray();
            if (hasUV) m.uv = newUV.ToArray();
            if (hasBW) m.boneWeights = newBW.ToArray();
            m.bindposes = src.bindposes;
            m.triangles = newTris.ToArray();
            m.RecalculateBounds();
            return m;
        }

        // =========================================================================================
        // 2. MATERIALES PBR URP PARA OFICIAL, CAPITÁN Y MINERO
        // =========================================================================================

        public static Material GetOrCreatePBRCharacterMaterial(string folderPath, string matName)
        {
            EnsureFolderExists(folderPath);
            string matPath = folderPath + "/" + matName + ".mat";

            ConfigurePBRTexturesInFolder(folderPath);

            Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(folderPath + "/texture_diffuse.png");
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(folderPath + "/texture_normal.png");
            Texture2D metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>(folderPath + "/texture_metallic.png");

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                mat.name = matName;
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = urpLit;
            }

            if (diffuseTex != null)
            {
                mat.SetTexture("_BaseMap", diffuseTex);
                mat.mainTexture = diffuseTex;
            }
            mat.SetColor("_BaseColor", Color.white);
            mat.color = Color.white;

            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metallicTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicTex);
            }
            mat.SetFloat("_Smoothness", 0.36f);
            mat.SetFloat("_Metallic", 0.12f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigurePBRTexturesInFolder(string folderPath)
        {
            string[] files = new string[]
            {
                "texture_diffuse.png",
                "texture_normal.png",
                "texture_metallic.png",
                "texture_roughness.png"
            };

            for (int i = 0; i < files.Length; i++)
            {
                string path = folderPath + "/" + files[i];
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                bool isNormal = files[i].Contains("normal");
                bool isLinear = files[i].Contains("metallic") || files[i].Contains("roughness");
                bool dirty = false;

                if (isNormal && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
                else if (!isNormal && ti.sRGBTexture == isLinear)
                {
                    ti.sRGBTexture = !isLinear;
                    dirty = true;
                }
                if (ti.maxTextureSize < 2048)
                {
                    ti.maxTextureSize = 2048;
                    dirty = true;
                }
                if (dirty)
                {
                    ti.SaveAndReimport();
                }
            }
        }

        // =========================================================================================
        // 3. PROCESAMIENTO DE CLIPS IN-PLACE + POSES Y ACCIONES SINTETIZADAS (ERGUIDO, FIRMES, PUÑO, SILLA)
        // =========================================================================================

        public static Dictionary<string, AnimationClip> ProcessFBXClipsToInPlace(string fbxPath, string outputFolder, HashSet<string> nonLoopingNames)
        {
            EnsureFolderExists(outputFolder);
            var dict = new Dictionary<string, AnimationClip>();

            UnityEngine.Object[] allObjects = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (allObjects == null) return dict;

            foreach (var obj in allObjects)
            {
                AnimationClip srcClip = obj as AnimationClip;
                if (srcClip == null || srcClip.name.StartsWith("__preview__"))
                    continue;

                string shortName = srcClip.name;
                int pipeIdx = shortName.IndexOf('|');
                if (pipeIdx >= 0 && pipeIdx + 1 < shortName.Length)
                    shortName = shortName.Substring(pipeIdx + 1);

                string assetPath = outputFolder + "/" + shortName + "_InPlace.anim";
                bool shouldLoop = !nonLoopingNames.Contains(shortName);

                AnimationClip processed = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (processed == null)
                {
                    processed = ConvertClipToInPlace(srcClip, shortName + "_InPlace", shouldLoop);
                    AssetDatabase.CreateAsset(processed, assetPath);
                }
                else
                {
                    AnimationClip fresh = ConvertClipToInPlace(srcClip, shortName + "_InPlace", shouldLoop);
                    EditorUtility.CopySerialized(fresh, processed);
                    EditorUtility.SetDirty(processed);
                }

                dict[shortName] = processed;
            }

            return dict;
        }

        private static AnimationClip ConvertClipToInPlace(AnimationClip srcClip, string newName, bool loopTime)
        {
            AnimationClip dst = new AnimationClip();
            EditorUtility.CopySerialized(srcClip, dst);
            dst.name = newName;

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(dst);
            foreach (var b in bindings)
            {
                if (b.path == "Armature/Hips" || b.path.EndsWith("/Hips") || b.path == "Hips")
                {
                    // Bloquear traslación horizontal (X e Y local del Armature) conservando Z local (altura vertical)
                    if (b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.y")
                    {
                        AnimationCurve flatZero = new AnimationCurve(
                            new Keyframe(0f, 0f),
                            new Keyframe(Mathf.Max(0.05f, dst.length), 0f));
                        AnimationUtility.SetEditorCurve(dst, b, flatZero);
                    }
                }
            }

            SerializedObject so = new SerializedObject(dst);
            SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                SerializedProperty loopProp = settings.FindPropertyRelative("m_LoopTime");
                if (loopProp != null) loopProp.boolValue = loopTime;

                SerializedProperty loopBlendProp = settings.FindPropertyRelative("m_LoopBlend");
                if (loopBlendProp != null) loopBlendProp.boolValue = loopTime;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return dst;
        }

        /// <summary>
        /// Genera los 4 clips especializados solicitados para el Oficial y la Tropa UTOP:
        /// 1. HumanSpUprightIdle_InPlace (Postura Erguida Natural sin cadera ladeada)
        /// 2. HumanSpMilitaryAttention_InPlace (Posición de Firmes Militar: pies juntos, manos pegadas a los muslos)
        /// 3. HumanSpRightPunch_InPlace (Golpe con el Puño Derecho)
        /// 4. HumanSpPullChair_InPlace (Gesto con la mano para jalar la silla antes de sentarse)
        /// </summary>
        public static void GenerateSynthesizedUTOPClips(AnimationClip baseSaluteClip, string outputFolder, Dictionary<string, AnimationClip> targetDict)
        {
            if (baseSaluteClip == null) return;
            EnsureFolderExists(outputFolder);

            // 1. Postura Erguida Natural (HumanSpUprightIdle_InPlace)
            string uprightPath = outputFolder + "/HumanSpUprightIdle_InPlace.anim";
            AnimationClip uprightClip = BuildUprightIdleClip(baseSaluteClip);
            SaveOrUpdateClipAsset(uprightClip, uprightPath);
            targetDict["HumanSpUprightIdle"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(uprightPath);

            // 2. Posición de Firmes Militar (HumanSpMilitaryAttention_InPlace)
            string attentionPath = outputFolder + "/HumanSpMilitaryAttention_InPlace.anim";
            AnimationClip attentionClip = BuildMilitaryAttentionClip(baseSaluteClip);
            SaveOrUpdateClipAsset(attentionClip, attentionPath);
            targetDict["HumanSpMilitaryAttention"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(attentionPath);

            // 3. Golpe con el Puño Derecho (HumanSpRightPunch_InPlace)
            string punchPath = outputFolder + "/HumanSpRightPunch_InPlace.anim";
            AnimationClip punchClip = BuildRightPunchClip(baseSaluteClip);
            SaveOrUpdateClipAsset(punchClip, punchPath);
            targetDict["HumanSpRightPunch"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(punchPath);

            // 4. Jalar Silla con la Mano (HumanSpPullChair_InPlace)
            string pullChairPath = outputFolder + "/HumanSpPullChair_InPlace.anim";
            AnimationClip pullClip = BuildPullChairClip(baseSaluteClip);
            SaveOrUpdateClipAsset(pullClip, pullChairPath);
            targetDict["HumanSpPullChair"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(pullChairPath);
        }

        private static void SaveOrUpdateClipAsset(AnimationClip clip, string assetPath)
        {
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing == null)
            {
                AnimationClip toSave = clip;
                if (AssetDatabase.Contains(clip))
                {
                    toSave = new AnimationClip { name = clip.name };
                    EditorUtility.CopySerialized(clip, toSave);
                }
                AssetDatabase.CreateAsset(toSave, assetPath);
            }
            else
            {
                if (existing != clip)
                {
                    EditorUtility.CopySerialized(clip, existing);
                    EditorUtility.SetDirty(existing);
                }
            }
        }

        private static AnimationClip CreateBasePoseClipFromSaluteFrame0(AnimationClip saluteClip, string name, float duration, bool loop)
        {
            AnimationClip dst = new AnimationClip();
            dst.name = name;

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(saluteClip);
            foreach (var b in bindings)
            {
                AnimationCurve srcCurve = AnimationUtility.GetEditorCurve(saluteClip, b);
                if (srcCurve == null || srcCurve.length == 0) continue;

                float val0 = srcCurve.Evaluate(0f);
                if (b.path.EndsWith("Hips") && (b.propertyName == "m_LocalPosition.x" || b.propertyName == "m_LocalPosition.y"))
                {
                    val0 = 0f;
                }

                AnimationCurve constantCurve = new AnimationCurve(
                    new Keyframe(0f, val0),
                    new Keyframe(duration, val0));
                AnimationUtility.SetEditorCurve(dst, b, constantCurve);
            }

            SerializedObject so = new SerializedObject(dst);
            SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                var lp = settings.FindPropertyRelative("m_LoopTime");
                if (lp != null) lp.boolValue = loop;
                var lb = settings.FindPropertyRelative("m_LoopBlend");
                if (lb != null) lb.boolValue = loop;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return dst;
        }

        private static void SetBoneConstantQuat(AnimationClip clip, string bonePath, Quaternion q, float duration)
        {
            q = q.normalized;
            SetBoneCurve(clip, bonePath, "m_LocalRotation.x", new AnimationCurve(new Keyframe(0f, q.x), new Keyframe(duration, q.x)));
            SetBoneCurve(clip, bonePath, "m_LocalRotation.y", new AnimationCurve(new Keyframe(0f, q.y), new Keyframe(duration, q.y)));
            SetBoneCurve(clip, bonePath, "m_LocalRotation.z", new AnimationCurve(new Keyframe(0f, q.z), new Keyframe(duration, q.z)));
            SetBoneCurve(clip, bonePath, "m_LocalRotation.w", new AnimationCurve(new Keyframe(0f, q.w), new Keyframe(duration, q.w)));
        }

        private static void SetBoneKeyframedQuat(AnimationClip clip, string bonePath, float[] times, Quaternion[] rots)
        {
            Keyframe[] kx = new Keyframe[times.Length];
            Keyframe[] ky = new Keyframe[times.Length];
            Keyframe[] kz = new Keyframe[times.Length];
            Keyframe[] kw = new Keyframe[times.Length];

            for (int i = 0; i < times.Length; i++)
            {
                Quaternion q = rots[i].normalized;
                kx[i] = new Keyframe(times[i], q.x);
                ky[i] = new Keyframe(times[i], q.y);
                kz[i] = new Keyframe(times[i], q.z);
                kw[i] = new Keyframe(times[i], q.w);
            }

            SetBoneCurve(clip, bonePath, "m_LocalRotation.x", new AnimationCurve(kx));
            SetBoneCurve(clip, bonePath, "m_LocalRotation.y", new AnimationCurve(ky));
            SetBoneCurve(clip, bonePath, "m_LocalRotation.z", new AnimationCurve(kz));
            SetBoneCurve(clip, bonePath, "m_LocalRotation.w", new AnimationCurve(kw));
        }

        private static void SetBoneCurve(AnimationClip clip, string path, string prop, AnimationCurve curve)
        {
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), prop);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static AnimationClip BuildUprightIdleClip(AnimationClip baseSaluteClip)
        {
            float dur = 3.2f;
            AnimationClip clip = CreateBasePoseClipFromSaluteFrame0(baseSaluteClip, "HumanSpUprightIdle_InPlace", dur, true);

            string hips = "Armature/Hips";
            string spine = hips + "/Spine";
            string spine1 = spine + "/Spine1";
            string spine2 = spine1 + "/Spine2";
            string neck = spine2 + "/Neck";
            string head = neck + "/Head";

            string lShoulder = spine2 + "/LeftShoulder";
            string lArm = lShoulder + "/LeftArm";
            string lForeArm = lArm + "/LeftForeArm";
            string lHand = lForeArm + "/LeftHand";

            string rShoulder = spine2 + "/RightShoulder";
            string rArm = rShoulder + "/RightArm";
            string rForeArm = rArm + "/RightForeArm";
            string rHand = rForeArm + "/RightHand";

            string lUpLeg = hips + "/LeftUpLeg";
            string lLeg = lUpLeg + "/LeftLeg";
            string lFoot = lLeg + "/LeftFoot";

            string rUpLeg = hips + "/RightUpLeg";
            string rLeg = rUpLeg + "/RightLeg";
            string rFoot = rLeg + "/RightFoot";

            // Espalda erguida, hombros nivelados y piernas simétricas (sin cadera ladeada)
            SetBoneConstantQuat(clip, hips, new Quaternion(0.684f, 0.000f, 0.000f, 0.730f), dur);
            SetBoneConstantQuat(clip, spine, new Quaternion(-0.025f, 0.000f, 0.000f, 0.9997f), dur);

            // Respiración suave natural en Spine1
            SetBoneKeyframedQuat(clip, spine1,
                new float[] { 0f, dur * 0.5f, dur },
                new Quaternion[]
                {
                    new Quaternion(-0.015f, 0f, 0f, 0.9999f),
                    new Quaternion(0.006f, 0f, 0f, 0.9999f),
                    new Quaternion(-0.015f, 0f, 0f, 0.9999f)
                });

            SetBoneConstantQuat(clip, spine2, new Quaternion(0.045f, 0.000f, 0.000f, 0.999f), dur);
            SetBoneConstantQuat(clip, neck, new Quaternion(0.035f, 0.000f, 0.000f, 0.9994f), dur);
            SetBoneConstantQuat(clip, head, new Quaternion(-0.020f, 0.000f, 0.000f, 0.9998f), dur);

            // Piernas erguidas a la anchura natural de los hombros (conservando los pies planos originales de Salute frame 0)
            SetBoneConstantQuat(clip, lUpLeg, new Quaternion(-0.065f, -0.045f, 0.996f, 0.015f), dur);
            SetBoneConstantQuat(clip, lLeg, new Quaternion(-0.035f, 0.000f, 0.000f, 0.9994f), dur);

            SetBoneConstantQuat(clip, rUpLeg, new Quaternion(0.065f, -0.045f, 0.996f, -0.015f), dur);
            SetBoneConstantQuat(clip, rLeg, new Quaternion(-0.035f, 0.000f, 0.000f, 0.9994f), dur);

            // Brazos relajados pero marciales a los costados
            SetBoneConstantQuat(clip, lShoulder, new Quaternion(-0.478f, 0.506f, -0.562f, -0.448f), dur);
            SetBoneConstantQuat(clip, lArm, new Quaternion(0.50f, -0.18f, -0.22f, 0.818f), dur);
            SetBoneConstantQuat(clip, lForeArm, new Quaternion(-0.02f, 0.00f, -0.14f, 0.990f), dur);
            SetBoneConstantQuat(clip, lHand, new Quaternion(-0.04f, -0.09f, 0.02f, 0.995f), dur);

            SetBoneConstantQuat(clip, rShoulder, new Quaternion(-0.478f, -0.506f, 0.562f, -0.448f), dur);
            SetBoneConstantQuat(clip, rArm, new Quaternion(0.50f, 0.18f, 0.22f, 0.818f), dur);
            SetBoneConstantQuat(clip, rForeArm, new Quaternion(-0.02f, 0.00f, 0.14f, 0.990f), dur);
            SetBoneConstantQuat(clip, rHand, new Quaternion(-0.04f, 0.09f, -0.02f, 0.995f), dur);

            return clip;
        }

        private static AnimationClip BuildMilitaryAttentionClip(AnimationClip baseSaluteClip)
        {
            float dur = 3.0f;
            AnimationClip clip = CreateBasePoseClipFromSaluteFrame0(baseSaluteClip, "HumanSpMilitaryAttention_InPlace", dur, true);

            string hips = "Armature/Hips";
            string spine = hips + "/Spine";
            string spine1 = spine + "/Spine1";
            string spine2 = spine1 + "/Spine2";
            string neck = spine2 + "/Neck";
            string head = neck + "/Head";

            string lShoulder = spine2 + "/LeftShoulder";
            string lArm = lShoulder + "/LeftArm";
            string lForeArm = lArm + "/LeftForeArm";
            string lHand = lForeArm + "/LeftHand";

            string rShoulder = spine2 + "/RightShoulder";
            string rArm = rShoulder + "/RightArm";
            string rForeArm = rArm + "/RightForeArm";
            string rHand = rForeArm + "/RightHand";

            string lUpLeg = hips + "/LeftUpLeg";
            string lLeg = lUpLeg + "/LeftLeg";
            string lFoot = lLeg + "/LeftFoot";

            string rUpLeg = hips + "/RightUpLeg";
            string rLeg = rUpLeg + "/RightLeg";
            string rFoot = rLeg + "/RightFoot";

            // FIRMES MILITAR: Pecho alto, barbilla recogida, pies juntos y manos pegadas a los muslos
            SetBoneConstantQuat(clip, hips, new Quaternion(0.685f, 0.000f, 0.000f, 0.728f), dur);
            SetBoneConstantQuat(clip, spine, new Quaternion(-0.030f, 0.000f, 0.000f, 0.9995f), dur);
            SetBoneConstantQuat(clip, spine1, new Quaternion(-0.015f, 0.000f, 0.000f, 0.9999f), dur);
            SetBoneConstantQuat(clip, spine2, new Quaternion(0.050f, 0.000f, 0.000f, 0.9987f), dur);
            SetBoneConstantQuat(clip, neck, new Quaternion(0.040f, 0.000f, 0.000f, 0.9992f), dur);
            SetBoneConstantQuat(clip, head, new Quaternion(-0.025f, 0.000f, 0.000f, 0.9997f), dur);

            // Pies juntos (talones unidos, rodillas rectas, conservando las plantas planas sobre el suelo de Salute frame 0)
            SetBoneConstantQuat(clip, lUpLeg, new Quaternion(-0.012f, -0.045f, 0.9989f, 0.005f), dur);
            SetBoneConstantQuat(clip, lLeg, new Quaternion(-0.030f, 0.000f, 0.000f, 0.9995f), dur);

            SetBoneConstantQuat(clip, rUpLeg, new Quaternion(0.012f, -0.045f, 0.9989f, -0.005f), dur);
            SetBoneConstantQuat(clip, rLeg, new Quaternion(-0.030f, 0.000f, 0.000f, 0.9995f), dur);

            // Brazos rectos pegados al torso y palmas pegadas a la costura exterior de los muslos
            SetBoneConstantQuat(clip, lShoulder, new Quaternion(-0.478f, 0.506f, -0.562f, -0.448f), dur);
            SetBoneConstantQuat(clip, lArm, new Quaternion(0.590f, -0.120f, -0.120f, 0.789f), dur);
            SetBoneConstantQuat(clip, lForeArm, new Quaternion(-0.010f, 0.000f, -0.050f, 0.9987f), dur);
            SetBoneConstantQuat(clip, lHand, new Quaternion(0.000f, 0.000f, 0.050f, 0.9987f), dur);

            SetBoneConstantQuat(clip, rShoulder, new Quaternion(-0.478f, -0.506f, 0.562f, -0.448f), dur);
            SetBoneConstantQuat(clip, rArm, new Quaternion(0.590f, 0.120f, 0.120f, 0.789f), dur);
            SetBoneConstantQuat(clip, rForeArm, new Quaternion(-0.010f, 0.000f, 0.050f, 0.9987f), dur);
            SetBoneConstantQuat(clip, rHand, new Quaternion(0.000f, 0.000f, -0.050f, 0.9987f), dur);

            // Estirar los dedos de ambas manos en posición militar de firmes
            string[] fingers = new string[] { "Index", "Middle", "Ring", "Pinky" };
            for (int f = 0; f < fingers.Length; f++)
            {
                for (int seg = 1; seg <= 3; seg++)
                {
                    string lFinger = lHand + "/LeftHand" + fingers[f] + seg;
                    string rFinger = rHand + "/RightHand" + fingers[f] + seg;
                    SetBoneConstantQuat(clip, lFinger, new Quaternion(0.05f, 0f, 0f, 0.9987f), dur);
                    SetBoneConstantQuat(clip, rFinger, new Quaternion(0.05f, 0f, 0f, 0.9987f), dur);
                }
            }

            return clip;
        }

        private static AnimationClip BuildRightPunchClip(AnimationClip baseSaluteClip)
        {
            float dur = 0.68f;
            AnimationClip clip = BuildUprightIdleClip(baseSaluteClip);
            clip.name = "HumanSpRightPunch_InPlace";

            SerializedObject so = new SerializedObject(clip);
            SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                var lp = settings.FindPropertyRelative("m_LoopTime");
                if (lp != null) lp.boolValue = false;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            string hips = "Armature/Hips";
            string spine = hips + "/Spine";
            string spine1 = spine + "/Spine1";
            string spine2 = spine1 + "/Spine2";
            string lShoulder = spine2 + "/LeftShoulder";
            string lArm = lShoulder + "/LeftArm";
            string lForeArm = lArm + "/LeftForeArm";
            string rShoulder = spine2 + "/RightShoulder";
            string rArm = rShoulder + "/RightArm";
            string rForeArm = rArm + "/RightForeArm";
            string rHand = rForeArm + "/RightHand";

            float[] times = new float[] { 0.0f, 0.14f, 0.26f, 0.42f, dur };

            // Rotación de torso acompañando el golpe directo de derecha
            SetBoneKeyframedQuat(clip, spine1, times, new Quaternion[]
            {
                new Quaternion(-0.015f, 0.00f, 0.00f, 0.9999f),
                new Quaternion(0.020f, 0.12f, 0.02f, 0.992f),   // Armado hacia atrás
                new Quaternion(0.085f, -0.22f, -0.04f, 0.971f), // Extensión de impacto
                new Quaternion(0.050f, -0.14f, -0.02f, 0.988f),
                new Quaternion(-0.015f, 0.00f, 0.00f, 0.9999f)
            });

            // Brazo izquierdo sube en guardia táctica
            SetBoneKeyframedQuat(clip, lArm, times, new Quaternion[]
            {
                new Quaternion(0.50f, -0.18f, -0.22f, 0.818f),
                new Quaternion(0.28f, -0.35f, -0.42f, 0.789f),
                new Quaternion(0.24f, -0.38f, -0.45f, 0.771f),
                new Quaternion(0.32f, -0.30f, -0.36f, 0.821f),
                new Quaternion(0.50f, -0.18f, -0.22f, 0.818f)
            });
            SetBoneKeyframedQuat(clip, lForeArm, times, new Quaternion[]
            {
                new Quaternion(-0.02f, 0.00f, -0.14f, 0.990f),
                new Quaternion(0.18f, -0.25f, -0.68f, 0.663f),
                new Quaternion(0.20f, -0.28f, -0.72f, 0.602f),
                new Quaternion(0.12f, -0.18f, -0.50f, 0.838f),
                new Quaternion(-0.02f, 0.00f, -0.14f, 0.990f)
            });

            // Brazo derecho: carga atrás (0.14s) -> golpe recto al frente (0.26s) -> retorno (0.68s)
            SetBoneKeyframedQuat(clip, rArm, times, new Quaternion[]
            {
                new Quaternion(0.50f, 0.18f, 0.22f, 0.818f),
                new Quaternion(0.58f, 0.42f, 0.05f, 0.695f),
                new Quaternion(0.08f, -0.46f, 0.56f, 0.683f),
                new Quaternion(0.14f, -0.38f, 0.52f, 0.751f),
                new Quaternion(0.50f, 0.18f, 0.22f, 0.818f)
            });

            SetBoneKeyframedQuat(clip, rForeArm, times, new Quaternion[]
            {
                new Quaternion(-0.02f, 0.00f, 0.14f, 0.990f),
                new Quaternion(0.15f, 0.32f, 0.76f, 0.543f),
                new Quaternion(-0.02f, 0.04f, 0.06f, 0.997f), // Brazo totalmente extendido en el impacto
                new Quaternion(0.08f, 0.18f, 0.42f, 0.885f),
                new Quaternion(-0.02f, 0.00f, 0.14f, 0.990f)
            });

            // Cerrar los dedos de la mano derecha formando un puño firme
            string[] fingers = new string[] { "Index", "Middle", "Ring", "Pinky" };
            for (int f = 0; f < fingers.Length; f++)
            {
                for (int seg = 1; seg <= 3; seg++)
                {
                    string rFinger = rHand + "/RightHand" + fingers[f] + seg;
                    SetBoneConstantQuat(clip, rFinger, new Quaternion(0.70f, 0f, 0f, 0.714f), dur);
                }
            }

            return clip;
        }

        private static AnimationClip BuildPullChairClip(AnimationClip baseSaluteClip)
        {
            float dur = 0.42f;
            AnimationClip clip = BuildUprightIdleClip(baseSaluteClip);
            clip.name = "HumanSpPullChair_InPlace";

            SerializedObject so = new SerializedObject(clip);
            SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                var lp = settings.FindPropertyRelative("m_LoopTime");
                if (lp != null) lp.boolValue = false;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            string hips = "Armature/Hips";
            string spine = hips + "/Spine";
            string spine1 = spine + "/Spine1";
            string spine2 = spine1 + "/Spine2";
            string rShoulder = spine2 + "/RightShoulder";
            string rArm = rShoulder + "/RightArm";
            string rForeArm = rArm + "/RightForeArm";

            float[] times = new float[] { 0.0f, 0.20f, dur };

            // Inclinación suave del torso y extensión de la mano derecha tomando el respaldo/asiento de la silla
            SetBoneKeyframedQuat(clip, spine1, times, new Quaternion[]
            {
                new Quaternion(-0.015f, 0f, 0f, 0.9999f),
                new Quaternion(0.140f, -0.08f, 0f, 0.987f),
                new Quaternion(0.060f, 0f, 0f, 0.998f)
            });

            SetBoneKeyframedQuat(clip, rArm, times, new Quaternion[]
            {
                new Quaternion(0.50f, 0.18f, 0.22f, 0.818f),
                new Quaternion(0.22f, -0.28f, 0.44f, 0.824f),
                new Quaternion(0.38f, 0.05f, 0.30f, 0.873f)
            });

            SetBoneKeyframedQuat(clip, rForeArm, times, new Quaternion[]
            {
                new Quaternion(-0.02f, 0f, 0.14f, 0.990f),
                new Quaternion(0.08f, 0.12f, 0.42f, 0.896f),
                new Quaternion(0.04f, 0.08f, 0.32f, 0.943f)
            });

            return clip;
        }

        // =========================================================================================
        // 4. CONSTRUCTORES DE PERSONAJES EN ESCENA (CAPITÁN, 30 POLICÍAS DE TROPA Y 2 MINEROS)
        // =========================================================================================

        public static GameObject SpawnRiggedCaptainInOffice(
            Transform parent,
            Vector3 seatedWorldPos,
            Quaternion seatedWorldRot,
            Vector3 clearStandingWorldPos,
            InteractiveChair executiveChair)
        {
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CAPTAIN_FBX);
            if (fbxPrefab == null) return null;

            Material capMat = GetOrCreatePBRCharacterMaterial(CAPTAIN_DIR, "Mat_UTOP_Captain_Skin");
            // Conservar la malla original completa del FBX del Capitán para máxima nitidez y calidad visual
            SkinnedMeshRenderer srcSmr = fbxPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Mesh capMesh = srcSmr != null ? srcSmr.sharedMesh : null;

            var nonLooping = new HashSet<string>
            {
                "HumanSpSalute",
                "HumanSpPointingForward",
                "HumanSpShoulderHitAndFall",
                "HumanSpKipUp"
            };
            Dictionary<string, AnimationClip> clips = ProcessFBXClipsToInPlace(CAPTAIN_FBX, CAPTAIN_CLIPS_DIR, nonLooping);

            AnimationClip baseSalute = null;
            clips.TryGetValue("HumanSpSalute", out baseSalute);
            if (baseSalute != null)
            {
                GenerateSynthesizedUTOPClips(baseSalute, CAPTAIN_CLIPS_DIR, clips);
            }

            GameObject captainRoot = new GameObject("NPC_Capitan_UTOP");
            captainRoot.transform.SetParent(parent, false);
            captainRoot.transform.position = seatedWorldPos;
            captainRoot.transform.rotation = seatedWorldRot;

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            visual.name = "UTOP_Captain_Mesh";
            visual.transform.SetParent(captainRoot.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            SkinnedMeshRenderer smr = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                if (capMesh != null) smr.sharedMesh = capMesh;
                if (capMat != null) smr.sharedMaterial = capMat;
                smr.quality = SkinQuality.Bone4;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;
                smr.updateWhenOffscreen = false;
            }

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.applyRootMotion = false;

            var ctrl = captainRoot.AddComponent<UTOPCaptainNPCController>();
            ctrl.targetAnimator = animator;
            ctrl.skinnedMeshRenderer = smr;
            ctrl.executiveChair = executiveChair;
            ctrl.SetSeatedHomePose(seatedWorldPos, seatedWorldRot, clearStandingWorldPos);

            clips.TryGetValue("HumanSpSitting", out ctrl.sittingClip);
            clips.TryGetValue("HumanSpUprightIdle", out ctrl.uprightIdleClip);
            clips.TryGetValue("HumanSpSalute", out ctrl.saluteClip);
            clips.TryGetValue("HumanSpPointingForward", out ctrl.pointingClip);
            clips.TryGetValue("HumanSpWalking6", out ctrl.walkClip);
            clips.TryGetValue("HumanSpTwoCycleSprint", out ctrl.sprintClip);
            clips.TryGetValue("HumanSpShoulderHitAndFall", out ctrl.hitAndFallClip);
            clips.TryGetValue("HumanSpKipUp", out ctrl.kipUpClip);
            clips.TryGetValue("HumanSpHipHopDancing2", out ctrl.danceClip);

            // Muestrear pose sentada inicial para que sea visible inmediatamente en Edit Mode
            if (ctrl.sittingClip != null)
            {
                ctrl.sittingClip.SampleAnimation(visual, 0.1f);
                if (smr != null) { smr.enabled = false; smr.enabled = true; }
            }

            return captainRoot;
        }

        public static void Spawn30UTOPTroopOfficers(
            Transform compoundRoot,
            List<InteractiveChair> barracksChairs)
        {
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OFFICER_FBX);
            if (fbxPrefab == null) return;

            Material officerMat = GetOrCreatePBRCharacterMaterial(OFFICER_DIR, "Mat_UTOP_Officer_Skin");
            SkinnedMeshRenderer srcSmr = fbxPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Mesh troopFullMesh = srcSmr != null ? srcSmr.sharedMesh : null;
            // Malla de alta definición para distancia media/lejana (gridDiv = 160, ~115k vértices, sin pérdida visual)
            Mesh troopHighLodMesh = GetOrCreateDecimatedMesh(OFFICER_FBX, OFFICER_DIR + "/Mesh_UTOP_Officer_Troop_HQ.asset", 160);
            if (troopHighLodMesh == null) troopHighLodMesh = troopFullMesh;

            var nonLooping = new HashSet<string>
            {
                "HumanSpRunningJump",
                "HumanSpQuickRollToRun",
                "HumanSpSalute",
                "HumanSpTwoHandedSwordDeath"
            };
            Dictionary<string, AnimationClip> clips = ProcessFBXClipsToInPlace(OFFICER_FBX, OFFICER_CLIPS_DIR, nonLooping);
            AnimationClip baseSalute = null;
            clips.TryGetValue("HumanSpSalute", out baseSalute);
            if (baseSalute != null)
            {
                GenerateSynthesizedUTOPClips(baseSalute, OFFICER_CLIPS_DIR, clips);
            }

            AnimationClip uprightClip, attentionClip, walkClip, sprintClip, saluteClip, sitClip;
            clips.TryGetValue("HumanSpUprightIdle", out uprightClip);
            clips.TryGetValue("HumanSpMilitaryAttention", out attentionClip);
            clips.TryGetValue("HumanSpWalking6", out walkClip);
            clips.TryGetValue("HumanSpTwoCycleSprint", out sprintClip);
            clips.TryGetValue("HumanSpSalute", out saluteClip);
            clips.TryGetValue("HumanSpSitting", out sitClip);

            GameObject squadRoot = new GameObject("Escuadron_30_Policias_UTOP");
            squadRoot.transform.SetParent(compoundRoot, false);

            // Centro exacto del bloque central del Patio de Honor (Imagen 1 de referencia: frente al emblema UTOP)
            float baseFormationZ = -15.20f;
            float rowSpacingZ = 1.15f;
            float colSpacingX = 0.84f;
            float centerFormationX = 13.0f;
            float patioFloorY = -0.30f;

            var manager = squadRoot.AddComponent<UTOPTroopFormationManager>();
            manager.emblemCenter = new Vector3(13.0f, -0.30f, -10.5f);
            manager.emblemActivationRadius = 3.25f;
            manager.patioFormationCenter = new Vector3(centerFormationX, patioFloorY, baseFormationZ);
            manager.rowSpacing = rowSpacingZ;
            manager.colSpacing = colSpacingX;

            var qualityLod = squadRoot.AddComponent<NpcQualityLod>();
            qualityLod.fullMesh = troopFullMesh;
            qualityLod.lodMesh = troopHighLodMesh;
            qualityLod.fullMeshDistance = 14.0f;
            qualityLod.maxFullMeshCount = 12;

            for (int i = 0; i < 30; i++)
            {
                int row = i / 10; // 0..2 (3 filas)
                int col = i % 10; // 0..9 (10 columnas)

                float formX = centerFormationX + (col - 4.5f) * colSpacingX;
                float formZ = baseFormationZ - row * rowSpacingZ;
                Vector3 formationPos = new Vector3(formX, patioFloorY, formZ);
                // Mirando hacia +Z (hacia el jugador parado en el emblema en Z = -10.5)
                Quaternion formationRot = Quaternion.Euler(0f, 0f, 0f);

                // Posición inicial dentro del amplio salón de la Cuadra de Tropa 1 (X: 21.2 a 33.0, Z: -24.8 a -19.8)
                InteractiveChair chairForOfficer = (barracksChairs != null && i < barracksChairs.Count) ? barracksChairs[i] : null;
                Vector3 barracksPos;
                Quaternion barracksRot;

                if (chairForOfficer != null)
                {
                    barracksPos = chairForOfficer.GetSeatedRootPosition();
                    barracksRot = chairForOfficer.GetSeatedRotation();
                }
                else
                {
                    int standIdx = i - (barracksChairs != null ? barracksChairs.Count : 0);
                    int bRow = standIdx / 6;
                    int bCol = standIdx % 6;
                    float bx = 21.6f + bCol * 1.95f + ((bRow % 2 == 0) ? 0.25f : -0.25f);
                    float bz = -24.5f + bRow * 1.35f;
                    barracksPos = new Vector3(Mathf.Clamp(bx, 21.2f, 33.0f), patioFloorY, Mathf.Clamp(bz, -24.8f, -19.8f));
                    barracksRot = Quaternion.Euler(0f, (standIdx * 67f) % 360f, 0f);
                }

                GameObject troopGO = new GameObject(string.Format("Policia_UTOP_{0:00}_F{1}C{2}", i + 1, row + 1, col + 1));
                troopGO.transform.SetParent(squadRoot.transform, false);
                troopGO.transform.position = barracksPos;
                troopGO.transform.rotation = barracksRot;

                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
                visual.name = "UTOP_Troop_Mesh";
                visual.transform.SetParent(troopGO.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                SkinnedMeshRenderer smr = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null)
                {
                    smr.sharedMesh = (i < 12 && troopFullMesh != null) ? troopFullMesh : troopHighLodMesh;
                    if (officerMat != null) smr.sharedMaterial = officerMat;
                    smr.quality = SkinQuality.Bone4;
                    smr.shadowCastingMode = (row == 0)
                        ? UnityEngine.Rendering.ShadowCastingMode.On
                        : UnityEngine.Rendering.ShadowCastingMode.Off;
                    smr.receiveShadows = true;
                    smr.updateWhenOffscreen = false;
                    qualityLod.renderers.Add(smr);
                }

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var npc = troopGO.AddComponent<UTOPTroopNPCController>();
                npc.targetAnimator = animator;
                npc.skinnedMeshRenderer = smr;
                npc.uprightIdleClip = uprightClip;
                npc.militaryAttentionClip = attentionClip;
                npc.walkClip = walkClip;
                npc.sprintClip = sprintClip;
                npc.saluteClip = saluteClip;
                npc.sitClip = sitClip;

                npc.ConfigureTroop(i, row, col, barracksPos, barracksRot, formationPos, formationRot, chairForOfficer);
                manager.troopOfficers.Add(npc);

                AnimationClip previewClip = (chairForOfficer != null && sitClip != null) ? sitClip : uprightClip;
                if (previewClip != null)
                {
                    previewClip.SampleAnimation(visual, 0.1f);
                    if (smr != null) { smr.enabled = false; smr.enabled = true; }
                }
            }
        }

        public static void Spawn2MinerBlockersOnCalleJunin(Transform streetRoot, Func<float, float> getHeightAtZ)
        {
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MINER_FBX);
            if (fbxPrefab == null) return;

            Material minerMat = GetOrCreatePBRCharacterMaterial(MINER_DIR, "Mat_Miner_Blocker_Skin");
            // Malla original de alta calidad completa
            SkinnedMeshRenderer srcSmr = fbxPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Mesh minerMesh = srcSmr != null ? srcSmr.sharedMesh : null;

            var nonLooping = new HashSet<string>
            {
                "HumanSpGoalieThrow",
                "HumanSpGoalkeeperOverhandThrow",
                "HumanSpHeadHit"
            };
            Dictionary<string, AnimationClip> clips = ProcessFBXClipsToInPlace(MINER_FBX, MINER_CLIPS_DIR, nonLooping);

            AnimationClip officerUpright = AssetDatabase.LoadAssetAtPath<AnimationClip>(OFFICER_CLIPS_DIR + "/HumanSpUprightIdle_InPlace.anim");
            if (officerUpright != null)
            {
                string uprightPath = MINER_CLIPS_DIR + "/HumanSpUprightIdle_InPlace.anim";
                SaveOrUpdateClipAsset(officerUpright, uprightPath);
                clips["HumanSpUprightIdle"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(uprightPath);
            }

            Vector3[] minerSpawns = new Vector3[]
            {
                new Vector3(-0.85f, getHeightAtZ != null ? getHeightAtZ(-2.5f) : -0.15f, -2.5f),
                new Vector3(1.15f, getHeightAtZ != null ? getHeightAtZ(3.8f) : 0.35f, 3.8f)
            };

            string[] titles = new string[]
            {
                "Minero Bloqueador #1 — Vanguardia / Dinamitero",
                "Minero Bloqueador #2 — Flanco / Lanzapiedras"
            };

            for (int i = 0; i < 2; i++)
            {
                GameObject minerGO = new GameObject("NPC_Minero_Bloqueador_" + (i + 1));
                minerGO.transform.SetParent(streetRoot, false);
                minerGO.transform.position = minerSpawns[i];
                minerGO.transform.rotation = Quaternion.Euler(0f, (i == 0) ? 110f : 185f, 0f);

                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
                visual.name = "Miner_Blocker_Mesh";
                visual.transform.SetParent(minerGO.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                SkinnedMeshRenderer smr = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null)
                {
                    if (minerMesh != null) smr.sharedMesh = minerMesh;
                    if (minerMat != null) smr.sharedMaterial = minerMat;
                    smr.quality = SkinQuality.Bone4;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    smr.receiveShadows = true;
                    smr.updateWhenOffscreen = false;
                }

                Animator animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.applyRootMotion = false;

                var ctrl = minerGO.AddComponent<MinerBlockerNPCController>();
                ctrl.protesterType = ProtesterType.Minero;
                ctrl.canThrowDynamite = true;
                ctrl.minerIndex = i + 1;
                ctrl.minerRoleTitle = titles[i];
                ctrl.targetAnimator = animator;
                ctrl.skinnedMeshRenderer = smr;
                ctrl.SetHomePosition(minerSpawns[i]);

                clips.TryGetValue("HumanSpUprightIdle", out ctrl.uprightIdleClip);
                clips.TryGetValue("HumanSpIdle2", out ctrl.idle2Clip);
                clips.TryGetValue("HumanSpIdle4", out ctrl.idle4Clip);
                clips.TryGetValue("HumanSpWalking6", out ctrl.walkClip);
                clips.TryGetValue("HumanSpRunning2", out ctrl.running2Clip);
                clips.TryGetValue("HumanSpRun2", out ctrl.run2Clip);
                clips.TryGetValue("HumanSpTwoCycleSprint", out ctrl.sprintClip);
                clips.TryGetValue("HumanSpRunLookBack", out ctrl.runLookBackClip);
                clips.TryGetValue("HumanSpGoalieThrow", out ctrl.goalieThrowClip);
                clips.TryGetValue("HumanSpGoalkeeperOverhandThrow", out ctrl.overhandThrowClip);
                clips.TryGetValue("HumanSpHeadHit", out ctrl.headHitClip);

                AnimationClip sampleClip = (i == 0 && ctrl.uprightIdleClip != null) ? ctrl.uprightIdleClip : ctrl.idle2Clip;
                if (sampleClip != null)
                {
                    sampleClip.SampleAnimation(visual, 0.1f);
                    if (smr != null) { smr.enabled = false; smr.enabled = true; }
                }
            }
        }

        /// <summary>
        /// Despliega los nuevos NPCs manifestantes en la Calle Junín con su calidad original completa:
        /// - 2 Cholitas (Cholita_Animations.fbx): SOLO lanzan piedras (canThrowDynamite = false).
        /// - 2 Ponchos Rojos (PonchoRojo_Animations.fbx): SOLO lanzan piedras (canThrowDynamite = false).
        /// </summary>
        public static void SpawnCholitasAndPonchosRojosOnCalleJunin(Transform streetRoot, Func<float, float> getHeightAtZ)
        {
            AnimationClip officerUpright = AssetDatabase.LoadAssetAtPath<AnimationClip>(OFFICER_CLIPS_DIR + "/HumanSpUprightIdle_InPlace.anim");

            // 1. CHOLITAS (2 manifestantes en Calle Junín — solo lanzan piedras, NUNCA dinamita)
            GameObject cholitaFbx = AssetDatabase.LoadAssetAtPath<GameObject>(CHOLITA_FBX);
            if (cholitaFbx != null)
            {
                Material cholitaMat = GetOrCreatePBRCharacterMaterial(CHOLITA_DIR, "Mat_Cholita_Skin");
                SkinnedMeshRenderer srcSmr = cholitaFbx.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Mesh cholitaFullMesh = srcSmr != null ? srcSmr.sharedMesh : null;

                var nonLoopCholita = new HashSet<string>
                {
                    "HumanSpThrow",
                    "HumanSpRunningJump",
                    "HumanSpReceivingAnUppercut"
                };
                Dictionary<string, AnimationClip> chClips = ProcessFBXClipsToInPlace(CHOLITA_FBX, CHOLITA_CLIPS_DIR, nonLoopCholita);
                if (officerUpright != null)
                {
                    string upPath = CHOLITA_CLIPS_DIR + "/HumanSpUprightIdle_InPlace.anim";
                    SaveOrUpdateClipAsset(officerUpright, upPath);
                    chClips["HumanSpUprightIdle"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(upPath);
                }

                Vector3[] cholitaPos = new Vector3[]
                {
                    new Vector3(-2.15f, getHeightAtZ != null ? getHeightAtZ(-5.2f) : -0.25f, -5.2f),
                    new Vector3(0.45f, getHeightAtZ != null ? getHeightAtZ(0.8f) : 0.10f, 0.8f)
                };

                for (int i = 0; i < 2; i++)
                {
                    GameObject go = new GameObject("NPC_Cholita_Manifestante_" + (i + 1));
                    go.transform.SetParent(streetRoot, false);
                    go.transform.position = cholitaPos[i];
                    go.transform.rotation = Quaternion.Euler(0f, (i == 0) ? 85f : 135f, 0f);

                    GameObject vis = (GameObject)PrefabUtility.InstantiatePrefab(cholitaFbx);
                    vis.name = "Cholita_Mesh";
                    vis.transform.SetParent(go.transform, false);
                    vis.transform.localPosition = Vector3.zero;
                    vis.transform.localRotation = Quaternion.identity;
                    vis.transform.localScale = Vector3.one;

                    SkinnedMeshRenderer smr = vis.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (smr != null)
                    {
                        if (cholitaFullMesh != null) smr.sharedMesh = cholitaFullMesh;
                        if (cholitaMat != null) smr.sharedMaterial = cholitaMat;
                        smr.quality = SkinQuality.Bone4;
                        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        smr.receiveShadows = true;
                        smr.updateWhenOffscreen = false;
                    }

                    Animator anim = vis.GetComponent<Animator>();
                    if (anim == null) anim = vis.AddComponent<Animator>();
                    anim.applyRootMotion = false;

                    var ctrl = go.AddComponent<MinerBlockerNPCController>();
                    ctrl.protesterType = ProtesterType.Cholita;
                    ctrl.canThrowDynamite = false; // Las cholitas NUNCA lanzan dinamita, solo piedras
                    ctrl.minerIndex = 3 + i;
                    ctrl.minerRoleTitle = $"Cholita Manifestante #{i + 1} — Bloqueo / Solo Piedras";
                    ctrl.targetAnimator = anim;
                    ctrl.skinnedMeshRenderer = smr;
                    ctrl.SetHomePosition(cholitaPos[i]);

                    chClips.TryGetValue("HumanSpUprightIdle", out ctrl.uprightIdleClip);
                    chClips.TryGetValue("HumanSpIdle2", out ctrl.idle2Clip);
                    chClips.TryGetValue("HumanSpIdle2", out ctrl.idle4Clip);
                    chClips.TryGetValue("HumanSpWalking6", out ctrl.walkClip);
                    chClips.TryGetValue("HumanSpRunning2", out ctrl.running2Clip);
                    chClips.TryGetValue("HumanSpRunning2", out ctrl.run2Clip);
                    chClips.TryGetValue("HumanSpTwoCycleSprint", out ctrl.sprintClip);
                    chClips.TryGetValue("HumanSpRunLookBack", out ctrl.runLookBackClip);
                    chClips.TryGetValue("HumanSpThrow", out ctrl.goalieThrowClip);
                    chClips.TryGetValue("HumanSpThrow", out ctrl.overhandThrowClip);
                    chClips.TryGetValue("HumanSpReceivingAnUppercut", out ctrl.headHitClip);

                    AnimationClip sc = ctrl.uprightIdleClip != null ? ctrl.uprightIdleClip : ctrl.idle2Clip;
                    if (sc != null)
                    {
                        sc.SampleAnimation(vis, 0.1f);
                        if (smr != null) { smr.enabled = false; smr.enabled = true; }
                    }
                }
            }

            // 2. PONCHOS ROJOS (2 manifestantes en Calle Junín — solo lanzan piedras, NUNCA dinamita)
            GameObject ponchoFbx = AssetDatabase.LoadAssetAtPath<GameObject>(PONCHO_FBX);
            if (ponchoFbx != null)
            {
                Material ponchoMat = GetOrCreatePBRCharacterMaterial(PONCHO_DIR, "Mat_PonchoRojo_Skin");
                SkinnedMeshRenderer srcSmr = ponchoFbx.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Mesh ponchoFullMesh = srcSmr != null ? srcSmr.sharedMesh : null;

                var nonLoopPoncho = new HashSet<string>
                {
                    "HumanSpGoalieThrow",
                    "HumanSpGoalkeeperOverhandThrow",
                    "HumanSpStandingMeleeKick",
                    "HumanSpRunningJump",
                    "HumanSpShoulderHitAndFall"
                };
                Dictionary<string, AnimationClip> pClips = ProcessFBXClipsToInPlace(PONCHO_FBX, PONCHO_CLIPS_DIR, nonLoopPoncho);
                if (officerUpright != null)
                {
                    string upPath = PONCHO_CLIPS_DIR + "/HumanSpUprightIdle_InPlace.anim";
                    SaveOrUpdateClipAsset(officerUpright, upPath);
                    pClips["HumanSpUprightIdle"] = AssetDatabase.LoadAssetAtPath<AnimationClip>(upPath);
                }

                Vector3[] ponchoPos = new Vector3[]
                {
                    new Vector3(-1.45f, getHeightAtZ != null ? getHeightAtZ(-8.0f) : -0.45f, -8.0f),
                    new Vector3(-2.35f, getHeightAtZ != null ? getHeightAtZ(2.2f) : 0.22f, 2.2f)
                };

                for (int i = 0; i < 2; i++)
                {
                    GameObject go = new GameObject("NPC_PonchoRojo_Manifestante_" + (i + 1));
                    go.transform.SetParent(streetRoot, false);
                    go.transform.position = ponchoPos[i];
                    go.transform.rotation = Quaternion.Euler(0f, (i == 0) ? 75f : 115f, 0f);

                    GameObject vis = (GameObject)PrefabUtility.InstantiatePrefab(ponchoFbx);
                    vis.name = "PonchoRojo_Mesh";
                    vis.transform.SetParent(go.transform, false);
                    vis.transform.localPosition = Vector3.zero;
                    vis.transform.localRotation = Quaternion.identity;
                    vis.transform.localScale = Vector3.one;

                    SkinnedMeshRenderer smr = vis.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (smr != null)
                    {
                        if (ponchoFullMesh != null) smr.sharedMesh = ponchoFullMesh;
                        if (ponchoMat != null) smr.sharedMaterial = ponchoMat;
                        smr.quality = SkinQuality.Bone4;
                        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        smr.receiveShadows = true;
                        smr.updateWhenOffscreen = false;
                    }

                    Animator anim = vis.GetComponent<Animator>();
                    if (anim == null) anim = vis.AddComponent<Animator>();
                    anim.applyRootMotion = false;

                    var ctrl = go.AddComponent<MinerBlockerNPCController>();
                    ctrl.protesterType = ProtesterType.PonchoRojo;
                    ctrl.canThrowDynamite = false; // Los Ponchos Rojos NUNCA lanzan dinamita, solo piedras
                    ctrl.minerIndex = 5 + i;
                    ctrl.minerRoleTitle = $"Poncho Rojo Manifestante #{i + 1} — Bloqueo / Solo Piedras";
                    ctrl.targetAnimator = anim;
                    ctrl.skinnedMeshRenderer = smr;
                    ctrl.SetHomePosition(ponchoPos[i]);

                    pClips.TryGetValue("HumanSpUprightIdle", out ctrl.uprightIdleClip);
                    pClips.TryGetValue("HumanSpIdle", out ctrl.idle2Clip);
                    pClips.TryGetValue("HumanSpIdle", out ctrl.idle4Clip);
                    pClips.TryGetValue("HumanSpWalking6", out ctrl.walkClip);
                    pClips.TryGetValue("HumanSpRunning2", out ctrl.running2Clip);
                    pClips.TryGetValue("HumanSpRunning2", out ctrl.run2Clip);
                    pClips.TryGetValue("HumanSpRunning2", out ctrl.sprintClip);
                    pClips.TryGetValue("HumanSpRunLookBack", out ctrl.runLookBackClip);
                    pClips.TryGetValue("HumanSpGoalieThrow", out ctrl.goalieThrowClip);
                    pClips.TryGetValue("HumanSpGoalkeeperOverhandThrow", out ctrl.overhandThrowClip);
                    pClips.TryGetValue("HumanSpShoulderHitAndFall", out ctrl.headHitClip);

                    AnimationClip sc = ctrl.uprightIdleClip != null ? ctrl.uprightIdleClip : ctrl.idle2Clip;
                    if (sc != null)
                    {
                        sc.SampleAnimation(vis, 0.1f);
                        if (smr != null) { smr.enabled = false; smr.enabled = true; }
                    }
                }
            }
        }

        // =========================================================================================
        // 5. POBLADO DE LA ARMERÍA Y CALLE CON LOS 8 MODELOS 3D IMPORTADOS (ESCUDO, TONFA, ESCOPETA, LANZAGAS, ETC.)
        // =========================================================================================

        public static void PopulateArmoryAndStreetWithImportedTacticalProps(
            Transform compoundRoot,
            Transform streetRoot,
            Func<float, float> getHeightAtZ,
            ScenarioDirector director)
        {
            string miscDir = PROPS_DIR + "/Misc";
            string escopetaDir = PROPS_DIR + "/Escopeta";
            string lanzaGasDir = PROPS_DIR + "/LanzaGasLacri";
            string propMatDir = PROPS_DIR + "/Materials";
            EnsureFolderExists(propMatDir);

            Material escopetaMat = GetOrCreatePBRCharacterMaterial(escopetaDir, "Mat_Prop_Escopeta_PBR");
            Material lanzaGasMat = GetOrCreatePBRCharacterMaterial(lanzaGasDir, "Mat_Prop_LanzaGas_PBR");

            GameObject shieldFbx = AssetDatabase.LoadAssetAtPath<GameObject>(miscDir + "/ESCUDO.fbx");
            GameObject tonfaFbx = AssetDatabase.LoadAssetAtPath<GameObject>(miscDir + "/TONFA.fbx");
            GameObject gasSpriteFbx = AssetDatabase.LoadAssetAtPath<GameObject>(miscDir + "/GASLACRISPRITE.fbx");
            GameObject balaGasFbx = AssetDatabase.LoadAssetAtPath<GameObject>(miscDir + "/BALAGASLACRI.fbx");
            GameObject barreraGrandeFbx = AssetDatabase.LoadAssetAtPath<GameObject>(miscDir + "/BARRERAGRANDE.fbx");
            GameObject barricadaFbx = AssetDatabase.LoadAssetAtPath<GameObject>(miscDir + "/BARRICADA.fbx");
            GameObject escopetaFbx = AssetDatabase.LoadAssetAtPath<GameObject>(escopetaDir + "/Escopeta.fbx");
            GameObject lanzaGasFbx = AssetDatabase.LoadAssetAtPath<GameObject>(lanzaGasDir + "/LanzaGasLacri.fbx");

            GameObject armoryPropsGroup = new GameObject("Imported_3D_Armory_Props");
            armoryPropsGroup.transform.SetParent(compoundRoot, false);

            float y0 = -0.30f; // GarageFloorY / Armory floor

            // 1. Racks de ESCUDOS 3D reales en la Sala de Armas (Armería: X = 10.2..17.0, Z = 6.2..9.3)
            GameObject firstShieldInstance = null;
            if (shieldFbx != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    float sx = 10.6f + (i % 4) * 1.15f;
                    float sz = (i < 4) ? 9.10f : 6.45f;
                    float yaw = (i < 4) ? 180f : 0f;
                    var sh = SpawnImportedPropInstance(shieldFbx, "Armory_3D_ESCUDO_" + (i + 1), armoryPropsGroup.transform,
                        new Vector3(sx, y0 + 0.65f, sz), Quaternion.Euler(0f, yaw, 0f), 0.92f, null, propMatDir, false);
                    if (firstShieldInstance == null) firstShieldInstance = sh;
                }
            }

            // 2. Racks de TONFAS 3D reales
            GameObject firstTonfaInstance = null;
            if (tonfaFbx != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    float tx = 14.5f + (i % 4) * 0.55f;
                    float tz = (i < 4) ? 9.15f : 6.40f;
                    var tf = SpawnImportedPropInstance(tonfaFbx, "Armory_3D_TONFA_" + (i + 1), armoryPropsGroup.transform,
                        new Vector3(tx, y0 + 1.55f, tz), Quaternion.Euler(0f, 90f, 0f), 1.0f, null, propMatDir, false);
                    if (firstTonfaInstance == null) firstTonfaInstance = tf;
                }
            }

            // 3. ESCOPETAS y LANZAGASES 3D reales con sus texturas PBR en el rack central de la Armería (X = 17.85, Z = 6.4..9.1)
            GameObject firstShotgunInstance = null;
            GameObject firstLauncherInstance = null;
            for (int i = 0; i < 6; i++)
            {
                float gz = 6.45f + i * 0.48f;
                bool isLauncher = (i % 2 == 1);
                GameObject src = isLauncher ? lanzaGasFbx : escopetaFbx;
                Material overrideMat = isLauncher ? lanzaGasMat : escopetaMat;
                if (src != null)
                {
                    var gun = SpawnImportedPropInstance(src,
                        (isLauncher ? "Armory_3D_LANZAGAS_" : "Armory_3D_ESCOPETA_") + (i + 1),
                        armoryPropsGroup.transform,
                        new Vector3(17.82f, y0 + 1.25f, gz),
                        Quaternion.Euler(-75f, -90f, 0f),
                        0.55f, overrideMat, propMatDir, false);
                    if (isLauncher && firstLauncherInstance == null) firstLauncherInstance = gun;
                    if (!isLauncher && firstShotgunInstance == null) firstShotgunInstance = gun;
                }
            }

            // 4. GASLACRISPRITE y BALAGASLACRI 3D sobre los estantes de munición de la Armería
            GameObject firstGasSprayInstance = null;
            if (gasSpriteFbx != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    var gs = SpawnImportedPropInstance(gasSpriteFbx, "Armory_3D_GASLACRISPRITE_" + (i + 1), armoryPropsGroup.transform,
                        new Vector3(15.2f + i * 0.25f, y0 + 0.96f, 9.18f), Quaternion.identity, 1.0f, null, propMatDir, false);
                    if (firstGasSprayInstance == null) firstGasSprayInstance = gs;
                }
            }
            if (balaGasFbx != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    SpawnImportedPropInstance(balaGasFbx, "Armory_3D_BALAGASLACRI_" + (i + 1), armoryPropsGroup.transform,
                        new Vector3(15.2f + i * 0.25f, y0 + 0.96f, 6.38f), Quaternion.Euler(0f, 90f, 0f), 1.0f, null, propMatDir, false);
                }
            }

            // 5. BARRERAGRANDE y BARRICADA en el acceso vehicular de la UTOP y en la línea de bloqueo de Calle Junín
            if (barricadaFbx != null && streetRoot != null)
            {
                // Barricadas de los manifestantes en la calle (con BoxCollider sólido)
                float bZ1 = -6.2f;
                SpawnImportedPropInstance(barricadaFbx, "Street_3D_BARRICADA_1", streetRoot,
                    new Vector3(-1.4f, getHeightAtZ != null ? getHeightAtZ(bZ1) + 0.36f : 0.1f, bZ1),
                    Quaternion.Euler(0f, 12f, 0f), 1.0f, null, propMatDir, true);

                float bZ2 = 1.6f;
                SpawnImportedPropInstance(barricadaFbx, "Street_3D_BARRICADA_2", streetRoot,
                    new Vector3(0.8f, getHeightAtZ != null ? getHeightAtZ(bZ2) + 0.36f : 0.3f, bZ2),
                    Quaternion.Euler(0f, -18f, 0f), 1.0f, null, propMatDir, true);
            }

            if (barreraGrandeFbx != null)
            {
                // Barrera Grande lista junto al pórtico UTOP y Barrera desplegable en Decisión 9 (Protección de instalaciones)
                SpawnImportedPropInstance(barreraGrandeFbx, "Compound_3D_BARRERAGRANDE_Side", armoryPropsGroup.transform,
                    new Vector3(6.8f, y0 + 1.0f, -10.2f), Quaternion.Euler(0f, 90f, 0f), 0.90f, null, propMatDir, true);

                var gateBarrier = SpawnImportedPropInstance(barreraGrandeFbx, "Deployable_3D_BARRERAGRANDE_Gate", armoryPropsGroup.transform,
                    new Vector3(5.65f, y0 + 1.0f, -6.2f), Quaternion.Euler(0f, 90f, 0f), 0.95f, null, propMatDir, true);

                if (director != null && gateBarrier != null)
                {
                    if (!director.deployableEntranceBarriers.Contains(gateBarrier))
                        director.deployableEntranceBarriers.Add(gateBarrier);
                }
            }

            if (director != null)
            {
                director.shieldTemplate = firstShieldInstance;
                director.tonfaTemplate = firstTonfaInstance;
                director.shotgunTemplate = firstShotgunInstance;
                director.gasLauncherTemplate = firstLauncherInstance;
                director.gasSprayTemplate = firstGasSprayInstance;
            }
        }

        private static GameObject SpawnImportedPropInstance(
            GameObject fbxPrefab,
            string instanceName,
            Transform parent,
            Vector3 worldPos,
            Quaternion worldRot,
            float scale,
            Material singleOverrideMat,
            string propMatDir,
            bool addSolidBoxCollider)
        {
            if (fbxPrefab == null) return null;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            inst.name = instanceName;
            inst.transform.SetParent(parent, false);
            inst.transform.position = worldPos;
            inst.transform.rotation = worldRot;
            inst.transform.localScale = Vector3.one * scale;

            MeshRenderer[] renderers = inst.GetComponentsInChildren<MeshRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var mr = renderers[r];
                if (singleOverrideMat != null)
                {
                    mr.sharedMaterial = singleOverrideMat;
                }
                else
                {
                    Material[] srcMats = mr.sharedMaterials;
                    Material[] newMats = new Material[srcMats.Length];
                    for (int m = 0; m < srcMats.Length; m++)
                    {
                        string mName = srcMats[m] != null ? srcMats[m].name : "DefaultMetal";
                        newMats[m] = GetOrCreateNamedPropURPMaterial(propMatDir, mName);
                    }
                    mr.sharedMaterials = newMats;
                }

                if (addSolidBoxCollider && mr.GetComponent<Collider>() == null)
                {
                    mr.gameObject.AddComponent<BoxCollider>();
                }
            }

            return inst;
        }

        private static Material GetOrCreateNamedPropURPMaterial(string folder, string rawMatName)
        {
            string clean = rawMatName.Replace(" (Instance)", "").Replace(" ", "_").Replace(":", "_");
            string path = folder + "/Mat_Prop_" + clean + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(urpLit) { name = "Mat_Prop_" + clean };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = urpLit;
            }

            string lower = rawMatName.ToLowerInvariant();
            Color col = new Color(0.20f, 0.22f, 0.24f, 1f);
            float metallic = 0.35f;
            float smooth = 0.45f;
            bool transparent = false;

            if (lower.Contains("polycarbonate") || lower.Contains("clear"))
            {
                col = new Color(0.78f, 0.88f, 0.95f, 0.38f);
                metallic = 0.05f;
                smooth = 0.88f;
                transparent = true;
            }
            else if (lower.Contains("gold"))
            {
                col = new Color(0.92f, 0.76f, 0.22f, 1f);
                metallic = 0.82f;
                smooth = 0.72f;
            }
            else if (lower.Contains("green"))
            {
                col = new Color(0.14f, 0.36f, 0.22f, 1f);
                metallic = 0.15f;
                smooth = 0.50f;
            }
            else if (lower.Contains("white"))
            {
                col = new Color(0.95f, 0.96f, 0.96f, 1f);
                metallic = 0.05f;
                smooth = 0.55f;
            }
            else if (lower.Contains("blue"))
            {
                col = new Color(0.16f, 0.44f, 0.82f, 1f);
                metallic = 0.25f;
                smooth = 0.60f;
            }
            else if (lower.Contains("steel") || lower.Contains("galvanized") || lower.Contains("hinge"))
            {
                col = new Color(0.58f, 0.61f, 0.64f, 1f);
                metallic = 0.80f;
                smooth = 0.65f;
            }
            else if (lower.Contains("rubber") || lower.Contains("polymer") || lower.Contains("black") || lower.Contains("charcoal"))
            {
                col = new Color(0.11f, 0.12f, 0.13f, 1f);
                metallic = 0.12f;
                smooth = 0.35f;
            }

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }

            mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
