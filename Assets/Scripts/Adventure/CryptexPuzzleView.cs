using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Modal UI and an isolated live model preview. No exploration camera or EventSystem is replaced.</summary>
public sealed class CryptexPuzzleView : MonoBehaviour
{
    public TMP_Text title, message, closeLabel, unlockLabel;
    public RawImage preview;
    public Transform ringControls;
    public Button ringTemplate, unlockButton, closeButton;
    public Color selectedColor = new Color(.83f, .74f, .52f);
    public Color normalColor = new Color(.12f, .12f, .13f);
    CryptexLock puzzle;
    CampaignSession session;
    GameObject stage;
    Camera previewCamera;
    RenderTexture texture;
    readonly List<Transform> originals = new List<Transform>(), copies = new List<Transform>();
    readonly List<Button> ringButtons = new List<Button>();
    readonly List<TMP_Text> letters = new List<TMP_Text>();
    GameObject oldSelection;
    int selected;
    bool closing, wrong;
    GameLanguage language;

    public void Open(CryptexLock source, CampaignSession owner)
    {
        puzzle = source; session = owner;
        oldSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        ringTemplate.gameObject.SetActive(false);
        for (int i = 0; i < puzzle.rings.Length; i++)
        {
            int index = i;
            var button = Instantiate(ringTemplate, ringControls);
            button.name = "Ring " + (i + 1); button.gameObject.SetActive(true);
            button.onClick.AddListener(() => SelectRing(index));
            var up = button.transform.Find("Up").GetComponent<Button>();
            var down = button.transform.Find("Down").GetComponent<Button>();
            up.onClick.AddListener(() => { SelectRing(index); Rotate(1); });
            down.onClick.AddListener(() => { SelectRing(index); Rotate(-1); });
            ringButtons.Add(button); letters.Add(button.transform.Find("Letter").GetComponent<TMP_Text>());
        }
        unlockButton.onClick.AddListener(AttemptUnlock);
        closeButton.onClick.AddListener(() => puzzle.Close());
        BuildPreview(); SelectRing(0); RefreshLanguage(); RefreshLetters();
    }
    void SelectRing(int index)
    {
        selected = index;
        for (int i = 0; i < ringButtons.Count; i++) ringButtons[i].image.color = i == index ? selectedColor : normalColor;
    }
    void Rotate(int direction)
    {
        if (puzzle.IsUnlocked) return;
        puzzle.RotateRing(selected, direction); wrong = false; RefreshLetters(); RefreshMessage();
    }
    void AttemptUnlock()
    {
        if (puzzle.IsTurning || puzzle.IsUnlocked) return;
        wrong = !puzzle.TryUnlock(session); RefreshMessage();
    }
    void Update()
    {
        if (puzzle == null || session == null || !session.OnMap || session.Menu != AdventureMenu.Cryptex || LoadingScreen.IsLoading)
        { if (puzzle != null) puzzle.Close(); else Close(); return; }
        if (language != GameLocalization.CurrentLanguage) RefreshLanguage();
        if (puzzle.IsUnlocked) { unlockButton.interactable = false; closeButton.interactable = false; return; }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { puzzle.Close(); return; }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SelectRing((selected + ringButtons.Count - 1) % ringButtons.Count);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SelectRing((selected + 1) % ringButtons.Count);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Rotate(1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Rotate(-1);
        if (Input.mouseScrollDelta.y != 0) Rotate(Input.mouseScrollDelta.y > 0 ? 1 : -1);
        // Return/Space are owned by UGUI when a button has focus; do not submit twice.
        if ((EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null) && Input.GetKeyDown(KeyCode.Return)) AttemptUnlock();
        unlockButton.interactable = !puzzle.IsTurning;
    }
    void LateUpdate()
    {
        for (int i = 0; i < originals.Count; i++)
            if (originals[i] != null && copies[i] != null)
            { copies[i].localPosition = originals[i].localPosition; copies[i].localRotation = originals[i].localRotation; copies[i].localScale = originals[i].localScale; }
    }
    void RefreshLetters() { for (int i = 0; i < letters.Count; i++) letters[i].text = puzzle.LetterAt(i).ToString(); }
    void RefreshLanguage()
    {
        language = GameLocalization.CurrentLanguage;
        title.text = GameLocalization.IsEnglish ? "CRYPTEX" : "Ổ KHÓA MẬT MÃ";
        closeLabel.text = GameLocalization.IsEnglish ? "Back" : "Trở lại";
        unlockLabel.text = GameLocalization.IsEnglish ? "Unlock" : "Mở khóa";
        RefreshMessage();
    }
    void RefreshMessage()
    {
        message.text = puzzle.IsUnlocked ? (GameLocalization.IsEnglish ? "Unlocked" : "Đã mở khóa") :
            wrong ? (GameLocalization.IsEnglish ? "Incorrect combination" : "Mật mã chưa đúng") : "";
    }
    void BuildPreview()
    {
        stage = new GameObject("Cryptex Preview Stage");
        SceneManager.MoveGameObjectToScene(stage, gameObject.scene);
        stage.transform.position = new Vector3(0, -3000, 0);
        var clone = Instantiate(puzzle.modelRoot.gameObject, stage.transform, false);
        clone.name = "Cryptex Model";
        foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        CopyChildren(puzzle.modelRoot, clone.transform);
        var meshes = clone.GetComponentsInChildren<Renderer>();
        var bounds = meshes[0].bounds;
        for (int i = 1; i < meshes.Length; i++) bounds.Encapsulate(meshes[i].bounds);
        var cameraObject = new GameObject("Cryptex Preview Camera"); cameraObject.transform.SetParent(stage.transform, false);
        previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.cullingMask = 1 << 30;
        previewCamera.scene = gameObject.scene;
        previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.025f, .025f, .03f, 1);
        previewCamera.orthographic = true; previewCamera.aspect = 2.4f;
        previewCamera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / previewCamera.aspect) * 1.25f;
        previewCamera.transform.position = bounds.center + new Vector3(0, .015f, -2);
        previewCamera.transform.LookAt(bounds.center); previewCamera.nearClipPlane = .01f; previewCamera.farClipPlane = 6;
        previewCamera.useOcclusionCulling = false;
        var data = previewCamera.GetUniversalAdditionalCameraData(); data.renderShadows = false; data.renderPostProcessing = false;
        data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
        texture = new RenderTexture(1200, 500, 24); texture.Create(); preview.texture = texture; previewCamera.targetTexture = texture;
        AddLight(new Vector3(-.3f, .4f, -.4f), 1.1f, new Color(1, .93f, .8f));
        AddLight(new Vector3(.4f, 0, .4f), .65f, new Color(.75f, .85f, 1));
    }
    void CopyChildren(Transform from, Transform to)
    {
        // Model root's authoring rotation/scale stay unchanged; each child includes its calibrated ring pivot.
        for (int i = 0; i < from.childCount; i++)
        { originals.Add(from.GetChild(i)); copies.Add(to.GetChild(i)); CopyChildren(from.GetChild(i), to.GetChild(i)); }
    }
    void AddLight(Vector3 offset, float intensity, Color color)
    {
        var obj = new GameObject("Preview Light"); obj.transform.SetParent(stage.transform, false); obj.transform.localPosition = offset;
        var light = obj.AddComponent<Light>(); light.type = LightType.Point; light.range = 2; light.intensity = intensity; light.color = color;
        light.shadows = LightShadows.None; light.cullingMask = 1 << 30;
    }
    public void Close()
    {
        if (closing) return;
        closing = true; gameObject.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(oldSelection != null && oldSelection.activeInHierarchy ? oldSelection : null);
        Destroy(gameObject);
    }
    void OnDisable() { if (!closing && puzzle != null) puzzle.Close(); }
    void OnDestroy()
    {
        if (previewCamera != null) previewCamera.targetTexture = null;
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (stage != null) Destroy(stage);
    }
}
