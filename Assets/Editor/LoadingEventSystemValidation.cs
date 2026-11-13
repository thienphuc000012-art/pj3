using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>Exercises additive UI ownership in temporary loaded scenes, without Play Mode or saving scene changes.</summary>
public static class LoadingEventSystemValidation
{
    const string Request = "Library/CombatValidation/LoadingEventSystem.request";
    const string Result = "Library/CombatValidation/LoadingEventSystem.result.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<EventSystem> created = new List<EventSystem>();
    static readonly List<BaseInputModule> enabledModules = new List<BaseInputModule>();
    static List<EventSystem> Registered => (List<EventSystem>)typeof(EventSystem).GetField("m_EventSystems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Requested; return; }
        File.Delete(Request); Validate();
    }
    [MenuItem("Tools/Adventure/UI/Validate Loading EventSystem Handoff")]
    public static void Validate()
    {
        var active = SceneManager.GetActiveScene();
        var previous = EventSystem.current;
        bool cursorVisible = Cursor.visible;
        Scene loading = default, destination = default;
        var warnings = new List<string>();
        Application.LogCallback listener = (message, stack, type) =>
        { if (message.Contains("event systems in the scene") || message.Contains("one active Event System")) warnings.Add(message); };
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
            Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).All(s => !s.isActiveAndEnabled),
                "Open a map without an active authored EventSystem before validation; existing scene components are never disabled by the test.");
            created.Clear();
            enabledModules.Clear();
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            Check(!SceneManager.GetSceneByPath("Assets/Scenes/Loading.unity").isLoaded && !SceneManager.GetSceneByPath("Assets/Scenes/combattest.unity").isLoaded,
                "Close Loading and combattest in the Editor before this isolated test.");
            loading = EditorSceneManager.OpenScene("Assets/Scenes/Loading.unity", OpenSceneMode.Additive);
            var holder = new GameObject("Isolated Loading Controller"); SceneManager.MoveGameObjectToScene(holder, loading);
            var loader = holder.AddComponent<LoadingScreen>(); // Non-ExecuteAlways: Awake/Start never run here.
            var loadingEvents = Create(loading);
            Register(loadingEvents);
            Application.logMessageReceived += listener;
            Count(1, "Loading input");
            Call(loader, "SuspendLoadingEventSystems"); UnregisterDisabled();
            Check(loadingEvents.GetComponents<BaseInputModule>().All(m => !m.enabled), "Loading's input module was left enabled and can dispose the map's shared default actions later.");
            Count(0, "Before destination activation");

            destination = EditorSceneManager.OpenScene("Assets/Scenes/combattest.unity", OpenSceneMode.Additive);
            var authored = destination.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).ToArray();
            Check(authored.Length == 1, "combattest must contain exactly one authored EventSystem.");
            var clone = authored[0].gameObject;
            var battleEvents = clone.GetComponent<EventSystem>(); created.Add(battleEvents); Register(battleEvents);
            Count(1, "Combat activation while Loading remains loaded");
            var handoff = typeof(LoadingScreen).GetMethod("PreserveMapEventSystem", Private);
            handoff.Invoke(loader, new object[] { destination, loading });
            Check(battleEvents.gameObject.scene == destination && !loadingEvents.enabled, "Keep authored combat bindings and suspend the outgoing system.");
#if ENABLE_INPUT_SYSTEM
            var module = battleEvents.GetComponent<InputSystemUIInputModule>();
            Check(module != null && module.actionsAsset != null && module.point != null && module.leftClick != null &&
                module.move != null && module.submit != null && module.cancel != null, "Combat mouse/navigation bindings were lost.");
