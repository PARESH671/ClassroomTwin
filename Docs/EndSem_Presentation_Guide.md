# End-Semester Presentation Guide & Project Summary
> **Project:** Classroom Digital Twin with Fuzzy Co-Regulation  
> **Source Document:** `ClassroomTwin_EndSem_Build_Spec.pdf` (doc2)  
> **Target Audience:** Project Team Members (Use this structured outline to make the final PPT slides)

---

## 🎯 10-Slide PPT Blueprint for End-Sem Presentation

### Slide 1: Title & Team Details
- **Title:** Classroom Digital Twin: Fuzzy Lighting Control & Smart Window-Curtain Co-Regulation
- **Subtitle:** End-Semester Minor Project Implementation & Experimental Validation
- **Engine:** Unity 6 3D Simulation + Pure C# Deterministic Engine
- **Team Members & Mentor Details**

---

### Slide 2: The Problem & Motivation (Why Mid-Sem Was Incomplete)
- **Mid-Semester Limitation:**
  - Evaluated basic on/off timers and static dimming (MDPI 2022).
  - Ignored solar heat gain: unshaded windows allow massive infrared radiation into classrooms.
  - Classroom lights dimming without blind control leads to severe glare or overheated rooms, causing the AC cooling load to skyrocket.
- **End-Semester Solution:**
  - Simultaneously co-regulate **12 smart LED fixtures** AND **30 motorized venetian blinds** using AI (Mamdani Fuzzy Inference Systems).
  - Balance **Visual Comfort (target 500 lux, glare-free)** with **Thermal Comfort (minimizing AC cooling load)**.

---

### Slide 3: Research Foundations & Architecture
- **Paper 1 (Geometry):** MDPI *Buildings* 2022 — Figure 5 Type B Classroom (11.5 m × 9.0 m × 3.0 m, 12 fixtures in 3 rows: Window, Center, Corridor).
- **Paper 2 (Lighting FIS):** IEEE CCIS 2024 (Singhai et al.) — 27 Mamdani rules mapping Light, Time Slot, and Temperature to LED PWM dimming.
- **Paper 3 (Curtain FIS):** IEEE *Internet of Things Journal* 2026 (Singhai et al.) — 18 Mamdani rules mapping Daylight, Indoor Temp, AQI, Time Slot, and Slat Angle to motorized blind rotation (0° to 75°).

---

### Slide 4: 3D Physical Digital Twin Model (Unity 6)
- **Classroom Structure:**
  - Real dimensions: 11.5 m length, 9.0 m width, 3.0 m ceiling height.
  - 12 LED ceiling fixtures with real-time emissive materials and photometric spotlights.
  - 3 large window bays with **30 interior venetian blind slats** (Slat depth = 0.17 m, pitch = 0.16 m) driven by simulated MG996R servos.
  - Dynamic directional Sunlight entering through the window wall (+Z vector).

---

### Slide 5: Mathematical & Physical Formulation
1. **Daylight Factor Model:**
   - Window row = 5.0%, Center row = 2.0%, Corridor row = 0.8% of exterior diffuse daylight.
2. **Venetian Blind Transmittance Equation:**
   $$\tau(\theta) = 1.0 - \text{Coverage}(\theta) \times (1.0 - \tau_{\text{slat}})$$
   - $\theta = 0^\circ$ (Open) $\rightarrow \tau = 1.00$ (100% transmission)
   - $\theta = 75^\circ$ (Closed) $\rightarrow \tau = 0.05$ (5% transmission)
3. **Explicit Euler Thermal Integration:**
   $$T_{\text{in}}(t + \Delta t) = T_{\text{in}}(t) + \frac{T_{\text{eq}} - T_{\text{in}}}{\tau_{\text{thermal}}} \Delta t$$
4. **Indoor Air Quality (AQI) Dynamics:**
   - Outdoor baseline + 60 AQI gain when occupied (respiratory $CO_2$ / particulate accumulation).

---

### Slide 6: Dual Mamdani Fuzzy Inference Controllers
- **Light FIS (CCIS 2024 Table I — 27 Rules):**
  - Inputs: `Light` (Low, Medium, High), `Slot` (Sleep, Work, Relax), `Temp` (Cold, Warm, Hot).
  - Output: `Artificial` PWM level (Low 20%, Medium 50%, High 80%) via discrete centroid defuzzification.
