# Reconstrucción Funcional 3D — Interior UTOP, Patio de Honor y Calle Junín (V3.5 Mejorada)

> [!IMPORTANT]
> Se han solucionado todos los problemas estructurales, de colisión y de detalle interior en [`BaseUTOP.unity`](file:///C:/Users/senol/SimuladorBloqueos/Assets/_Project/Scenes/BaseUTOP/BaseUTOP.unity):
> 1. **Cero Parpadeo (`Z-Fighting`):** Se separaron las cotas de la cimentación (`-0.65m`), la losa del Garaje (`-0.30m`), la losa del Patio de Honor (`-0.30m` con junta de dilatación sin solapamiento) y el emblema circular (`+0.025m`).
> 2. **Gradas 100% Transitables y con Barandas Continuas:** Se eliminaron las vigas que atravesaban la escalera, se corrigió el ángulo de la rampa de colisión (`-35.5°`), se creó un **descanso superior amplio de `3.2m`** y se instalaron **barandas de madera caoba y balaustres de hierro** a lo largo de todo el pasillo y en ambos lados de las gradas.
> 3. **Puertas Reales que se Abren y Cierran (`[E]`):** Todas las puertas de oficinas, la puerta principal del baño y las puertas de los 3 cubículos sanitarios tienen bisagra real y el componente [`InteractiveDoor.cs`](file:///C:/Users/senol/SimuladorBloqueos/Assets/_Project/Scripts/Environment/InteractiveDoor.cs).
> 4. **Baños Reales Opacos con Azulejos, Inodoros de Porcelana y Lavamanos con Espejo:** Paredes sólidas opacas revestidas en azulejo cerámico ([`Tex_Bathroom_Tile.png`](file:///C:/Users/senol/SimuladorBloqueos/Assets/_Project/Art/Textures/CalleJunin/Tex_Bathroom_Tile.png)), suelo de azulejo, 3 lavamanos cóncavos de porcelana con grifo curvo cromado y gran espejo reflectante, y 3 cubículos privados con inodoros esculpidos en 3D (pedestal curvo, taza ovalada, agua, asiento, tapa levantada, tanque cisterna y botón cromado).
> 5. **Sillas, Sillones y Mesas Esculpidas:** Sillones ejecutivos giratorios con base estrella de 5 ruedas, cojín anatómico y respaldo curvo en S con cabecero, más sillas ergonómicas de visita/reuniones y sofá de cuero.

---

## 1. Galería Visual de las Mejoras Estructurales y de Mobiliario (Unity 6 URP)

````carousel
![1er Piso Redistribuido: Pasillo Amplio, Baranda Continua, Gradas Despejadas y Puerta Opaca de Baños](C:/Users/senol/.gemini/antigravity/brain/978c045c-4783-423a-984e-32e4427141ea/utop_primer_piso_mamparas_y_gradas.png)
<!-- slide -->
![Baños Reales Opacos: Suelo y Paredes de Azulejo, Lavamanos Cóncavos con Espejo, Cubículos con Puertas e Inodoros de Porcelana](C:/Users/senol/.gemini/antigravity/brain/978c045c-4783-423a-984e-32e4427141ea/utop_banos_azulejos_e_inodoros.png)
<!-- slide -->
![Sala de Operaciones: Mesa Biselada y 6 Sillas Ergonómicas Esculpidas](C:/Users/senol/.gemini/antigravity/brain/978c045c-4783-423a-984e-32e4427141ea/utop_sala_operaciones_sillas.png)
<!-- slide -->
![Oficina del Capitán UTOP: Sillón Ejecutivo Esculpido, Escritorio con Inlay de Cuero, Banderas 3D y Cuadro](C:/Users/senol/.gemini/antigravity/brain/978c045c-4783-423a-984e-32e4427141ea/utop_oficina_capitan.png)
<!-- slide -->
![Patio de Honor UTOP Sin Parpadeo (Cero Z-Fighting)](C:/Users/senol/.gemini/antigravity/brain/978c045c-4783-423a-984e-32e4427141ea/utop_patio_de_honor.png)
<!-- slide -->
![Armería Antidisturbios UTOP en el Garaje Nivelado](C:/Users/senol/.gemini/antigravity/brain/978c045c-4783-423a-984e-32e4427141ea/utop_armeria_antidisturbios.png)
<!-- slide -->
![Cuadra de Tropa: Literas Dobles Metálicas y Roperos Individuales](C:/Users/senol/.gemini/antigravity/brain/978c045c-4783-423a-984e-32e4427141ea/utop_cuadra_literas_y_roperos.png)
<!-- slide -->
![Calle Junín: Toyota Caldina Wagon, VW Peta y Minibús Estacionados al Lateral](C:/Users/senol/.gemini/antigravity/brain/978c045c-4783-423a-984e-32e4427141ea/junin_autos_laterales_y_garaje.png)
````

---

## 2. Resumen Técnico de las Correcciones Aplicadas

| Problema Detectado | Solución Arquitectónica y de Código Implementada |
| :--- | :--- |
| **Parpadeo (`Z-Fighting`) en el Patio de Honor y Garaje** | Se bajó la cimentación profunda (`Compound_Deep_Foundation`) `0.35m` por debajo del nivel de piso y se separaron exactamente los límites en `Z` de `Garage_Level_Floor` (`Z = -2.35m a +10.0m`) y `Patio_Concrete_Floor` (`Z = -34.50m a -2.38m`). |
| **Bloqueo al bajar por las gradas y falta de barandas** | Se eliminaron las vigas casetonadas sobre el área de la escalera, se amplió el descanso superior a **`3.20m` libres**, se invirtió la inclinación de `Stair_Smooth_Collider` a **`-35.53°`** para coincidir con la bajada y se construyó la **baranda continua de caoba y hierro** en todo el borde del 1er piso y en ambos lados de las gradas. |
| **Paredes del baño de cristal y falta de azulejos** | Se reemplazó el cerramiento del baño (`X = +19.80m`) por **muros 100% sólidos y opacos**, revestidos por dentro con azulejo cerámico brillante y piso de azulejo con juntas ([`Tex_Bathroom_Tile.png`](file:///C:/Users/senol/SimuladorBloqueos/Assets/_Project/Art/Textures/CalleJunin/Tex_Bathroom_Tile.png)). |
| **Inodoros, lavamanos y puertas reales en el baño** | Se esculpieron en [`ProceduralMeshBuilder.cs`](file:///C:/Users/senol/SimuladorBloqueos/Assets/_Project/Scripts/Environment/Editor/ProceduralMeshBuilder.cs) mallas multi-polígono de **inodoros de porcelana** (pedestal contorneado, taza cóncava con agua, asiento, tapa levantada, tanque y botón cromado) y **3 lavamanos cóncavos** con grifo cuello de cisne, dispensadores de jabón, secador de manos y gran espejo reflectante. |
| **Puertas interactivas que se abren y cierran** | Se creó [`InteractiveDoor.cs`](file:///C:/Users/senol/SimuladorBloqueos/Assets/_Project/Scripts/Environment/InteractiveDoor.cs): al acercarse a cualquier puerta de oficina, baño o cubículo sanitario y presionar **`[E]`**, la hoja gira suavemente sobre sus bisagras. |
| **Sillas, sillones y mesas más realistas** | Se añadieron en [`ProceduralMeshBuilder.cs`](file:///C:/Users/senol/SimuladorBloqueos/Assets/_Project/Scripts/Environment/Editor/ProceduralMeshBuilder.cs) los generadores `BuildSculptedExecutiveChair` (base estrella de 5 puntas con ruedas dobles, pistón neumático, asiento anatómico y respaldo en S con cabecero) y `BuildSculptedOfficeVisitorChair`, además de tableros de mesa biselados con inserciones de cuero y sofá ejecutivo de 2 cuerpos. |
