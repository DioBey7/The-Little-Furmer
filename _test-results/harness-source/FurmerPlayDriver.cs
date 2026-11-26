// TEMPORARY test harness - lives only in the FurmerTest copy, never committed.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FurmerPlayDriver : MonoBehaviour
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string ShotDir = @"C:\Users\WIN11\Deneme1\_test-results\screenshots";
    static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/playtest-results.txt"));

    static readonly List<string> logErrors = new List<string>();
    static readonly List<string> lines = new List<string>();
    static int failures, passes;

    float lastProgress;
    bool finished;
    string currentStep = "init";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (!SessionState.GetBool("FurmerPlayTest.Active", false)) return;
        SessionState.SetBool("FurmerPlayTest.Active", false);
        Application.logMessageReceived += OnLog;
        var go = new GameObject("[FurmerPlayDriver]");
        DontDestroyOnLoad(go);
        go.AddComponent<FurmerPlayDriver>();
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (msg.StartsWith("[PLAYTEST]")) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            logErrors.Add($"{type}: {msg}\n{stack}");
        else if (type == LogType.Warning && msg.StartsWith("[GameManager]"))
            Note("game warning: " + msg);
    }

    static void Note(string s) { lines.Add(s); Debug.Log("[PLAYTEST] " + s); }
    static void Check(bool ok, string what)
    {
        if (ok) { passes++; Note("PASS " + what); }
        else { failures++; Note("FAIL " + what); }
    }

    void Start() { lastProgress = Time.realtimeSinceStartup; StartCoroutine(Scenario()); }

    void Update()
    {
        if (finished) return;
        if (Time.realtimeSinceStartup - lastProgress > 180f)
        {
            failures++;
            Note($"FAIL watchdog: step '{currentStep}' made no progress for 180 s (coroutine died or hung)");
            Finish();
        }
    }

    void Step(string s) { currentStep = s; lastProgress = Time.realtimeSinceStartup; Note("--- " + s); }

    IEnumerator WaitFor(Func<bool> cond, float timeout, string what)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < end)
        {
            bool ok = false;
            try { ok = cond(); } catch { }
            if (ok) { Check(true, what); yield break; }
            yield return null;
        }
        Check(false, what + $" (timed out after {timeout}s)");
    }

    static bool LevelReady(int idx) =>
        GameManager.Instance != null && LevelManager.Instance != null &&
        LevelManager.Instance.currentLevelInstance != null &&
        LevelManager.Instance.CurrentLevelIndex == idx && GameManager.Instance.isGameActive;

    IEnumerator Scenario()
    {
        Directory.CreateDirectory(ShotDir);
        Note($"Screen {Screen.width}x{Screen.height}, persistentDataPath {Application.persistentDataPath}");
        Note("GameManager.resetDataOnStart in scene = " + (GameManager.Instance != null ? GameManager.Instance.resetDataOnStart.ToString() : "n/a"));

        Step("startup: wait for level 1");
        yield return WaitFor(() => LevelReady(0), 60, "level index 0 loaded on fresh start");
        yield return WaitRealtime(1f);
        var menu = FindObjectOfType<MenuManager>();
        Check(menu != null, "MenuManager present");
        Note("levels in masterLevelData: " + (LevelManager.Instance.masterLevelData != null ? LevelManager.Instance.masterLevelData.levels.Count : -1));
        Note("objectives L1: " + DescribeObjectives());

        Step("screenshot main menu");
        yield return Capture("01_main_menu");

        Step("press Play");
        if (menu != null) menu.PlayGame();
        yield return WaitRealtime(1.0f);
        Check(Mathf.Approximately(Time.timeScale, 1f), "timeScale is 1 after PlayGame");
        yield return Capture("02_hud_level1");

        Step("options menu");
        if (menu != null) { menu.OpenOptions(); yield return WaitRealtime(0.7f); yield return Capture("03_options_menu"); menu.CloseOptions(); yield return WaitRealtime(0.6f); }
        Check(Mathf.Approximately(Time.timeScale, 1f), "timeScale back to 1 after closing options");

        Step("pool integrity + double-return guards");
        CheckPools("L1 start");
        DoubleReturnTests();

        Step("let level 1 run 8 s");
        yield return WaitRealtime(8f);
        CheckPools("L1 after 8s play");
        NavDiag("L1 after 8s");

        Step("cash + batched save");
        UiManager.Instance.AddCash(50);
        Check(GameManager.Instance.Data.currentCash == 50, "Data.currentCash == 50 after AddCash(50)");
        yield return WaitRealtime(2f);
        var saved = ReadSave();
        Check(saved != null && saved.currentCash == 50, $"save file has currentCash 50 (got {(saved == null ? "no file" : saved.currentCash.ToString())})");

        Step("restart #1 (no forced save)");
        yield return Restart();
        yield return WaitFor(() => LevelReady(0), 60, "level index 0 after restart");
        yield return WaitRealtime(0.5f);
        Check(FindObjectsOfType<GameManager>().Length == 1, "exactly one GameManager after restart");
        Check(GameManager.Instance.Data.currentCash == 50, $"Data.currentCash kept after restart (got {GameManager.Instance.Data.currentCash})");
        Check(UiManager.Instance.CurrentCash == 50, $"UI cash kept after restart (got {UiManager.Instance.CurrentCash})");
        menu = FindObjectOfType<MenuManager>();
        if (menu != null) menu.PlayGame();
        yield return WaitRealtime(1f);

        for (int next = 1; next <= 3; next++)
        {
            Step($"complete level index {next - 1} -> {next}");
            ObjectiveManager.Instance.DebugCompleteAll();
            yield return WaitRealtime(1.8f);
            var popup = FindPopup();
            Check(popup != null && popup.activeInHierarchy, "level complete popup shown");
            if (next == 1) yield return Capture("04_level_complete");
            LevelManager.Instance.LoadNextLevelFromUI();
            yield return WaitFor(() => LevelReady(next), 30, $"level index {next} loaded");
            yield return null; yield return null;
            int levels = 0; foreach (var l in FindObjectsOfType<Level>()) levels++;
            Check(levels == 1, $"exactly one active Level object (got {levels})");
            Check(GameManager.Instance.Data.currentLevelIndex == next, $"Data.currentLevelIndex == {next}");
            Check(GameManager.Instance.Data.currentCash == 0 && UiManager.Instance.CurrentCash == 0, "cash reset to 0 on new level");
            var s = ReadSave();
            Check(s != null && s.currentLevelIndex == next, $"save file currentLevelIndex == {next}");
            Note($"objectives L{next + 1}: " + DescribeObjectives());
            CheckPools($"L{next + 1} start");
            yield return WaitRealtime(6f);
            CheckPools($"L{next + 1} after 6s play");
            NavDiag($"L{next + 1} after 6s");
            if (next == 1)
            {
                yield return Capture("05_hud_level2");
                UiManager.Instance.AddCash(30);
                yield return WaitRealtime(2f);
                Step("restart #2 on level 2");
                yield return Restart();
                yield return WaitFor(() => LevelReady(1), 60, "level index 1 after restart");
                yield return WaitRealtime(0.5f);
                Check(GameManager.Instance.Data.currentCash == 30 && UiManager.Instance.CurrentCash == 30, $"cash 30 kept after restart on level 2 (data {GameManager.Instance.Data.currentCash}, ui {UiManager.Instance.CurrentCash})");
                Check(FindObjectsOfType<GameManager>().Length == 1, "exactly one GameManager after restart #2");
                CheckPools("L2 after restart");
                menu = FindObjectOfType<MenuManager>();
                if (menu != null) menu.PlayGame();
                yield return WaitRealtime(1f);
            }
        }
        yield return Capture("06_hud_level4");
        Finish();
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        Check(logErrors.Count == 0, $"no errors/exceptions logged during play ({logErrors.Count})");
        var sb = new StringBuilder();
        sb.AppendLine($"RESULT: {passes} passed, {failures} failed");
        foreach (var l in lines) sb.AppendLine(l);
        sb.AppendLine("==== logged errors/exceptions ====");
        foreach (var e in logErrors) sb.AppendLine(e).AppendLine();
        File.WriteAllText(ResultPath, sb.ToString());
        Debug.Log("[PLAYTEST] RESULT " + passes + " passed, " + failures + " failed. Written to " + ResultPath);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static IEnumerator WaitRealtime(float s) { yield return new WaitForSecondsRealtime(s); }

    IEnumerator Restart()
    {
        Note("restart: destroying GameManager (DontDestroyOnLoad) and reloading scene");
        Destroy(GameManager.Instance.gameObject);
        yield return null;
        EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return null; yield return null;
        lastProgress = Time.realtimeSinceStartup;
    }

    static GameData ReadSave()
    {
        string p = Path.Combine(Application.persistentDataPath, "ArcadeIdleSave.json");
        if (!File.Exists(p)) return null;
        try { return JsonUtility.FromJson<GameData>(File.ReadAllText(p)); } catch (Exception e) { Note("save parse error " + e.Message); return null; }
    }

    static string DescribeObjectives()
    {
        var sb = new StringBuilder();
        if (ObjectiveManager.Instance == null) return "none";
        foreach (var o in ObjectiveManager.Instance.currentObjectives) sb.Append($"{o.type}/{o.itemType} {o.currentAmount}/{o.targetAmount}; ");
        return sb.ToString();
    }

    static GameObject FindPopup()
    {
        var f = typeof(UiManager).GetField("levelCompletePopup", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return f != null ? f.GetValue(UiManager.Instance) as GameObject : null;
    }

    static void NavDiag(string label)
    {
        try
        {
            var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            Note($"[{label}] NavMesh triangulation: {tri.vertices.Length} verts, {tri.indices.Length / 3} tris");
            foreach (var s in FindObjectsOfType<Unity.AI.Navigation.NavMeshSurface>())
                Note($"[{label}] NavMeshSurface '{s.name}' agentTypeID {s.agentTypeID} data {(s.navMeshData != null ? s.navMeshData.name : "NULL")} pos {s.transform.position}");
            var cm = CustomerManager.Instance;
            if (cm != null && cm.spawnPoint != null)
            {
                bool hit = UnityEngine.AI.NavMesh.SamplePosition(cm.spawnPoint.position, out var h, 5f, UnityEngine.AI.NavMesh.AllAreas);
                Note($"[{label}] customer spawnPoint {cm.spawnPoint.position} nearest navmesh {(hit ? h.position + " dist " + Vector3.Distance(h.position, cm.spawnPoint.position).ToString("0.00") : "none within 5m")}");
            }
            foreach (var a in FindObjectsOfType<UnityEngine.AI.NavMeshAgent>())
            {
                bool hit = UnityEngine.AI.NavMesh.SamplePosition(a.transform.position, out var h, 5f, UnityEngine.AI.NavMesh.AllAreas);
                Note($"[{label}] agent '{a.name}' type {a.agentTypeID} onNavMesh {a.isOnNavMesh} enabled {a.enabled} pos {a.transform.position} nearest {(hit ? Vector3.Distance(h.position, a.transform.position).ToString("0.00") : "none")}");
            }
        }
        catch (Exception e) { Note("navdiag threw " + e); }
    }

    // ---------------- pools ----------------
    static T Field<T>(object o, string name) where T : class =>
        o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(o) as T;

    static void CheckPools(string label)
    {
        try
        {
            var pm = PoolingManager.Instance;
            var pools = Field<Dictionary<ItemType, List<GameObject>>>(pm, "pools");
            var set = Field<HashSet<GameObject>>(pm, "pooledObjects");
            var all = new HashSet<GameObject>();
            int dup = 0, activeInPool = 0, notInSet = 0, destroyed = 0, total = 0;
            foreach (var kv in pools)
                foreach (var go in kv.Value)
                {
                    if (go == null) { destroyed++; continue; }
                    total++;
                    if (!all.Add(go)) dup++;
                    if (go.activeSelf) activeInPool++;
                    if (set != null && !set.Contains(go)) notInSet++;
                }
            int activeMarkedPooled = 0, activeItems = 0;
            foreach (var it in FindObjectsOfType<Item>())
            {
                activeItems++;
                if (all.Contains(it.gameObject)) activeMarkedPooled++;
            }
            Note($"[{label}] item pools: {total} pooled, {activeItems} active items, {destroyed} destroyed entries");
            Check(dup == 0, $"[{label}] no duplicate entries in item pools ({dup})");
            Check(activeInPool == 0, $"[{label}] no active objects inside item pools ({activeInPool})");
            Check(activeMarkedPooled == 0, $"[{label}] no in-play item is also in a pool ({activeMarkedPooled})");
            Check(notInSet == 0, $"[{label}] pool list and pooled set agree ({notInSet})");

            var cm = CashManager.Instance;
            var cpool = Field<List<GameObject>>(cm, "pool");
            var cset = new HashSet<GameObject>();
            int cdup = 0, cactive = 0, cdestroyed = 0;
            foreach (var go in cpool)
            {
                if (go == null) { cdestroyed++; continue; }
                if (!cset.Add(go)) cdup++;
                if (go.activeSelf) cactive++;
            }
            int activeCash = 0, activeCashPooled = 0;
            foreach (var c in FindObjectsOfType<CashItem>()) { activeCash++; if (cset.Contains(c.gameObject)) activeCashPooled++; }
            Note($"[{label}] cash pool: {cset.Count} pooled, {activeCash} active, {cdestroyed} destroyed entries");
            Check(cdup == 0 && cactive == 0 && activeCashPooled == 0, $"[{label}] cash pool consistent (dup {cdup}, activeInPool {cactive}, activeAlsoPooled {activeCashPooled})");
        }
        catch (Exception e) { Check(false, $"[{label}] pool check threw {e}"); }
    }

    static void DoubleReturnTests()
    {
        try
        {
            var pm = PoolingManager.Instance;
            var pools = Field<Dictionary<ItemType, List<GameObject>>>(pm, "pools");
            foreach (ItemType t in Enum.GetValues(typeof(ItemType)))
            {
                var go = pm.GetItem(t);
                if (go == null) continue;
                pm.ReturnItem(t, go);
                pm.ReturnItem(t, go);
                int n = 0; foreach (var x in pools[t]) if (x == go) n++;
                Check(n == 1, $"item {t}: returned twice -> in pool once (count {n})");
                var a = pm.GetItem(t); var b = pm.GetItem(t);
                Check(a != b, $"item {t}: two GetItem calls give two objects");
                pm.ReturnItem(t, a); pm.ReturnItem(t, b);
                break;
            }
            var cm = CashManager.Instance;
            var cpool = Field<List<GameObject>>(cm, "pool");
            var c = cm.GetCash(Vector3.zero, Quaternion.identity);
            cm.ReturnToPool(c); cm.ReturnToPool(c);
            int cn = 0; foreach (var x in cpool) if (x == c) cn++;
            Check(cn == 1, $"cash returned twice -> in pool once (count {cn})");
            var c1 = cm.GetCash(Vector3.zero, Quaternion.identity); var c2 = cm.GetCash(Vector3.zero, Quaternion.identity);
            Check(c1 != c2, "two GetCash calls give two objects");
            cm.ReturnToPool(c1); cm.ReturnToPool(c2);
        }
        catch (Exception e) { Check(false, "double-return test threw " + e); }
    }

    // ---------------- screenshots ----------------
    struct CanvasState { public Canvas c; public RenderMode mode; public Camera cam; public float dist; public CanvasScaler s; public float match; }

    IEnumerator Capture(string name)
    {
        lastProgress = Time.realtimeSinceStartup;
        var cam = Camera.main;
        if (cam == null) { Note("no main camera, skip screenshot " + name); yield break; }
        var sizes = new[] { new Vector2Int(1920, 1080), new Vector2Int(960, 540) };
        foreach (var size in sizes)
            foreach (bool match1 in new[] { false, true })
            {
                var states = new List<CanvasState>();
                foreach (var c in FindObjectsOfType<Canvas>())
                {
                    if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    var st = new CanvasState { c = c, mode = c.renderMode, cam = c.worldCamera, dist = c.planeDistance, s = c.GetComponent<CanvasScaler>() };
                    if (st.s != null) st.match = st.s.matchWidthOrHeight;
                    states.Add(st);
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = cam.nearClipPlane + 0.05f;
                    if (st.s != null && match1 && st.s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) st.s.matchWidthOrHeight = 1f;
                }
                var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                var prevTarget = cam.targetTexture;
                cam.targetTexture = rt;
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;
                string file = Path.Combine(ShotDir, $"{name}_{size.x}x{size.y}_{(match1 ? "match1" : "orig")}.png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                var scalerInfo = new StringBuilder();
                foreach (var st in states) if (st.s != null) scalerInfo.Append($"{st.c.name}: scaleFactor {st.c.scaleFactor:0.###}, match {st.s.matchWidthOrHeight}; ");
                Note($"screenshot {file} [{scalerInfo}]");
                cam.targetTexture = prevTarget;
                Destroy(tex); rt.Release(); Destroy(rt);
                foreach (var st in states)
                {
                    st.c.renderMode = st.mode; st.c.worldCamera = st.cam; st.c.planeDistance = st.dist;
                    if (st.s != null) st.s.matchWidthOrHeight = st.match;
                }
            }
    }
}
#endif