- **Curtain FIS (IoT-J 2026 Table I — 18 Rules):**
  - Inputs: `Light` (0-100%), `Temp` (0-40°C), `AQI` (Clean/Poor), `Slot` (0-24h), `PrevAngle` (0-75°).
  - Output: New Slat Angle (Open 0°, Partial 45°, Closed 75°).
  - Handles hold states and anti-glare thermal defense (Rule R12).

---

### Slide 7: Complete Scenario Taxonomy (S0 to S6)

| Scenario | Strategy Name | Type | Description |
| :--- | :--- | :--- | :--- |
| **S0** | Baseline | Legacy | 24/7 ON at 100% power (780 W), unshaded windows |
| **S1** | Manager Cutoff | Timer | Lights ON only 09:00 - 18:00 |
| **S2** | PIR Occupancy | Motion Sensor | Lights ON when occupied + 10-minute hold |
| **S3** | Uniform Dimming | Quantitative | All fixtures fixed at 34 W (500 lux design level) |
| **S4** | Static Daylight | Zonal On/Off | Window row OFF from 09:00 - 13:00 |
| **S5** | Fuzzy Lighting (CCIS 2024) | Fuzzy AI | Independent 3-row dimming based on daylight & temp |
| **S6** | **Fuzzy Co-Regulation (IoT-J 2026)** | **Dual Fuzzy AI** | **Simultaneous adaptive blinds + LED dimming** |

---

### Slide 8: Experimental Results & Benchmarks (Use in Slides!)

#### 1. Normal Weather Day (T_mean = 24 °C, Exterior = 20,000 lux)
- **Lighting Energy:** S0 = 18.72 kWh $\rightarrow$ S6 = **3.07 kWh** (**83.6% Lighting Savings**).
- **Cooling Load:** S0 = 9.61 kWh $\rightarrow$ S6 = **5.95 kWh** (**38.1% AC Load Savings**).
- **Total Energy:** S0 = 28.33 kWh $\rightarrow$ S6 = **9.02 kWh** (**68.2% Total Energy Reduction**).
- **Visual Comfort ($F_c$):** **0.985 / 1.00** (94.4% of occupied hours inside ideal 400-2000 lux band).

#### 2. Heatwave Weather Day (T_mean = 34 °C, Exterior = 25,000 lux)
- S6 activates thermal defense (blinds hold at ~62° to block solar heat).
- Peak indoor temperature drops from **42.06 °C (S0) to 38.43 °C (S6)**.
- Cooling energy drops from **12.44 kWh to 2.53 kWh** (**79.6% Cooling Energy Saved!**).

#### 3. Pollution Weather Day (Outdoor AQI = 250+ Poor)
- Rules R8 and R9 force blinds to **Partial (45°)** to limit particulate influx.
- LEDs compensate to maintain exact reading illuminance ($\ge 500\text{ lux}$).

---

### Slide 9: Live Demo Script for Evaluators / Viva (4-Minute Demo)
1. **Show Scene & Architecture:** Point out 30 venetian slats, 12 LEDs across 3 rows, sun direction.
2. **Run Self-Test:** Run `Digital Twin > Run Self-Test` from menu — show **"SELF-TEST PASSED (98 checks)"** to prove mathematical rigor.
3. **Press Play:**
   - Press **`2`**: PIR Baseline (shows lights turning ON 100% — mid-sem status).
   - Press **`5`**: CCIS 2024 (independent row dimming: Window ~20% vs Corridor ~50%).
   - Press **`6`**: IoT-J 2026 (co-regulation: blinds rotate to track daylight, LEDs compensate).
   - Press **`T`**: Heatwave mode (blinds close to 62° to block solar heat, peak temp drops 3.6 °C).
   - Press **`Q`**: Pollution mode (rules R8/R9 force blinds to Partial for air defense).
   - Press **`B`**: 24-hour comparative table popup overlay.

---

### Slide 10: Critical Analysis, Limitations & Future Scope
- **Engineering Limitation Identified in IEEE CCIS 2024:**
  - The paper uses 3 coarse fuzzy sets (`Low`, `Medium`, `High`).
  - Because 0-250 lux is bucketed into `Low`, both 194 lux (window) and 31 lux (corridor) get flat 50% power during partial blind coverage.
- **Future Improvement:**
  - Implement a 5-level fine-grained FIS (`Very Low`, `Low`, `Medium`, `High`, `Very High`) or continuous closed-loop proportional feedback ($500 - E_{\text{day}}$) to save an extra ~8-10% energy.
  - Physical IoT deployment via ESP32 microcontrollers and MQTT bridge for bidirectional hardware twin sync.
