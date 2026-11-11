# Classroom Digital Twin — Fuzzy Co-Regulation

An interactive, physics-based 3D **Digital Twin Simulation Ecosystem** for energy-efficient classroom lighting, automated venetian blinds, and thermal comfort co-regulation.

Built with **Unity 6 (6000.6.0f1)** and pure C# deterministic simulation.

---

## 📖 Research Foundations
This implementation integrates methodologies and experimental setups from:
1. **MDPI Buildings (2022)** — *Digital Twin-Based Assessment Framework for Energy Savings in University Classroom Lighting* (Figure 5 Type B Classroom model).
2. **IEEE CCIS (2024)** — *Fuzzy Logic-Based Smart Lighting System* (Table I, 27 Mamdani FIS rules for tri-zonal LED dimming).
3. **IEEE Internet of Things Journal (IoT-J 2026)** — *Smart Window & Curtain Co-Regulation for Thermal and Visual Comfort* (18 Mamdani FIS rules balancing solar heat gain vs. daylight illuminance).

---

## 🚀 Key Features & Scenarios

| Scenario | Strategy | Controller | Energy Savings |
| :--- | :--- | :--- | :--- |
| **S0** | Baseline (24/7 ON, 780 W) | None (Legacy) | 0.0% (Reference) |
| **S1** | Manager Cutoff (09:00 - 18:00) | Schedule Timer | 62.5% |
| **S2** | PIR Occupancy (10-min hold) | Passive Infrared | 65.3% |
| **S3** | Uniform Dimming (500 lux target) | Proportional (34 W/fixture) | 47.7% |
| **S4** | Static Daylight Harvesting | Window Row Cutoff (09:00 - 13:00) | 5.6% |
| **S5** | Fuzzy Lighting (CCIS 2024) | 3-Input Mamdani FIS | 85.2% |
| **S6** | **Fuzzy Co-Regulation (IoT-J 2026)** | **Dual FIS: Lights + 30 Motorised Blinds** | **~83.6% Light + 38% Cooling Save** |

## 📁 End-Semester Documentation & Reference Models
All detailed documentation from the end-sem roadmap (`doc2`) is located in the [`Docs/`](Docs/) directory:
- [`Docs/ClassroomTwin_EndSem_Build_Spec.pdf`](Docs/ClassroomTwin_EndSem_Build_Spec.pdf) — Complete 34-page specification with mathematical formulations, membership functions, FIS rule bases, and experimental benchmarks.
- [`Docs/Reference/model.py`](Docs/Reference/model.py) & [`expected_TwinResults.csv`](Docs/Reference/expected_TwinResults.csv) — Python reference model and benchmark validation data.

---

## 🛠️ How to Run (For Teammates)

### Prerequisites:
- **Unity Hub**
- **Unity 6 Editor** (`6000.x.x`, recommended `6000.6.0f1` or later)

### Steps:
1. Clone or download this repository:
   ```bash
   git clone https://github.com/PARESH671/ClassroomTwin.git
   ```
2. Open **Unity Hub**.
3. Click **Add > Add project from disk** and select the cloned `ClassroomTwin` folder.
4. Wait for Unity to import packages and compile assets (takes ~1-2 minutes on first run).
5. In the Project window, open:
   `Assets > classroomMain.unity`
6. Press the **Play (▶️)** button at the top center!

---

## 🎮 Interactive Keyboard Controls (In Play Mode)
- **`0` – `6`**: Switch active control scenario (`6` = Fuzzy Co-Regulation)
- **`T`**: Toggle **Heatwave** weather (T_mean = 34 °C, peak facade solar 550 W/m²)
- **`Q`**: Toggle **Pollution** smog weather (AQI = 250+ Poor)
- **`B`**: Toggle **24-hour comparative batch table** overlay
- **`[` / `]`**: Slow down / Speed up simulation time
- **`Space`**: Pause / Resume clock
- **`R`**: Reset simulation to 08:30 AM
- **`H`**: Hide / Show on-screen HUD Dashboard

---

## 🧪 Mathematical Verification
Run the 70+ regression checks against the Python reference model anytime inside Unity:
> **Digital Twin > Run Self-Test** (All checks must show `PASS`).

