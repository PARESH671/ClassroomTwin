// ClassroomTwin - procedural scene builder (Editor only).
// Menu: Digital Twin > Generate Classroom (Paper Figure 5 Type B)
// Changes vs mid-sem: ceiling added, desks seated on the floor, window bay centres fixed to +/-3.80 m,
// glass panes, 30 interior venetian slats, 3 blind motor units, materials saved as assets,
// LED spot shadows off, sun aimed THROUGH the window wall, Undo + scene-dirty support.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ClassroomBuilder
{
    const string MatFolder = "Assets/Materials/Twin";

    // ---------- geometry constants (metres) ----------
    public const float WindowWallZ = -4.4f;          // centre plane of window wall (thickness 0.2 => inner face -4.3)
    public const float SillTopY = 0.8f;
    public const float HeaderBottomY = 2.4f;
    public static readonly float[] BayCentersX = { -3.80f, 0.0f, 3.80f };
    public const float BayWidth = 3.50f;
    public const float SlatZ = -4.20f;               // interior side, 0.10 m in front of inner wall face
    public const float SlatLength = 3.46f;           // 0.02 m clearance each side
    public const float SlatThickness = 0.012f;
    public const float SlatDepth = 0.17f;            // must equal TwinConfig.SlatDepthM
    public const float SlatPitch = 0.16f;            // must equal TwinConfig.SlatPitchM
    public const int SlatsPerBay = 10;
    public const float FirstSlatY = 0.88f;           // 0.8 + pitch/2  -> slats at 0.88 ... 2.32

    [MenuItem("Digital Twin/Generate Classroom (Paper Figure 5 Type B)")]
    public static void GenerateClassroom()
    {
        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader lit = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        if (lit == null) { Debug.LogError("ClassroomBuilder: lit shader not found. Use the Built-in Render Pipeline (Standard shader)."); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Twin");

        // 1. Materials (saved as assets so they survive scene save/reload)
        Material matFloor = Mat("Twin_Floor", lit, new Color(0.50f, 0.50f, 0.50f));
        Material matWall = Mat("Twin_Wall", lit, new Color(0.90f, 0.90f, 0.90f));
        Material matCeiling = Mat("Twin_Ceiling", lit, new Color(0.95f, 0.95f, 0.95f));
        Material matBoard = Mat("Twin_Blackboard", lit, new Color(0.10f, 0.30f, 0.10f));
        Material matDesk = Mat("Twin_Desk", lit, new Color(0.60f, 0.40f, 0.20f));
        Material matSlat = Mat("Twin_Slat", lit, new Color(0.90f, 0.92f, 0.95f));
        Material matMotor = Mat("Twin_Motor", lit, new Color(0.15f, 0.15f, 0.18f));
        Material matGlass = Mat("Twin_Glass", lit, new Color(0.70f, 0.90f, 1.00f, 0.25f));
        MakeTransparent(matGlass, urp);
        Material matLED = Mat("Twin_LED", lit, Color.white);
        matLED.EnableKeyword("_EMISSION");
        matLED.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        matLED.SetColor("_EmissionColor", Color.white * 2.0f);
        AssetDatabase.SaveAssets();

        // 2. Root (replace previous build)
        GameObject old = GameObject.Find("Classroom_Room302");
        if (old != null) Undo.DestroyObjectImmediate(old);
        GameObject room = new GameObject("Classroom_Room302");
        Undo.RegisterCreatedObjectUndo(room, "Generate Classroom");

        // 3. Floor (top surface at y = 0.10) and ceiling (bottom surface at y = 3.00)
        Box("Floor", room.transform, new Vector3(0f, 0f, 0f), new Vector3(11.5f, 0.2f, 9.0f), matFloor);
        Box("Ceiling", room.transform, new Vector3(0f, 3.05f, 0f), new Vector3(11.5f, 0.1f, 9.0f), matCeiling);

        // 4. Walls (height 3.0, y 0..3)
        Box("Wall_Back", room.transform, new Vector3(5.65f, 1.5f, 0f), new Vector3(0.2f, 3.0f, 9.0f), matWall);
        Box("Wall_Front", room.transform, new Vector3(-5.65f, 1.5f, 0f), new Vector3(0.2f, 3.0f, 9.0f), matWall);
        Box("Wall_Corridor", room.transform, new Vector3(0f, 1.5f, 4.4f), new Vector3(11.5f, 3.0f, 0.2f), matWall);
        Box("Wall_Window_Sill", room.transform, new Vector3(0f, 0.4f, WindowWallZ), new Vector3(11.5f, 0.8f, 0.2f), matWall);
        Box("Wall_Window_Header", room.transform, new Vector3(0f, 2.7f, WindowWallZ), new Vector3(11.5f, 0.6f, 0.2f), matWall);
        for (int i = 0; i < 2; i++)
            Box("Pillar_" + (i + 1), room.transform, new Vector3(i == 0 ? -1.9f : 1.9f, 1.5f, WindowWallZ), new Vector3(0.3f, 3.0f, 0.25f), matWall);

        // 5. Glass panes (do not cast shadows, no colliders)
        GameObject glass = new GameObject("Window_Glass");
        glass.transform.parent = room.transform;
        for (int b = 0; b < 3; b++)
        {
            GameObject g = Box("Glass_Bay_" + (b + 1), glass.transform, new Vector3(BayCentersX[b], 1.6f, WindowWallZ), new Vector3(BayWidth, 1.6f, 0.01f), matGlass);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            MeshRenderer mr = g.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // 6. Podium & blackboard
        Box("Teacher_Podium", room.transform, new Vector3(-4.7f, 0.1f, 0f), new Vector3(1.5f, 0.2f, 6.0f), matDesk);
        Box("Blackboard", room.transform, new Vector3(-5.5f, 1.6f, 0f), new Vector3(0.05f, 1.2f, 4.5f), matBoard);

        // 7. Lighting system - 12 LED panels in 3 rows (names are read by SimulationEngine)
        GameObject lighting = new GameObject("Lighting_System");
        lighting.transform.parent = room.transform;
        float[] xs = { -3.6f, -1.2f, 1.2f, 3.6f };
        float[] zs = { -2.8f, 0.0f, 2.8f };
        string[] rowNames = { "Row1_Window", "Row2_Center", "Row3_Corridor" };
        for (int r = 0; r < 3; r++)
        {
            GameObject row = new GameObject(rowNames[r]);
            row.transform.parent = lighting.transform;
            for (int x = 0; x < 4; x++)
            {
                GameObject panel = Box("LED_Fixture_" + (x + 1), row.transform, new Vector3(xs[x], 2.95f, zs[r]), new Vector3(1.2f, 0.05f, 0.3f), matLED);
                panel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                GameObject ls = new GameObject("Spotlight");
                ls.transform.parent = panel.transform;
                ls.transform.localPosition = new Vector3(0f, -0.6f, 0f);   // = 0.03 m below the panel in world space
                ls.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // points straight down
                Light l = ls.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 75f;
                l.range = 6.0f;
                l.intensity = 1.0f;
                l.color = new Color(1f, 0.95f, 0.9f);
                l.shadows = LightShadows.None;                              // 12 shadowed spots are too expensive
                l.renderMode = LightRenderMode.ForcePixel;
            }
        }

        // 8. Student desks (seated on floor top y = 0.10 => centre y = 0.475)
        GameObject desks = new GameObject("Student_Desks");
        desks.transform.parent = room.transform;
        for (int x = -2; x <= 4; x += 2)
            for (int z = -2; z <= 2; z += 2)
                Box("Desk_" + x + "_" + z, desks.transform, new Vector3(x, 0.475f, z), new Vector3(0.8f, 0.75f, 1.5f), matDesk);

        // 9. Sun - travels towards +Z so it enters THROUGH the window wall (SimulationEngine animates it)
        GameObject oldSun = GameObject.Find("Sunlight");
        if (oldSun != null) Undo.DestroyObjectImmediate(oldSun);
        GameObject sun = new GameObject("Sunlight");
        Undo.RegisterCreatedObjectUndo(sun, "Generate Classroom");
        Light sl = sun.AddComponent<Light>();
        sl.type = LightType.Directional;
        sl.color = new Color(1f, 0.95f, 0.8f);
        sl.intensity = 1.2f;
        sl.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

        // 10. Smart motorised venetian blinds: 3 bays x 10 slats, rotation about local X
        GameObject blinds = new GameObject("Smart_Blinds");
        blinds.transform.parent = room.transform;
        for (int b = 0; b < 3; b++)
        {
            GameObject bay = new GameObject("Blinds_Bay_" + (b + 1));
            bay.transform.parent = blinds.transform;
            bay.transform.position = new Vector3(BayCentersX[b], 0f, SlatZ);
            for (int s = 0; s < SlatsPerBay; s++)
            {
                GameObject slat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slat.name = "Slat_" + b + "_" + s;
                slat.transform.parent = bay.transform;
                slat.transform.localPosition = new Vector3(0f, FirstSlatY + s * SlatPitch, 0f);
                slat.transform.localRotation = Quaternion.identity;
                slat.transform.localScale = new Vector3(SlatLength, SlatThickness, SlatDepth);
                slat.GetComponent<Renderer>().sharedMaterial = matSlat;
                Object.DestroyImmediate(slat.GetComponent<Collider>());
            }
            // MG996R servo + ZigBee end-device housing (visual only)
            GameObject motor = Box("Blind_Motor_Unit_" + (b + 1), bay.transform, Vector3.zero, new Vector3(0.12f, 0.06f, 0.08f), matMotor);
            motor.transform.localPosition = new Vector3(-1.65f, 2.44f, 0f);
            Object.DestroyImmediate(motor.GetComponent<Collider>());
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("ClassroomTwin: classroom generated - 12 LED fixtures, 30 venetian slats, 3 glass panes, ceiling, sun. Save the scene (Ctrl/Cmd+S).");
    }

    // ---------- helpers ----------
    static GameObject Box(string name, Transform parent, Vector3 worldPos, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.parent = parent;
        g.transform.position = worldPos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static Material Mat(string name, Shader shader, Color c)
    {
        string path = MatFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void MakeTransparent(Material m, bool urp)
    {
        if (urp)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            m.SetFloat("_Mode", 3f);
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
        }
        m.SetInt("_SrcBlend", (int)BlendMode.One);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
    }
}