#endif
            EventSystem.current = battleEvents;
            var buttonObject = new GameObject("UI Input Recipient", typeof(RectTransform), typeof(Button));
            SceneManager.MoveGameObjectToScene(buttonObject, destination);
            int clicks = 0; buttonObject.GetComponent<Button>().onClick.AddListener(() => clicks++);
            battleEvents.SetSelectedGameObject(buttonObject);
            ExecuteEvents.Execute(buttonObject, new PointerEventData(battleEvents) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            Check(clicks == 1 && battleEvents.currentSelectedGameObject == buttonObject, "The destination lost click or navigation selection.");

            // Failed combat load: destination is disabled/unloaded before Retry is restored.
            clone.SetActive(false); UnregisterDisabled();
            Call(loader, "RestoreLoadingEventSystem"); Register(loadingEvents);
            Count(1, "Failure/Retry restores input");
            Check(loadingEvents.enabled, "Retry has no input after failure.");
            UnityEngine.Object.DestroyImmediate(clone);
            Call(loader, "SuspendLoadingEventSystems"); UnregisterDisabled();
            AdventureCanvasRoot.EnsureEventSystem(destination);
            var mapEvents = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
                .Single(s => s.isActiveAndEnabled);
#if ENABLE_INPUT_SYSTEM
            var mapModule = mapEvents.GetComponent<InputSystemUIInputModule>();
            mapModule.enabled = false; mapModule.AssignDefaultActions(); mapModule.enabled = true;
            Check(!EditorUtility.IsPersistent(mapModule.actionsAsset), "Map fixture must use runtime-generated defaults, not an Editor Reset asset.");
#endif
            created.Add(mapEvents); Register(mapEvents);
            Check(mapEvents.gameObject.scene == destination, "Destination UI was created in Loading and would be destroyed at unload.");
            AdventureCanvasRoot.EnsureEventSystem(destination); Count(1, "Repeated Ensure stays idempotent");
            Call(loader, "RestoreLoadingEventSystem");
            Count(1, "Map setup failure reuses destination input");
            Check(!loadingEvents.enabled, "Failure re-enabled a second EventSystem.");

            // Critical regression: the newly-created map must still receive
            // real Input System actions AFTER the old Loading module is gone.
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(loading, true); loading = default;
            Count(1, "New map after Loading unload");
#if ENABLE_INPUT_SYSTEM
            ValidateLiveActions(mapEvents.GetComponent<InputSystemUIInputModule>());
#endif
            mapEvents.gameObject.SetActive(false); UnregisterDisabled(); UnityEngine.Object.DestroyImmediate(mapEvents.gameObject);

            // Also cover destinations without a Canvas Awake/own EventSystem.
            loading = EditorSceneManager.OpenScene("Assets/Scenes/Loading.unity", OpenSceneMode.Additive);
            holder = new GameObject("Isolated Fallback Loading Controller"); SceneManager.MoveGameObjectToScene(holder, loading);
            loader = holder.AddComponent<LoadingScreen>();
            loadingEvents = Create(loading); Register(loadingEvents);
            Call(loader, "SuspendLoadingEventSystems"); UnregisterDisabled();
            handoff.Invoke(loader, new object[] { destination, loading }); Register(loadingEvents);
            Count(1, "Fallback input transfer");
            Check(loadingEvents.gameObject.scene == destination && loadingEvents.enabled, "Fallback did not survive Loading unload.");
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(loading, true); loading = default;
            Count(1, "After Loading unload");
#if ENABLE_INPUT_SYSTEM
            ValidateLiveActions(loadingEvents.GetComponent<InputSystemUIInputModule>());
#endif
            Check(warnings.Count == 0, "UGUI/UI Toolkit reported duplicate EventSystems: " + string.Join("\n", warnings));
            File.WriteAllText(Result, "PASS\nAuthored combat activation: one active EventSystem, existing input bindings retained.\nLoading suspends BOTH EventSystem and input module before destination activation.\nNew map and fallback input survive Loading unload: virtual mouse position/click and keyboard Submit actions delivered.\nFailure/Retry, repeated Ensure, navigation selection and fallback transfer verified.\nDuplicate EventSystem warnings: 0.\nNo campaign, Play Mode or player-save writes.");
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllText(Result, "FAIL\n" + error); Debug.LogException(error); }
        finally
        {
            Application.logMessageReceived -= listener;
            foreach (var module in enabledModules.ToArray()) if (module != null) DisableModule(module);
            enabledModules.Clear();
            foreach (var item in created) if (item != null && Registered.Contains(item)) Call(item, "OnDisable");
            created.Clear();
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            if (loading.IsValid()) EditorSceneManager.CloseScene(loading, true);
            if (destination.IsValid()) EditorSceneManager.CloseScene(destination, true);
            if (previous != null && Registered.Contains(previous)) EventSystem.current = previous;
            Cursor.visible = cursorVisible;
        }
    }
    static EventSystem Create(Scene scene)
    {
        var obj = new GameObject("Isolated Loading EventSystem"); SceneManager.MoveGameObjectToScene(obj, scene);
        var events = obj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        var module = obj.AddComponent<InputSystemUIInputModule>();
        // Editor Reset supplies an asset reference; runtime creation instead
        // uses package-generated shared defaults. Reproduce that exact path.
        module.enabled = false; module.AssignDefaultActions(); module.enabled = true;
#else
        obj.AddComponent<StandaloneInputModule>();
#endif
        created.Add(events); return events;
    }
    static void Register(EventSystem events)
    {
        if (!Registered.Contains(events)) Call(events, "OnEnable");
        foreach (var module in events.GetComponents<BaseInputModule>())
        {
            if (!module.isActiveAndEnabled || enabledModules.Contains(module)) continue;
            Call(module, "OnEnable"); enabledModules.Add(module);
        }
    }
    static void UnregisterDisabled()
    {
        foreach (var module in enabledModules.ToArray())
            if (module != null && !module.isActiveAndEnabled) { DisableModule(module); enabledModules.Remove(module); }
        foreach (var item in created) if (item != null && !item.isActiveAndEnabled && Registered.Contains(item)) Call(item, "OnDisable");
    }
    static void DisableModule(BaseInputModule module)
    {
#if ENABLE_INPUT_SYSTEM
        // The package's generated Dispose uses Object.Destroy, which is not
        // permitted in Edit Mode. Preserve its OnDisable behaviour and dispose
        // this fixture-owned default asset immediately after unhooking actions.
        var defaultsField = typeof(InputSystemUIInputModule).GetField("defaultActions", BindingFlags.Static | BindingFlags.NonPublic);
        var defaults = defaultsField.GetValue(null) as DefaultInputActions;
        var ui = module as InputSystemUIInputModule;
        if (defaults != null && ui != null && defaults.asset == ui.actionsAsset)
        {
            defaultsField.SetValue(null, null);
            Call(module, "OnDisable");
            ui.UnassignActions();
            UnityEngine.Object.DestroyImmediate(defaults.asset);
            return;
        }
#endif
        Call(module, "OnDisable");
    }
