# INFORME TÉCNICO FASE 2 — CONSTRUCCIÓN DEL ENTORNO 3D BASE UTOP

**Proyecto:** Simulador de Bloqueos  
**Fase:** Fase 2 — Entorno 3D Base UTOP  
**Motor:** Unity 6 (`6000.6.8f1`) | **Pipeline:** Universal Render Pipeline (URP 17.6.0) con Render Graph  
**Estado:** Construcción, pruebas e integración finalizadas exitosamente.

---

## 1. Resumen Ejecutivo de la Entrega

Se ha diseñado y construido la escena central independiente `Assets/_Project/Scenes/BaseUTOP/BaseUTOP.unity`. Representa un complejo institucional moderno, creíble y sobrio de dos plantas con explanada exterior, diseñado específicamente para la formación y evaluación táctica de la Policía Boliviana (UTOP).

El entorno es 100% navegable en tercera persona, cuenta con proporciones a escala humana real, colisiones sólidas en toda su geometría (muros, mobiliario, barandillas y rampa física para escaleras), 4 zonas de interacción por proximidad con avisos contextuales mediante el nuevo Input System (tecla [E]), e integración bidireccional con el Menú Principal y el `GameSessionManager`.

---

## 2. Distribución Arquitectónica

El edificio cuenta con una huella de **36 metros de ancho por 24 metros de profundidad**, con una altura total de **8.2 metros** distribuida en dos niveles de 3.8 metros cada uno más losa técnica de entrepiso y cubierta.

### Detalle de Ambientes

1. **Patio Exterior y Explanada de Formación (50m × 32m):**
   - Pavimento de asfalto oscuro sobrio con vereda de aproximación de hormigón.
   - Asta monumental con estandarte institucional.
   - Muros perimetrales protectores y portón de control de acceso.
   - Jardineras lineales de contención con setos verdes.
   - Luminarias de exterior con conos de luz focal hacia el acceso.

2. **Planta Baja:**
   - **Recepción:** Mostrador institucional en L, estación informática con pantalla táctica, sillón y punto de control.
   - **Sala de Espera:** Juego de sofás tapizados en tela azul noche y mesa baja de atención.
   - **Pasillo Central:** Distribuidor amplio con luminarias LED empotradas en cielo raso.
   - **Sala de Reuniones Tácticas:** Gran mesa de conferencias con capacidad para 10 oficiales, pantalla panorámica táctica en muro frontal y pizarra de coordinación.
   - **Aula de Formación Especializada:** Podio elevado para el instructor, gran pantalla de proyección de derechos humanos y contención pacífica, y 3 filas de pupitres dobles con asientos individuales.
   - **Servicios Higiénicos (SS.HH.):** Área delimitada con señalética en español.
   - **Escalera Central:** 18 peldaños con contrahuella exacta, barandilla de protección en acero cromado y **rampa física oculta de colisión continua**, permitiendo al `CharacterController` ascender y descender sin tropiezos ni temblores de cámara.

3. **Planta Alta:**
   - **Distribuidor y Balcón:** Suelo con barandillas perimetrales de seguridad en vidrio laminado y perfilería cromada que asoman hacia el núcleo de escaleras.
   - **Jefatura de Operaciones UTOP (Oficina de Mando):** Acabado de suelo en parquet noble, gran escritorio ejecutivo presidencial, sillón ergonómico de mando, asientos de visita, estantería de archivo normativo y placa dorada institucional de mando.
   - **Oficinas Administrativas:** 3 módulos de trabajo equipados con mesas, ordenadores de monitoreo, asientos y archivadores metálicos.
   - **Área de Descanso / Coordinación:** Espacio reservado para descansos de guardia y preparación operativa.

---

## 3. Puntos de Interacción Táctica (Input System)

Se ha implementado el componente modular `InteractableZone.cs` junto al gestor visual `InteractionHUD.cs`. Al ingresar al radio de proximidad del punto, aparece en la parte inferior de la pantalla un indicador estilizado; al pulsar **[E]**, se despliega el modal táctico informativo:

