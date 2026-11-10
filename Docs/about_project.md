# Digital Twin Ecosystem for Energy-Efficient Classroom Lighting
> **Minor Project Documentation**  
> **Reference Research Paper:** *"Digital Twin-Based Assessment Framework for Energy Savings in University Classroom Lighting"* (MDPI Buildings 2022)  
> **Authors:** Hyuncheol Seo and Woo-Seung Yun (Kyungpook National University, South Korea)

---

## 1. Project Overview (In Simple Words)

In educational institutions (schools, colleges, universities), classrooms often leave lights turned **ON** even when no students or professors are present.
- A field survey conducted across **9 university buildings and 55 classrooms** showed that lighting was left on in empty classrooms for an average of **10.7 hours per day**.
- Furthermore, modern LED light fixtures are frequently **over-designed** (providing 700 to 1500+ lux on desks, whereas standard comfortable reading/writing requires only **500 lux** according to standard EN 12464-1).
- This project builds a **Digital Twin simulation ecosystem in Unity 3D** to model a university classroom, simulate occupant behavior and schedules, test various energy-saving control algorithms, and measure how much electricity and cost can be saved.

---

## 2. What is a "Digital Twin"?

A **Digital Twin** is a real-time, interactive 3D virtual replica of a physical space (in our case, a college classroom) running on a computer.

### Why use a Digital Twin instead of testing in the real world?
1. **Zero Hardware Risk & Cost:** Installing hundreds of IoT sensors across campus costs money. A digital twin allows you to simulate and prove savings *before* buying hardware.
2. **Instant Time Travel:** You can simulate an entire semester or academic year's energy consumption in seconds by speeding up time in the simulator.
3. **Scenario Testing:** You can toggle between different control algorithms with a single click and visually watch how the classroom lights and energy meters respond.

---

## 3. Core Objectives of the Minor Project

1. **3D Virtual Environment:** Create a 3D model of a typical college lecture hall in **Unity** (walls, desks, blackboard, ceiling lights in rows, and windows).
2. **Simulation Logic & Scheduling:** Simulate a normal college day (9:00 AM to 6:00 PM) based on lecture timetables, break intervals, and student movement.
3. **Algorithm Implementation:** Implement multiple smart lighting control strategies (from simple timers to PIR motion sensing and daylight harvesting).
4. **Interactive Dashboard:** Build a real-time HUD/UI inside Unity that displays:
   - Current Time & Occupancy Status (Class in progress / Break / Empty)
   - Real-time Power Demand (Watts)
   - Cumulative Energy Consumed (kWh)
   - Percentage Energy Saved compared to baseline
5. **Presentation & Reporting:** Prepare a complete presentation (PPT) and project report with comparative results.

---

## 4. Control Algorithms & Scenarios

The project evaluates 9 distinct scenarios divided into **On/Off Control**, **Quantitative Adjustment (Dimming)**, and **Hybrid Strategies**:

| Scenario | Strategy Name | Type | How It Works | Expected Energy Savings |
| :--- | :--- | :--- | :--- | :--- |
| **0** | **Baseline** | Default | No control. Lights stay on during classes, breaks, and often stay ON overnight if someone forgets to turn them off (63.6% probability). | **0% (Reference)** |
| **1** | **Manager / Schedule Control** | On/Off | A scheduled cutoff at 6:00 PM (end of day) turns off all lights. | **~62%** |
| **2** | **PIR Motion Sensor** | On/Off | Passive Infrared sensor detects humans. Lights turn off automatically if room is empty. | **~63% - 64%** |
| **3** | **Illuminance Dimming** | Quantitative | Dims LED lights from over-designed ~970 lux down to the optimal standard **500 lux**. | **~46%** |
| **4** | **Daylight Harvesting** | Quantitative | Turns OFF the row of lights near windows between 9:00 AM and 1:00 PM when natural sunlight is sufficient. | **~1.7% - 8%** (depends on window access) |
| **1 x 3** | **Manager + Dimming** | Hybrid | 6:00 PM cutoff combined with 500 lux illuminance dimming. | **~79.5%** |
| **2 x 3** | **PIR Sensor + Dimming** | Hybrid | Smart occupancy detection combined with 500 lux dimming. *(Highest efficiency)* | **~80.5% - 81%** |
| **2 x 4** | **PIR Sensor + Daylight** | Hybrid | Motion sensor combined with window-side daylight harvesting. | **~64% - 65%** |

---

## 5. Mathematical Formulation

The energy consumption $E_i$ for classroom $i$ over $n$ time steps is computed using the formula:

$$n = d \times 24 \times \frac{60}{\Delta t}$$

$$E_i = \frac{\Delta t}{60} \sum_{t=1}^{n} (P_{t,i} \times LP_{t,i})$$