#if ENABLE_INPUT_SYSTEM
    static void ValidateLiveActions(InputSystemUIInputModule module)
    {
        Check(module != null && module.isActiveAndEnabled && module.point?.action?.enabled == true &&
            module.leftClick?.action?.enabled == true && module.move?.action?.enabled == true &&
            module.submit?.action?.enabled == true && module.cancel?.action?.enabled == true,
            "Map input actions were disabled/disposed when Loading unloaded.");
        int points = 0, clicks = 0, submits = 0;
        Action<InputAction.CallbackContext> point = _ => points++;
        Action<InputAction.CallbackContext> click = ctx => { if (ctx.ReadValueAsButton()) clicks++; };
        Action<InputAction.CallbackContext> submit = _ => submits++;
        module.point.action.performed += point; module.leftClick.action.performed += click; module.submit.action.performed += submit;
        Mouse mouse = null; Keyboard keyboard = null;
        // Action callbacks intentionally ignore Editor updates. Exercise only
        // the native input update (not Unity Play Mode/the gameplay loop), and
        // restore these transient manager flags in the same synchronous call.
        var manager = typeof(InputSystem).GetField("s_Manager", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var runUpdates = manager.GetType().GetProperty("runPlayerUpdatesInEditMode");
        bool previousUpdates = (bool)runUpdates.GetValue(manager);
        var focus = manager.GetType().GetField("m_HasFocus", BindingFlags.Instance | BindingFlags.NonPublic);
        bool previousFocus = (bool)focus.GetValue(manager);
        var update = typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null);
        try
        {
            runUpdates.SetValue(manager, true); focus.SetValue(manager, true);
            mouse = InputSystem.AddDevice<Mouse>("MapReturnValidationMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("MapReturnValidationKeyboard");
            update.Invoke(null, new object[] { InputUpdateType.Dynamic });
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(137, 193) }.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            update.Invoke(null, new object[] { InputUpdateType.Dynamic });
            Check(points > 0 && clicks > 0 && submits > 0,
                "Map actions did not receive queued input: point=" + points + ", click=" + clicks + ", submit=" + submits);
        }
        finally
        {
            module.point.action.performed -= point; module.leftClick.action.performed -= click; module.submit.action.performed -= submit;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            runUpdates.SetValue(manager, previousUpdates); focus.SetValue(manager, previousFocus);
        }
    }
#endif
    static void Count(int expected, string phase)
    {
        int actual = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(s => s.isActiveAndEnabled);
        Check(actual == expected, phase + ": expected " + expected + " active EventSystems, got " + actual);
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