| Punto de Interacción | Ubicación | Indicador en Pantalla | Contenido Contextual |
| :--- | :--- | :--- | :--- |
| **Recepción** | Planta Baja (X: -4.0, Z: 1.8) | `[E] PUESTO DE CONTROL Y RECEPCIÓN` | *Puesto de Control y Recepción de la Base UTOP. Registro obligatorio de ingresos, acreditaciones y orientación de áreas.* |
| **Sala de Reuniones** | Planta Baja (X: 6.5, Z: 3.0) | `[E] SALA DE REUNIONES TÁCTICAS` | *Espacio de coordinación para oficiales y jefes de escuadra. Planificación de despliegues preventivos y análisis de bloqueos.* |
| **Aula de Formación** | Planta Baja (X: 10.0, Z: 11.0) | `[E] AULA DE FORMACIÓN ESPECIALIZADA` | *Centro de capacitación en DDHH aplicados a la función policial, resolución pacífica de conflictos y técnicas no letales.* |
| **Oficina de Mando** | Planta Alta (X: 10.0, Z: 3.5) | `[E] JEFATURA DE OPERACIONES UTOP` | *Área de acceso restringido. Despacho del Mando Operativo para impartir directivas y autorizaciones tácticas.* |

---

## 4. Jugador, Físicas y Navegación

* **Modelo Articulado Reutilizado:** Se integró el generador `HumanoidModelBuilder`, conservando el esqueleto funcional completo de 15 articulaciones anatómicas.
* **Controlador Biomecánico:** `PlayerController` con `CharacterController` configurado con radio 0.32m, altura 1.85m, `stepOffset` de 0.35m y `slopeLimit` de 45°.
* **Velocidades:** Caminata a 2.6 m/s con WASD y carrera (Sprint) a 6.2 m/s manteniendo `LeftShift`.
* **Cámara Orbital:** `ThirdPersonCamera` a una distancia de 3.8m con altura de hombros a 1.4m, rotación suave con ratón, bloqueo de cursor y tecla `[Esc]` para alternar el control del cursor.
* **Aparición Inicial:** El oficial aparece en la explanada exterior frontal en las coordenadas `(0, 0.05, -14)`, orientado en línea recta hacia el pórtico de entrada de la Base.

---

## 5. HUD de Simulación e Integración de Sesión

1. **Banner Superior Izquierdo (`SimulationBaseHUD.cs`):**
   - Muestra dinámicamente el Grado, Nombre Completo, Código de Identificación y Unidad del participante activo tomados desde `GameSessionManager.Instance.activeParticipant`.
   - Si se inicia la escena de forma aislada, muestra el perfil por defecto *"Subt. Oficial en Prácticas | POL-2026-UTOP"*.
2. **Botón de Salida (`Volver al Menú`):**
   - Ubicado en la esquina superior derecha, libera el cursor y retorna de inmediato a `MainMenu.unity`.
3. **Guía de Controles:**
   - Panel discreto en la esquina inferior derecha con recordatorios de teclas (WASD, Shift, Ratón, E, Esc).

---

## 6. Flujo y Configuración de Escenas (Build Settings)

Se actualizaron los Build Settings del proyecto con el orden exacto solicitado:

| Índice | Escena | Ruta en Proyecto | Propósito |
| :---: | :--- | :--- | :--- |
| **0** | `MainMenu` | `Assets/_Project/Scenes/MainMenu/MainMenu.unity` | Menú inicial y registro de participantes |
| **1** | `BaseUTOP` | `Assets/_Project/Scenes/BaseUTOP/BaseUTOP.unity` | **Entorno central de operaciones de la UTOP** |
| **2** | `SampleScene` | `Assets/Scenes/SampleScene.unity` | Escena de pruebas y prototipo anterior (100% conservada) |
