# Simulador de Conflictos y Bloqueos (Base UTOP - Calle Junín)

Simulador interactivo y pedagógico desarrollado en **Unity 6 (URP)** para la toma de decisiones tácticas y operativas policiales ante escenarios de conflictividad social y bloqueos en la Unidad Táctica de Operaciones Policiales (UTOP) y Calle Junín (La Paz, Bolivia).

---

## 📋 Características Principales

1. **Sistema de 13 Decisiones Tácticas Secuenciales**:
   - Flujo pedagógico completo desde la recepción de la orden en el Despacho del Capitán hasta el desescalamiento y evaluación final.
   - Ponderación de decisiones con métricas en tiempo real: Integridad física, Legalidad y DD.HH., Administración de efectivos, Equipamiento no letal y Coordinación.
   - Panel interactivo uGUI con soporte táctil, clic con ratón y atajos de teclado numéricos `[1]`, `[2]`, `[3]`, `[4]`.

2. **Entorno y Arquitectura 3D Fidedigna**:
   - Edificio de la UTOP modelado a escala real:
     - **Planta Alta**: Despacho del Capitán, Sala de Operaciones/Crisis, Secretaría, baños reglamentarios con sanitarios y espejos.
     - **Planta Baja**: Patio de Honor con emblema institucional, Cuadra de Tropa 1 y Sala de Armas (Armería).
     - **Exterior**: Pendiente real de la Calle Junín, pórtico de acceso, vehículos institucionales, vehículos civiles y casas coloniales paceñas.

3. **Tropa UTOP (30 Efectivos Policiales)**:
   - Formación reglamentaria en bloque central de 3 filas x 10 columnas (`3x10`) frente al emblema UTOP.
   - Vida casual en cuadra (oficiales sentados en bancas, de pie, patrullando, ejecutando saludo militar al aproximarse).
   - Equipamiento funcional 3D: Escudos antimotines transparentes en guardia frontal, bastones policiales (tonfas), lanzagases de 37mm y escopetas sostenidas a dos manos.

4. **Manifestantes y Control de Masas**:
   - Manifestantes con modelos y vestimentas autóctonas: **Mineros**, **Cholitas** y **Ponchos Rojos**.
   - Comportamiento de asamblea y protesta a la altura del vehículo rojo en Calle Junín (`Z = 24.5m..28m`).
   - Lanzamiento esporádico de piedras y dinamita según el nivel de escalamiento y decisiones tácticas adoptadas.
   - Reacción y repliegue de manifestantes ante el uso reglamentario de gas lacrimógeno.

5. **Sistema de Inventario del Jugador (8 Ranuras)**:
   - Selección rápida de equipo reglamentario:
     - `1`: Escudo antimotines (alternancia entre postura de descanso y bloqueo frontal).
     - `2`: Tonfa policial (golpe y guardia).
     - `3`: Gas lacrimógeno en spray (rociador de mano con medidor de capacidad `0-100%`).
     - `4`: Lanzagases de 37mm (balística parabólica con gravedad y dispersión de nube).
     - `5`: Munición de gas (cartuchos de reserva y recarga con tecla `[R]`).
     - `6`: Escopeta con postas de goma.
     - `7`: Barricada policial (colocación táctica en el terreno).
     - `8`: Barrera grande de contención.
   - Reabastecimiento de suministros en el interior de la Sala de Armas.

6. **Cámara Dual y Locomoción**:
   - Alternancia fluida entre 1ra Persona y 3ra Persona con la tecla `[V]`.
   - Marcha táctica reglamentaria por defecto, sprint con `[Shift]`, salto con `[Espacio]`, rodar con `[Q]`, posición de firmes con `[F]`, sentarse con `[2]` o `[Z]` frente a sillas reales.
   - Caminado de herido automático cuando la salud desciende por debajo del 15% y caída por abatimiento al 0%.

---

## 📂 Estructura del Repositorio

- `Assets/`: Scripts, modelos 3D (`.fbx`), texturas, animaciones procesadas, prefabs y escenas Unity.
  - `Assets/_Project/Scenes/BaseUTOP/BaseUTOP.unity`: Escena principal del simulador.
  - `Assets/Scripts/Decisions/`: Lógica del árbol de decisiones, evaluadores, historial y métricas.
  - `Assets/Scripts/Scenario/`: Director de escena, eventos dinámicos y control de manifestantes.
- `Packages/`: Manifiesto de paquetes Unity (URP, TextMeshPro, Input System, Mathematics).
- `ProjectSettings/`: Configuración gráfica, tags, layers, input y físicas del proyecto Unity.
- `Docs/`: Documentación técnica e historial de desarrollo del simulador (`historial_conversacion.jsonl`).

---

## 🛠 Requisitos para Colaboradores

- **Motor**: Unity 6 (6000.x o superior) con **Universal Render Pipeline (URP)**.
- **Git LFS**: Asegúrese de tener Git LFS instalado (`git lfs install`) para el manejo óptimo de assets y modelos 3D.
- **Plataforma**: PC / Windows (DirectX 11/12).
