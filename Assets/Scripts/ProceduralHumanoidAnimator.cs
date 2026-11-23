using UnityEngine;

namespace SimuladorBloqueos
{
    /// <summary>
    /// Controla la animación procedural biomecánica completa de un humanoide articulado:
    /// Caminar, correr (sprint) e idle con cinemática natural para todas las articulaciones.
    /// </summary>
    public class ProceduralHumanoidAnimator : MonoBehaviour
    {
        [Header("Referencias")]
        public ArticulatedHumanoid humanoid;

        [Header("Velocidad y Estados")]
        [Range(0f, 1f)] public float normalizedSpeed = 0f; // 0 = Parado, 0.45 = Caminando, 1.0 = Corriendo
        public bool isGrounded = true;

        [Header("Configuración de Marcha")]
        public float walkFrequency = 2.1f;
        public float runFrequency = 3.3f;
        public float walkThighSwing = 26f;
        public float runThighSwing = 48f;
        public float walkKneeBend = 42f;
        public float runKneeBend = 72f;
        public float walkArmSwing = 28f;
        public float runArmSwing = 58f;
        public float walkElbowBend = 35f;
        public float runElbowBend = 70f;
        public float walkPelvisBob = 0.04f;
        public float runPelvisBob = 0.08f;
        public float runForwardLean = 14f;

        // Fases internas
        private float _gaitPhase = 0f;
        private float _idlePhase = 0f;
        private Vector3 _initialHipsLocalPos;

        // Rotaciones iniciales de referencia
        private Quaternion _initHipsRot;
        private Quaternion _initWaistRot;
        private Quaternion _initChestRot;
        private Quaternion _initNeckRot;
        private Quaternion _initHeadRot;

        private Quaternion _initLShoulderRot, _initRShoulderRot;
        private Quaternion _initLUpperArmRot, _initRUpperArmRot;
        private Quaternion _initLForearmRot, _initRForearmRot;
        private Quaternion _initLWristRot, _initRWristRot;

        private Quaternion _initLThighRot, _initRThighRot;
        private Quaternion _initLCalfRot, _initRCalfRot;
        private Quaternion _initLFootRot, _initRFootRot;

        private void Awake()
        {
            if (humanoid == null) humanoid = GetComponent<ArticulatedHumanoid>();
            CaptureInitialPoses();
        }

        public void CaptureInitialPoses()
        {
            if (humanoid == null || humanoid.hips == null) return;

            _initialHipsLocalPos = humanoid.hips.localPosition;
            _initHipsRot = humanoid.hips.localRotation;
            _initWaistRot = humanoid.waist.localRotation;
            _initChestRot = humanoid.chest.localRotation;
            _initNeckRot = humanoid.neck.localRotation;
            _initHeadRot = humanoid.head.localRotation;

            _initLShoulderRot = humanoid.leftShoulder.localRotation;
            _initRShoulderRot = humanoid.rightShoulder.localRotation;
            _initLUpperArmRot = humanoid.leftUpperArm.localRotation;
            _initRUpperArmRot = humanoid.rightUpperArm.localRotation;
            _initLForearmRot = humanoid.leftForearm.localRotation;
            _initRForearmRot = humanoid.rightForearm.localRotation;
            _initLWristRot = humanoid.leftWrist.localRotation;
            _initRWristRot = humanoid.rightWrist.localRotation;

            _initLThighRot = humanoid.leftThigh.localRotation;
            _initRThighRot = humanoid.rightThigh.localRotation;
            _initLCalfRot = humanoid.leftCalf.localRotation;
            _initRCalfRot = humanoid.rightCalf.localRotation;
            _initLFootRot = humanoid.leftFoot.localRotation;
            _initRFootRot = humanoid.rightFoot.localRotation;
        }

