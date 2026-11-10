// ClassroomTwin - runtime bridge between the deterministic TwinSimulator and the Unity scene.
// The simulator owns ALL physics and control decisions. This MonoBehaviour only:
//   (1) advances the simulator in fixed 1-minute steps,  (2) maps its state to slats/lights/sun,
//   (3) handles keys,  (4) draws the HUD and the 24-h batch table.
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ClassroomTwin.Core;

public class SimulationEngine : MonoBehaviour
{
    [Header("Simulation")]
    [Range(0, 6)] public int scenario = 6;
    public Weather weather = Weather.Normal;
    [Tooltip("Simulated minutes per real second (15 => one day in 96 s)")]
    public float minutesPerRealSecond = 15f;
    [Tooltip("Start-of-run clock, minutes after midnight (510 = 08:30)")]
    public int startMinute = 510;

    [Header("Visual mapping (cosmetic only, never affects results)")]
    public float servoVisualSpeedDegPerSec = 40f;
    public float spotIntensityAtFull = 1.0f;
    public float sunIntensityAtNoon = 1.2f;
    public bool showHud = true;

    TwinSimulator sim;
    float minuteAccumulator;
    bool paused;
    float visualAngle;
    bool showBatch;
    string batchText = "";

    readonly List<Transform> slats = new List<Transform>();
    readonly List<Light>[] rowLights = { new List<Light>(), new List<Light>(), new List<Light>() };
    readonly List<Renderer>[] rowPanels = { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
    Light sun;
    MaterialPropertyBlock mpb;
    GUIStyle boxStyle, labelStyle, headStyle, goodStyle, hintStyle, monoStyle;

    const int MaxStepsPerFrame = 240;
    static readonly string[] RowObjectNames = { "Row1_Window", "Row2_Center", "Row3_Corridor" };
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 16);
        mpb = new MaterialPropertyBlock();
        FindHardware();
        sim = new TwinSimulator(scenario, weather);
        sim.ResetToMinute(startMinute);
        visualAngle = (float)sim.ThetaDeg;
        ApplyVisuals(0f);
    }

    void FindHardware()
    {
        GameObject lighting = GameObject.Find("Lighting_System");
        if (lighting == null) Debug.LogError("SimulationEngine: 'Lighting_System' not found - run Digital Twin > Generate Classroom first.");
        else
        {
            for (int r = 0; r < 3; r++)
            {
                Transform row = lighting.transform.Find(RowObjectNames[r]);
                if (row == null) { Debug.LogError("SimulationEngine: missing " + RowObjectNames[r]); continue; }
                foreach (Transform fixture in row)
                {
                    Light l = fixture.GetComponentInChildren<Light>();
                    Renderer rend = fixture.GetComponent<Renderer>();
                    if (l != null) rowLights[r].Add(l);
                    if (rend != null) rowPanels[r].Add(rend);
                }
            }
        }

        GameObject blinds = GameObject.Find("Smart_Blinds");
        if (blinds == null) Debug.LogError("SimulationEngine: 'Smart_Blinds' not found - regenerate the classroom.");
        else
            foreach (Transform bay in blinds.transform)
                foreach (Transform child in bay)
                    if (child.name.StartsWith("Slat_")) slats.Add(child);

        GameObject sunGo = GameObject.Find("Sunlight");
        if (sunGo != null) sun = sunGo.GetComponent<Light>();

        if (slats.Count != 30) Debug.LogWarning("SimulationEngine: expected 30 slats, found " + slats.Count);
        int lights = rowLights[0].Count + rowLights[1].Count + rowLights[2].Count;
        if (lights != 12) Debug.LogWarning("SimulationEngine: expected 12 fixtures, found " + lights);
    }

    void Update()
    {
        HandleKeys();
        if (!paused)
        {
            minuteAccumulator += Time.deltaTime * minutesPerRealSecond;
            int steps = (int)minuteAccumulator;
            if (steps > MaxStepsPerFrame) { steps = MaxStepsPerFrame; minuteAccumulator = 0f; }
            else minuteAccumulator -= steps;
            for (int i = 0; i < steps; i++) sim.Step();
        }
        ApplyVisuals(Time.deltaTime);
    }

    void HandleKeys()
    {
        for (int s = 0; s <= 6; s++)
            if (Input.GetKeyDown(KeyCode.Alpha0 + s) || Input.GetKeyDown(KeyCode.Keypad0 + s)) { scenario = s; sim.SetScenario(s); }

        if (Input.GetKeyDown(KeyCode.T)) { weather = (weather == Weather.Heatwave) ? Weather.Normal : Weather.Heatwave; sim.Weather = weather; }
        if (Input.GetKeyDown(KeyCode.Q)) { weather = (weather == Weather.Pollution) ? Weather.Normal : Weather.Pollution; sim.Weather = weather; }
        if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
        if (Input.GetKeyDown(KeyCode.R)) { sim.ResetToMinute(startMinute); minuteAccumulator = 0f; }
        if (Input.GetKeyDown(KeyCode.RightBracket)) minutesPerRealSecond = Mathf.Min(120f, minutesPerRealSecond * 2f);
        if (Input.GetKeyDown(KeyCode.LeftBracket)) minutesPerRealSecond = Mathf.Max(1f, minutesPerRealSecond * 0.5f);
        if (Input.GetKeyDown(KeyCode.H)) showHud = !showHud;
        if (Input.GetKeyDown(KeyCode.B))
        {
            showBatch = !showBatch;
            if (showBatch) RunBatch();
        }
    }

    void RunBatch()
    {
        DayResult[,] res = TwinSimulator.RunAll();
        string csv = TwinSimulator.FormatTable(res);
        string path = Path.Combine(Application.persistentDataPath, "TwinResults.csv");
        File.WriteAllText(path, csv);
        Debug.Log("ClassroomTwin batch written to " + path + "\n" + csv);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("24-h deterministic batch (lighting kWh | cooling kWh | total kWh | light save vs S0 | Tpeak C)");
        for (int w = 0; w < res.GetLength(0); w++)
        {
            sb.AppendLine("--- " + TwinConfig.Profiles[w].Name + " ---");
            double e0 = res[w, 0].LightingKWh;
            for (int s = 0; s < 7; s++)
            {
                DayResult r = res[w, s];
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "S{0}  {1,7:F3}  {2,7:F3}  {3,7:F3}  {4,6:F1}%  {5,5:F1}", s, r.LightingKWh, r.CoolingKWh, r.TotalKWh, 100.0 * (1.0 - r.LightingKWh / e0), r.TPeakC));
            }
        }
        batchText = sb.ToString();
    }

    void ApplyVisuals(float dt)
    {
        // 1. slats - cosmetic servo animation towards the modelled angle
        visualAngle = (dt <= 0f) ? (float)sim.ThetaDeg : Mathf.MoveTowards(visualAngle, (float)sim.ThetaDeg, servoVisualSpeedDegPerSec * dt);
        Quaternion rot = Quaternion.Euler(visualAngle, 0f, 0f);
        for (int i = 0; i < slats.Count; i++) slats[i].localRotation = rot;

        // 2. LED rows - PWM level drives light intensity and panel emission
        for (int r = 0; r < 3; r++)
        {
            float la = (float)sim.La[r];
            for (int i = 0; i < rowLights[r].Count; i++)
            {
                rowLights[r][i].enabled = la > 0.001f;
                rowLights[r][i].intensity = spotIntensityAtFull * la;
            }
            for (int i = 0; i < rowPanels[r].Count; i++)
            {
                rowPanels[r][i].GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, Color.white * (2.0f * la));
                rowPanels[r][i].SetPropertyBlock(mpb);
            }
        }

        // 3. sun - smooth clock; always enters through the window wall (light travels towards +Z)
        if (sun != null)
        {
            float h = (sim.MinuteOfDay + minuteAccumulator) / 60f;
            float sf = (float)TwinSimulator.Sun(h);
            sun.enabled = sf > 0f;
            if (sf > 0f)
            {
                float elevation = 5f + 55f * sf;                 // 5 deg at sunrise/sunset, 60 deg at noon
                float azimuth = -60f + 120f * (h - 6f) / 12f;    // -60 deg (06:00) -> +60 deg (18:00)
                sun.transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
                sun.intensity = sunIntensityAtNoon * sf * (float)(TwinConfig.Profile(weather).EdiffMaxLux / 20000.0);
            }
        }
    }

    // ---------------- HUD ----------------
    void BuildStyles()
    {
        if (boxStyle != null) return;
        boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        boxStyle.normal.textColor = Color.white;
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        labelStyle.normal.textColor = Color.white;
        headStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
        headStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);
        goodStyle = new GUIStyle(labelStyle) { fontSize = 16, fontStyle = FontStyle.Bold };
        goodStyle.normal.textColor = Color.green;
        hintStyle = new GUIStyle(labelStyle) { fontSize = 12 };
        hintStyle.normal.textColor = Color.yellow;
        monoStyle = new GUIStyle(labelStyle) { fontSize = 13 };
        monoStyle.font = Font.CreateDynamicFontFromOSFont("Courier New", 13);
    }

    static string AqiBand(double a)
    {
        if (a <= 50) return "Good";
        if (a <= 100) return "Satisfactory";
        if (a <= 200) return "Moderate";
        if (a <= 300) return "Poor";
        if (a <= 400) return "Very Poor";
        return "Severe";
    }

    static string CurtainLabel(double t) { return t < 15.0 ? "Open" : (t < 60.0 ? "Partial" : "Closed"); }

    void OnGUI()
    {
        if (sim == null || !showHud) return;
        BuildStyles();

        int m = sim.MinuteOfDay;
        string clock = string.Format("{0:00}:{1:00}", m / 60, m % 60);
        float x = 10f, y = 10f, w = 560f, lh = 21f;
        GUI.Box(new Rect(x, y, w, 430f), "Classroom Digital Twin - Fuzzy Co-Regulation", boxStyle);
        y += 34f;

        Line(ref y, x, w, lh, "Time " + clock + "   |   " + minutesPerRealSecond.ToString("0") + " sim-min / s" + (paused ? "   [PAUSED]" : ""), labelStyle);
        Line(ref y, x, w, lh, "Mode [" + sim.Scenario + "] " + TwinConfig.ScenarioNames[sim.Scenario].Substring(3), headStyle);
        Line(ref y, x, w, lh, "Weather: " + TwinConfig.Profile(sim.Weather).Name + "   |   Room: " + (sim.Occupied ? "Occupied" : (sim.PirActive ? "Empty (PIR hold)" : "Empty")), labelStyle);
        y += 4f;
        Line(ref y, x, w, lh, "SENSORS", headStyle);
        Line(ref y, x, w, lh, string.Format("Ext. diffuse {0:F0} lux | LDR(window) {1:F0} %", sim.EdiffLux, sim.LdrCurtainPct), labelStyle);
        Line(ref y, x, w, lh, string.Format("T_out {0:F1} C | T_in {1:F1} C | AQI {2:F0} ({3})", sim.ToutC, sim.TinC, sim.Aqi, AqiBand(sim.Aqi)), labelStyle);
        y += 4f;
        Line(ref y, x, w, lh, "ACTUATORS", headStyle);
        Line(ref y, x, w, lh, string.Format("Blinds {0:F1} deg [{1}]  (servo view {2:F1} deg) | transmission {3:F2}{4}",
            sim.ThetaDeg, CurtainLabel(sim.ThetaDeg), visualAngle, sim.TauBlind, sim.CurtainHeld ? " | HOLD" : ""), labelStyle);
        for (int r = 0; r < 3; r++)
            Line(ref y, x, w, lh, string.Format("{0,-8} LED {1,3:F0} %  daylight {2,5:F0} lux  task {3,5:F0} lux",
                TwinConfig.RowNames[r], sim.La[r] * 100.0, sim.EdayLux[r], sim.EtaskLux[r]), labelStyle);
        y += 4f;
        Line(ref y, x, w, lh, "ENERGY", headStyle);
        Line(ref y, x, w, lh, string.Format("Lighting power {0:F0} W  (baseline {1:F0} W) | visual comfort Fc {2:F2}", sim.PowerW, TwinConfig.BaselinePowerW, sim.FcNow), labelStyle);
        double save = sim.BaselineWh > 0 ? 100.0 * (1.0 - sim.LightingWh / sim.BaselineWh) : 0.0;
        Line(ref y, x, w, lh, string.Format("Since last switch: {0:F2} kWh lighting vs {1:F2} kWh baseline | cooling est. {2:F2} kWh",
            sim.LightingWh / 1000.0, sim.BaselineWh / 1000.0, sim.CoolingWh / 1000.0), labelStyle);
        Line(ref y, x, w, lh + 3f, string.Format("Lighting energy saved: {0:F1} %", save), goodStyle);
        Line(ref y, x, w, lh, "0-6 scenario | T heatwave | Q pollution | B 24-h batch | [ ] speed | Space pause | R reset | H hide", hintStyle);

        if (showBatch && batchText.Length > 0)
        {
            GUI.Box(new Rect(Screen.width - 600f, 10f, 590f, 560f), "", boxStyle);
            GUI.Label(new Rect(Screen.width - 590f, 18f, 575f, 545f), batchText, monoStyle);
        }
    }

    static void Line(ref float y, float x, float w, float h, string text, GUIStyle style)
    {
        GUI.Label(new Rect(x + 10f, y, w - 20f, h), text, style);
        y += h;
    }
}