Where:
- $d$ = Number of analysis days (e.g., 7 days for a weekly cycle).
- $\Delta t$ = Time step duration in minutes (e.g., 30 minutes).
- $P_{t,i}$ = Probability that lights are ON at time step $t$ (0 or 1 in a deterministic run, or occupancy probability).
- $LP_{t,i}$ = Power consumption of the lights at time step $t$ in Watts (e.g., 800 W at full power, 400 W when dimmed).
- $E_i$ = Cumulative electricity consumed in Watt-hours (Wh).

---

## 6. Unity 3D Ecosystem Architecture

```
+-------------------------------------------------------------------------+
|                              UNITY ENGINE                               |
|                                                                         |
|  [ 3D Classroom Environment ]                                           |
|   - Classroom mesh (Desks, Chairs, Walls, Windows, Blackboard)           |
|   - Light GameObjects:                                                  |
|       * Row 1 (Window side lights)                                      |
|       * Row 2 (Center room lights)                                      |
|       * Row 3 (Corridor side lights)                                    |
|   - Directional Sun Light (Rotates according to simulated time of day)  |
|                                                                         |
|  [ Simulation Manager (C# Script) ]                                     |
|   - Time Clock: Simulates 24-hour cycle (can speed up: 1s = 10 mins)    |
|   - Timetable Scheduler: 9:00-10:00 Class, 10:00-10:15 Break, etc.     |
|   - Occupancy Tracker: Tracks whether room is occupied or empty         |
|   - Algorithm Controller: Applies Scenario rules (Baseline, PIR, Lux)   |
|   - Energy Calculator: Accumulates kWh & % savings                      |
|                                                                         |
|  [ Interactive Canvas UI (Dashboard) ]                                  |
|   - Scenario Dropdown (Choose Baseline, PIR, Dimming, Hybrid)           |
|   - Time Slider & Play/Pause/Fast-Forward buttons                       |
|   - Live Meters: Current Power (Watts), Total Energy (kWh), Saved (%)   |
+-------------------------------------------------------------------------+
```

---

## 7. PPT Presentation Structure (10-Slide Guide)

1. **Slide 1: Title & Introduction**
   - Title: Digital Twin Ecosystem for Energy-Efficient Classroom Lighting
   - Subtitle: Minor Project Presentation
   - Student Name, Roll Number, Branch & Mentor Name

2. **Slide 2: Problem Statement & Motivation**
   - Up to 40% of building energy goes into lighting in educational institutes.
   - Lights remain ON in empty rooms for an average of 10.7 hours/day.
   - Classrooms suffer from excessive light levels (>950 lux vs 500 lux standard).

3. **Slide 3: Proposed Solution — Digital Twin Approach**
   - Definition of Digital Twin: Virtualizing physical classroom systems.
   - Benefits: Risk-free virtual experimentation, rapid ROI assessment before physical sensor deployment.

4. **Slide 4: System Architecture**
   - Building 3D model in Unity 3D.
   - Simulating timetable and occupant behaviors via C# scripts.
   - Live analytics visualization.

5. **Slide 5: Control Algorithms & Scenarios**
   - Summary of Baseline, Manager Timer, PIR Sensor, Illuminance Dimming, and Daylight Harvesting.

6. **Slide 6: Mathematical Model**
   - Energy consumption equation ($E_i = \sum P_{t,i} \times LP_{t,i} \times \Delta t$).
   - Calculation of Energy Use Intensity (EUI) in $\text{kWh/m}^2$.

7. **Slide 7: Simulation Demonstration (Unity Model)**
   - Screenshots of the 3D classroom in Unity.
   - Showing lights turning off automatically when empty or dimming near windows.
   - Demonstration of the real-time analytics UI.

8. **Slide 8: Experimental Results & Comparisons**
   - Bar chart / Table showing savings across scenarios:
     - Baseline: 0%
     - Manager cutoff: ~62%
     - PIR Sensor: ~63% - 64%
     - Dimming to 500 lux: ~46%
     - Combined (PIR + Dimming): **~80.5%**

9. **Slide 9: Managerial & Practical Insights**
   - Why a simple routine manager sweep or automated timer after 6 PM saves ~62% with zero extra sensor hardware.
   - When to invest in PIR sensors vs. when to invest in occupant awareness campaigns.

10. **Slide 10: Conclusion & Future Scope**
    - Summary of project achievements.
    - Future enhancements: Connecting Unity to physical IoT devices (ESP32/Raspberry Pi) via MQTT for bi-directional physical-to-digital twin sync.

---

## 8. Practical Tips for Evaluation & Viva

- **If the examiner asks: "Did you invent this algorithm?"**  
  *Answer:* "No, the algorithm is based on proven building energy assessment methodologies from research published in MDPI Buildings. Our contribution is creating an interactive 3D Digital Twin simulation ecosystem in Unity to visualize, test, and validate these energy-saving algorithms dynamically."
- **If the examiner asks: "Why did you use Unity instead of a simple Python script?"**  
  *Answer:* "A Digital Twin requires spatial visualization. Unity allows us to simulate physical light distribution, room zoning (window side vs. corridor side), daylight angles, and provide an intuitive 3D visual dashboard that facility managers can understand at a glance."