        private void LateUpdate()
        {
            if (humanoid == null || humanoid.hips == null) return;

            // Suavizado del factor de velocidad
            float speed = Mathf.Clamp01(normalizedSpeed);
            bool isMoving = speed > 0.03f;

            // Avance de fases
            float currentFreq = Mathf.Lerp(walkFrequency, runFrequency, Mathf.InverseLerp(0.45f, 1f, speed));
            if (isMoving)
            {
                _gaitPhase += currentFreq * Mathf.PI * 2f * Time.deltaTime * Mathf.Max(speed, 0.25f);
                if (_gaitPhase > Mathf.PI * 2f) _gaitPhase -= Mathf.PI * 2f;
            }
            _idlePhase += 1.8f * Time.deltaTime;

            // Factores interpolados entre caminar y correr
            float runWeight = Mathf.InverseLerp(0.45f, 1f, speed);
            float thighSwingMax = Mathf.Lerp(walkThighSwing, runThighSwing, runWeight);
            float kneeBendMax = Mathf.Lerp(walkKneeBend, runKneeBend, runWeight);
            float armSwingMax = Mathf.Lerp(walkArmSwing, runArmSwing, runWeight);
            float elbowBendMax = Mathf.Lerp(walkElbowBend, runElbowBend, runWeight);
            float pelvisBobMax = Mathf.Lerp(walkPelvisBob, runPelvisBob, runWeight);
            float forwardLean = Mathf.Lerp(3f, runForwardLean, runWeight) * speed;

            // ==========================================
            // 1. PELVIS / CADERAS (Rebote vertical, oscilación lateral y giro)
            // ==========================================
            float verticalBob = 0f;
            float hipRoll = 0f;
            float hipYaw = 0f;

            if (isMoving)
            {
                // Rebote doble por ciclo (dos pasos por ciclo de marcha)
                verticalBob = -Mathf.Abs(Mathf.Sin(_gaitPhase)) * pelvisBobMax * speed;
                // Inclinación lateral de la pelvis hacia la pierna de apoyo
                hipRoll = Mathf.Sin(_gaitPhase) * 3.5f * speed;
                // Giro de pelvis hacia adelante con la pierna que avanza
                hipYaw = Mathf.Sin(_gaitPhase) * 6f * speed;
            }
            else
            {
                // Respiración en reposo
                verticalBob = Mathf.Sin(_idlePhase) * 0.006f;
                hipRoll = Mathf.Sin(_idlePhase * 0.5f) * 1f;
            }

            humanoid.hips.localPosition = _initialHipsLocalPos + new Vector3(0, verticalBob, 0);
            humanoid.hips.localRotation = _initHipsRot * Quaternion.Euler(forwardLean * 0.3f, hipYaw, hipRoll);

            // ==========================================
            // 2. CINTURA Y TORSO (Contra-rotación y respiración)
            // ==========================================
            float chestYaw = -hipYaw * 0.75f; // El torso contra-gira para mantener el frente estable
            float chestRoll = -hipRoll * 0.8f;
            float breath = Mathf.Sin(_idlePhase) * 1.5f;

            humanoid.waist.localRotation = _initWaistRot * Quaternion.Euler(forwardLean * 0.4f, chestYaw * 0.5f, chestRoll * 0.5f);
            humanoid.chest.localRotation = _initChestRot * Quaternion.Euler(forwardLean * 0.6f + breath, chestYaw, chestRoll);

            // ==========================================
            // 3. CUELLO Y CABEZA (Estabilización visual)
            // ==========================================
            // La cabeza compensa para mantener la mirada al frente
            float headPitch = -forwardLean * 0.8f + (isMoving ? Mathf.Sin(_gaitPhase * 2f) * 2f * speed : 0);
            humanoid.neck.localRotation = _initNeckRot * Quaternion.Euler(headPitch * 0.4f, -chestYaw * 0.5f, 0);
            humanoid.head.localRotation = _initHeadRot * Quaternion.Euler(headPitch * 0.6f, -chestYaw * 0.5f, 0);

            // ==========================================
            // 4. PIERNAS Y PIES (Cinemática de zancada)
            // ==========================================
            // Pierna Izquierda (fase 0) y Derecha (fase + PI)
            AnimateLeg(true, _gaitPhase, speed, thighSwingMax, kneeBendMax);
            AnimateLeg(false, _gaitPhase + Mathf.PI, speed, thighSwingMax, kneeBendMax);

            // ==========================================
            // 5. BRAZOS, ANTEBRAZOS Y MANOS (Balanceo cruzado natural)
            // ==========================================
            // El brazo izquierdo se balancea adelante cuando la pierna derecha va adelante (fase + PI)
            AnimateArm(true, _gaitPhase + Mathf.PI, speed, armSwingMax, elbowBendMax, breath);
            AnimateArm(false, _gaitPhase, speed, armSwingMax, elbowBendMax, breath);
        }

