using System.Collections.Generic;
using UnityEngine;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Genera la geometría 3D completa y articulada del personaje humanoide,
    /// asegurando que cada pivote de rotación esté exactamente en la articulación anatómica correspondiente.
    /// </summary>
    public static class HumanoidModelBuilder
    {
        public static ArticulatedHumanoid BuildHumanoid(
            GameObject rootObject,
            Material bodyMaterial,
            Material jointMaterial,
            Material accentMaterial,
            Material eyesMaterial)
        {
            var humanoid = rootObject.AddComponent<ArticulatedHumanoid>();
            var bodyRenderers = new List<Renderer>();
            var jointRenderers = new List<Renderer>();

            // Contenedor visual
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(rootObject.transform, false);

            // ==========================================
            // CADERAS / PELVIS (Pivote central a 0.95m de altura)
            // ==========================================
            var hips = CreateJoint("Hips", visualRoot.transform, new Vector3(0, 0.95f, 0));
            humanoid.hips = hips.transform;
            var pelvisMesh = CreateBox("PelvisMesh", hips.transform, Vector3.zero, new Vector3(0.28f, 0.16f, 0.18f), bodyMaterial, bodyRenderers);
            pelvisMesh.layer = rootObject.layer;

            // ==========================================
            // COLUMNA: CINTURA Y TORSO/PECHO
            // ==========================================
            var waist = CreateJoint("Waist", hips.transform, new Vector3(0, 0.10f, 0));
            humanoid.waist = waist.transform;
            CreateSphere("WaistJoint", waist.transform, Vector3.zero, 0.13f, jointMaterial, jointRenderers);
            CreateCapsule("WaistMesh", waist.transform, new Vector3(0, 0.08f, 0), new Vector3(0.24f, 0.14f, 0.16f), bodyMaterial, bodyRenderers);

            var chest = CreateJoint("Chest", waist.transform, new Vector3(0, 0.16f, 0));
            humanoid.chest = chest.transform;
            CreateSphere("ChestJoint", chest.transform, Vector3.zero, 0.14f, jointMaterial, jointRenderers);
            CreateBox("ChestMesh", chest.transform, new Vector3(0, 0.14f, 0), new Vector3(0.32f, 0.24f, 0.20f), bodyMaterial, bodyRenderers);
            // Placa pectoral de acento
            CreateBox("ChestPlate", chest.transform, new Vector3(0, 0.15f, 0.11f), new Vector3(0.22f, 0.16f, 0.03f), accentMaterial, jointRenderers);

            // ==========================================
            // CUELLO Y CABEZA
            // ==========================================
            var neck = CreateJoint("Neck", chest.transform, new Vector3(0, 0.28f, 0));
            humanoid.neck = neck.transform;
            CreateCylinder("NeckMesh", neck.transform, new Vector3(0, 0.04f, 0), new Vector3(0.09f, 0.08f, 0.09f), jointMaterial, jointRenderers);

            var head = CreateJoint("Head", neck.transform, new Vector3(0, 0.09f, 0));
            humanoid.head = head.transform;
            CreateSphere("HeadJoint", head.transform, Vector3.zero, 0.11f, jointMaterial, jointRenderers);
            CreateBox("HeadMesh", head.transform, new Vector3(0, 0.10f, 0.02f), new Vector3(0.18f, 0.22f, 0.19f), bodyMaterial, bodyRenderers);

            // Visor / Ojos
            var visor = CreateBox("Visor", head.transform, new Vector3(0, 0.11f, 0.12f), new Vector3(0.14f, 0.05f, 0.04f), eyesMaterial, bodyRenderers);
            humanoid.visor = visor.transform;

            // ==========================================
            // EXTREMIDADES SUPERIORES (BRAZOS, ANTEBRAZOS, MUÑECAS, DEDOS)
            // ==========================================
            BuildArm(true, chest.transform, humanoid, bodyMaterial, jointMaterial, accentMaterial, bodyRenderers, jointRenderers);
            BuildArm(false, chest.transform, humanoid, bodyMaterial, jointMaterial, accentMaterial, bodyRenderers, jointRenderers);

            // ==========================================
            // EXTREMIDADES INFERIORES (PIERNAS, RODILLAS, TOBILLOS/PIES)
            // ==========================================
            BuildLeg(true, hips.transform, humanoid, bodyMaterial, jointMaterial, accentMaterial, bodyRenderers, jointRenderers);
            BuildLeg(false, hips.transform, humanoid, bodyMaterial, jointMaterial, accentMaterial, bodyRenderers, jointRenderers);

            humanoid.bodyRenderers = bodyRenderers.ToArray();
            humanoid.jointRenderers = jointRenderers.ToArray();

            // Eliminar colliders en la geometría visual para que no choquen con el CharacterController
            var colliders = visualRoot.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                Object.DestroyImmediate(col);
            }

            return humanoid;
        }

        private static void BuildArm(
            bool isLeft,
            Transform chest,
            ArticulatedHumanoid humanoid,
            Material bodyMat,
            Material jointMat,
            Material accentMat,
            List<Renderer> bodyRends,
            List<Renderer> jointRends)
        {
            string side = isLeft ? "Left" : "Right";
            float sideSign = isLeft ? -1f : 1f;

            // Clavícula / Hombro base
            var shoulder = CreateJoint(side + "Shoulder", chest, new Vector3(sideSign * 0.17f, 0.22f, 0));
            if (isLeft) humanoid.leftShoulder = shoulder.transform;
            else humanoid.rightShoulder = shoulder.transform;

            CreateSphere(side + "ShoulderJoint", shoulder.transform, Vector3.zero, 0.10f, jointMat, jointRends);

            // Brazo superior (pivote en el hombro)
            var upperArm = CreateJoint(side + "UpperArm", shoulder.transform, new Vector3(sideSign * 0.04f, 0, 0));
            if (isLeft) humanoid.leftUpperArm = upperArm.transform;
            else humanoid.rightUpperArm = upperArm.transform;

            // Malla del brazo superior (hacia abajo -Y)
            CreateCapsule(side + "UpperArmMesh", upperArm.transform, new Vector3(0, -0.13f, 0), new Vector3(0.09f, 0.13f, 0.09f), bodyMat, bodyRends);

            // Codo / Antebrazo (pivote a 0.26m debajo del hombro)
            var forearm = CreateJoint(side + "Forearm", upperArm.transform, new Vector3(0, -0.26f, 0));
            if (isLeft) humanoid.leftForearm = forearm.transform;
            else humanoid.rightForearm = forearm.transform;

            CreateSphere(side + "ElbowJoint", forearm.transform, Vector3.zero, 0.085f, jointMat, jointRends);
            CreateCapsule(side + "ForearmMesh", forearm.transform, new Vector3(0, -0.12f, 0), new Vector3(0.08f, 0.12f, 0.08f), bodyMat, bodyRends);

            // Muñeca (pivote a 0.24m debajo del codo)
            var wrist = CreateJoint(side + "Wrist", forearm.transform, new Vector3(0, -0.24f, 0));
            if (isLeft) humanoid.leftWrist = wrist.transform;
            else humanoid.rightWrist = wrist.transform;

            CreateSphere(side + "WristJoint", wrist.transform, Vector3.zero, 0.065f, jointMat, jointRends);

            // Mano (palma)
            var hand = CreateJoint(side + "Hand", wrist.transform, new Vector3(0, -0.04f, 0));
            if (isLeft) humanoid.leftHand = hand.transform;
            else humanoid.rightHand = hand.transform;

            CreateBox(side + "Palm", hand.transform, new Vector3(0, -0.03f, 0), new Vector3(0.06f, 0.06f, 0.03f), bodyMat, bodyRends);

            // Pulgar
            var thumb = CreateJoint(side + "Thumb", hand.transform, new Vector3(sideSign * -0.03f, -0.01f, 0.02f));
            if (isLeft) humanoid.leftThumb = thumb.transform;
            else humanoid.rightThumb = thumb.transform;
            CreateCapsule(side + "ThumbMesh", thumb.transform, new Vector3(sideSign * -0.015f, -0.015f, 0.015f), new Vector3(0.02f, 0.03f, 0.02f), accentMat, jointRends);

            // Dedos articulados (Índice, Medio, Anular)
            var fingers = new List<Transform>();
            for (int f = 0; f < 3; f++)
            {
                float xOffset = (f - 1) * 0.018f * sideSign;
                var finger = CreateJoint($"{side}Finger_{f}", hand.transform, new Vector3(xOffset, -0.06f, 0));
                CreateCapsule($"{side}FingerMesh_{f}", finger.transform, new Vector3(0, -0.02f, 0), new Vector3(0.018f, 0.035f, 0.018f), accentMat, jointRends);
                fingers.Add(finger.transform);
            }

            if (isLeft) humanoid.leftFingers = fingers.ToArray();
            else humanoid.rightFingers = fingers.ToArray();
        }

        private static void BuildLeg(
            bool isLeft,
            Transform hips,
            ArticulatedHumanoid humanoid,
            Material bodyMat,
            Material jointMat,
            Material accentMat,
            List<Renderer> bodyRends,
            List<Renderer> jointRends)
        {
            string side = isLeft ? "Left" : "Right";
            float sideSign = isLeft ? -1f : 1f;

            // Cadera / Muslo (pivote lateral en la pelvis)
            var thigh = CreateJoint(side + "Thigh", hips, new Vector3(sideSign * 0.10f, -0.08f, 0));
            if (isLeft) humanoid.leftThigh = thigh.transform;
            else humanoid.rightThigh = thigh.transform;

            CreateSphere(side + "HipJoint", thigh.transform, Vector3.zero, 0.10f, jointMat, jointRends);
            CreateCapsule(side + "ThighMesh", thigh.transform, new Vector3(0, -0.20f, 0), new Vector3(0.11f, 0.20f, 0.11f), bodyMat, bodyRends);

            // Rodilla / Pantorrilla (pivote a 0.42m debajo de la cadera)
            var calf = CreateJoint(side + "Calf", thigh.transform, new Vector3(0, -0.42f, 0));
            if (isLeft) humanoid.leftCalf = calf.transform;
            else humanoid.rightCalf = calf.transform;

            CreateSphere(side + "KneeJoint", calf.transform, Vector3.zero, 0.09f, jointMat, jointRends);
            CreateCapsule(side + "CalfMesh", calf.transform, new Vector3(0, -0.20f, 0), new Vector3(0.095f, 0.20f, 0.095f), bodyMat, bodyRends);

            // Tobillo / Pie (pivote a 0.40m debajo de la rodilla)
            var foot = CreateJoint(side + "Foot", calf.transform, new Vector3(0, -0.40f, 0));
            if (isLeft) humanoid.leftFoot = foot.transform;
            else humanoid.rightFoot = foot.transform;

            CreateSphere(side + "AnkleJoint", foot.transform, Vector3.zero, 0.075f, jointMat, jointRends);
            // Pie extendido hacia adelante en Z
            CreateBox(side + "FootMesh", foot.transform, new Vector3(0, -0.035f, 0.06f), new Vector3(0.10f, 0.07f, 0.20f), bodyMat, bodyRends);

            // Punta del pie
            var toe = CreateJoint(side + "Toe", foot.transform, new Vector3(0, -0.035f, 0.16f));
            if (isLeft) humanoid.leftToe = toe.transform;
            else humanoid.rightToe = toe.transform;
            CreateBox(side + "ToeMesh", toe.transform, new Vector3(0, 0, 0.02f), new Vector3(0.09f, 0.05f, 0.05f), accentMat, jointRends);
        }

        private static GameObject CreateJoint(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, List<Renderer> rendList)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            rendList.Add(mr);
            return go;
        }

        private static GameObject CreateSphere(string name, Transform parent, Vector3 localPos, float diameter, Material mat, List<Renderer> rendList)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * diameter;
            var mr = go.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            rendList.Add(mr);
            return go;
        }

        private static GameObject CreateCapsule(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, List<Renderer> rendList)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            rendList.Add(mr);
            return go;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, List<Renderer> rendList)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            rendList.Add(mr);
            return go;
        }
    }
}
