// ClassroomTwin - Editor menus: add the engine + camera, run self-test, run 24-h batch.
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using ClassroomTwin.Core;

public static class SimulationBuilder
{
    [MenuItem("Digital Twin/Add Simulation Engine")]
    public static void AddSimulationEngine()
    {
        GameObject go = GameObject.Find("Simulation_Engine");
        if (go == null)
        {
            go = new GameObject("Simulation_Engine");
            Undo.RegisterCreatedObjectUndo(go, "Add Simulation Engine");
        }
        if (go.GetComponent<SimulationEngine>() == null) Undo.AddComponent<SimulationEngine>(go);

        // Presentation camera: back-right corner, looking at the window wall and blackboard.
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject cg = new GameObject("Main Camera");
            cg.tag = "MainCamera";
            cam = cg.AddComponent<Camera>();
            Undo.RegisterCreatedObjectUndo(cg, "Add Camera");
        }
        Vector3 pos = new Vector3(5.0f, 2.2f, 3.9f);
        Vector3 target = new Vector3(-2.0f, 1.1f, -3.6f);
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(target - pos, Vector3.up);
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("ClassroomTwin: Simulation_Engine ready. Press Play.");
    }

    [MenuItem("Digital Twin/Run Self-Test")]
    public static void RunSelfTest()
    {
        foreach (string line in TwinSelfTest.Run())
        {
            if (line.StartsWith("FAIL") || line.StartsWith("SELF-TEST FAILED")) Debug.LogError(line);
            else if (line.StartsWith("SELF-TEST")) Debug.Log(line);
        }
        if (TwinSelfTest.Failures > 0) Debug.LogError("ClassroomTwin: self-test failed - do not continue until every check passes.");
    }

    [MenuItem("Digital Twin/Run 24h Batch Evaluation")]
    public static void RunBatch()
    {
        string csv = TwinSimulator.FormatTable(TwinSimulator.RunAll());
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TwinResults");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "TwinResults.csv");
        File.WriteAllText(path, csv);
        Debug.Log("ClassroomTwin 24-h batch results written to " + path + "\n" + csv);
    }
}