        private void AnimateLeg(bool isLeft, float phase, float speed, float thighMax, float kneeMax)
        {
            Transform thigh = isLeft ? humanoid.leftThigh : humanoid.rightThigh;
            Transform calf = isLeft ? humanoid.leftCalf : humanoid.rightCalf;
            Transform foot = isLeft ? humanoid.leftFoot : humanoid.rightFoot;
            Quaternion initThigh = isLeft ? _initLThighRot : _initRThighRot;
            Quaternion initCalf = isLeft ? _initLCalfRot : _initRCalfRot;
            Quaternion initFoot = isLeft ? _initLFootRot : _initRFootRot;

            if (speed > 0.03f)
            {
                float sinP = Mathf.Sin(phase);
                float cosP = Mathf.Cos(phase);

                // Movimiento oscilatorio del muslo (Pitch X)
                float thighAngle = -sinP * thighMax * speed;

                // Flexión de rodilla: la rodilla se dobla principalmente en la fase de despegue y avance (sinP > 0)
                float kneeAngle = 0f;
                if (sinP < 0f) // Pierna avanzando
                {
                    kneeAngle = Mathf.Abs(sinP) * kneeMax * speed;
                }
                else // Pierna apoyando/empujando
                {
                    kneeAngle = Mathf.Max(0f, cosP) * (kneeMax * 0.25f) * speed;
                }

                // Tobillo/Pie: se levanta la punta al avanzar (dorsiflexión) y empuja al despegar (plantarflexión)
                float footAngle = 0f;
                if (sinP < -0.2f)
                {
                    footAngle = -14f * speed; // Punta arriba para no arrastrar el pie
                }
                else if (sinP > 0.3f)
                {
                    footAngle = 20f * speed; // Empuje con los dedos al final del paso
                }

                thigh.localRotation = initThigh * Quaternion.Euler(thighAngle, 0, 0);
                calf.localRotation = initCalf * Quaternion.Euler(kneeAngle, 0, 0);
                foot.localRotation = initFoot * Quaternion.Euler(footAngle, 0, 0);
            }
            else
            {
                // Pose de descanso
                thigh.localRotation = Quaternion.Slerp(thigh.localRotation, initThigh, Time.deltaTime * 6f);
                calf.localRotation = Quaternion.Slerp(calf.localRotation, initCalf, Time.deltaTime * 6f);
                foot.localRotation = Quaternion.Slerp(foot.localRotation, initFoot, Time.deltaTime * 6f);
            }
        }

        private void AnimateArm(bool isLeft, float phase, float speed, float armMax, float elbowMax, float breath)
        {
            Transform upperArm = isLeft ? humanoid.leftUpperArm : humanoid.rightUpperArm;
            Transform forearm = isLeft ? humanoid.leftForearm : humanoid.rightForearm;
            Transform wrist = isLeft ? humanoid.leftWrist : humanoid.rightWrist;
            Transform thumb = isLeft ? humanoid.leftThumb : humanoid.rightThumb;
            Transform[] fingers = isLeft ? humanoid.leftFingers : humanoid.rightFingers;

            Quaternion initUpper = isLeft ? _initLUpperArmRot : _initRUpperArmRot;
            Quaternion initForearm = isLeft ? _initLForearmRot : _initRForearmRot;
            Quaternion initWrist = isLeft ? _initLWristRot : _initRWristRot;

            float sideSign = isLeft ? -1f : 1f;

            if (speed > 0.03f)
            {
                float sinP = Mathf.Sin(phase);

                // Balanceo del brazo superior (hacia adelante y ligeramente hacia adentro)
                float armPitch = sinP * armMax * speed;
                float armYaw = sinP * 8f * sideSign * speed;
                float armRoll = 10f * sideSign; // Brazos ligeramente separados del torso

                // Codo: se flexiona más cuando el brazo se balancea hacia adelante
                float elbowPitch = 0f;
                if (sinP > 0f) // Brazo hacia adelante
                {
                    elbowPitch = -Mathf.Lerp(20f, elbowMax, sinP) * speed;
                }
                else // Brazo hacia atrás
                {
                    elbowPitch = -15f * speed;
                }

                // Muñeca: reacción de inercia al movimiento
                float wristPitch = -sinP * 12f * speed;

                upperArm.localRotation = initUpper * Quaternion.Euler(armPitch, armYaw, armRoll);
                forearm.localRotation = initForearm * Quaternion.Euler(elbowPitch, 0, 0);
                wrist.localRotation = initWrist * Quaternion.Euler(wristPitch, 0, 0);

                // Dedos: se cierran más cuando corre (puño relajado)
                float fingerCurl = Mathf.Lerp(15f, 45f, speed);
                if (thumb != null) thumb.localRotation = Quaternion.Euler(0, 0, fingerCurl * 0.6f * sideSign);
                if (fingers != null)
                {
                    foreach (var f in fingers)
                    {
                        if (f != null) f.localRotation = Quaternion.Euler(fingerCurl, 0, 0);
                    }
                }
            }
            else
            {
                // Reposo natural (brazos a los lados, ligera curvatura en codos y dedos)
                Quaternion targetUpper = initUpper * Quaternion.Euler(breath * 0.5f, 0, 8f * sideSign);
                Quaternion targetForearm = initForearm * Quaternion.Euler(-18f, 0, 0);
                Quaternion targetWrist = initWrist * Quaternion.Euler(0, 0, 0);

                upperArm.localRotation = Quaternion.Slerp(upperArm.localRotation, targetUpper, Time.deltaTime * 5f);
                forearm.localRotation = Quaternion.Slerp(forearm.localRotation, targetForearm, Time.deltaTime * 5f);
                wrist.localRotation = Quaternion.Slerp(wrist.localRotation, targetWrist, Time.deltaTime * 5f);

                if (thumb != null) thumb.localRotation = Quaternion.Euler(0, 0, 10f * sideSign);
                if (fingers != null)
                {
                    foreach (var f in fingers)
                    {
                        if (f != null) f.localRotation = Quaternion.Euler(15f, 0, 0);
                    }
                }
            }
        }
    }
}
