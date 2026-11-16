using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Stores editor preview filters for one Canvas and applies them to its direct
/// UI branches. The filter is deliberately ignored during gameplay.
/// </summary>
[ExecuteAlways]
public sealed class UiPreviewController : MonoBehaviour
{
    [SerializeField]
    private bool previewEnabled = true;

    [SerializeField]
    private UiPreviewCategory selectedCategories = UiPreviewCategory.Gameplay | UiPreviewCategory.Hud;

    public bool PreviewEnabled => previewEnabled;
    public UiPreviewCategory SelectedCategories => selectedCategories;

    public void SetFilter(bool enabled, UiPreviewCategory categories)
    {
        previewEnabled = enabled;
        selectedCategories = categories;
        ApplyPreview();
    }

    public void ApplyPreview()
    {
        UiPreviewGroup[] groups = GetComponentsInChildren<UiPreviewGroup>(true);
        foreach (UiPreviewGroup group in groups)
            group.ApplyPreview(previewEnabled && !Application.isPlaying, selectedCategories);
    }

    public void ShowAll()
    {
        previewEnabled = false;
        ApplyPreview();
    }

    public static void EnsureSceneSetup(Scene scene)
    {
        if (Application.isPlaying || !scene.IsValid() || !scene.isLoaded)
            return;

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.gameObject.scene != scene)
                continue;

            EnsureCanvasSetup(canvas, scene.name);
        }
    }

    public static UiPreviewController EnsureCanvasSetup(Canvas canvas, string sceneName)
    {
        if (canvas == null)
            return null;

        UiPreviewController controller = canvas.GetComponent<UiPreviewController>();
        bool created = controller == null;
        if (created)
        {
            controller = canvas.gameObject.AddComponent<UiPreviewController>();
            controller.previewEnabled = true;
            controller.selectedCategories = sceneName == "Menu"
                ? UiPreviewCategory.Menu
                : UiPreviewCategory.Gameplay | UiPreviewCategory.Hud;
        }

        Transform canvasTransform = canvas.transform;
        Transform[] descendants = canvas.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in descendants)
        {
            if (child == canvasTransform)
                continue;

            if (child.GetComponent<RectTransform>() == null ||
                child.GetComponentInChildren<Graphic>(true) == null)
                continue;

            if (!IsCategoryRoot(child.name, sceneName))
            {
                // GameplayUi is a shared container in Main, not a category. Its
                // children must be allowed to select their own preview group.
                if (sceneName == "Main" && child.name == "GameplayUi")
                {
                    UiPreviewGroup staleGroup = child.GetComponent<UiPreviewGroup>();
                    if (staleGroup != null)
                        Object.DestroyImmediate(staleGroup);
                }

                continue;
            }

            UiPreviewGroup group = child.GetComponent<UiPreviewGroup>();
            if (group == null)
                group = child.gameObject.AddComponent<UiPreviewGroup>();

            group.Configure(Categorize(child.name, sceneName));
        }

        if (created || controller.previewEnabled)
            controller.ApplyPreview();

        return controller;
    }

    private static bool IsCategoryRoot(string objectName, string sceneName)
    {
        string name = objectName.Trim().ToLowerInvariant();
        if (sceneName == "Menu")
            return name == "menuwindow" || name == "menubackdrop" || name.Contains("menubrand");

        return name == "questwindow" || name == "statswindow" || name == "dialogwindow" ||
               name == "wantedwindow" || name == "stealingwindow" || name == "inventorywindow" ||
               name == "attemptpanel" || name == "timerpanel" ||
               name == "stagewindow" || name == "itempanel" || name == "steampanel" || name == "lockpanel" ||
               name == "pausewindow" || name == "modernhudheader" || name == "fpscounter";
    }

    public static void RestoreRuntimeVisibility()
    {
        UiPreviewGroup[] groups = Object.FindObjectsByType<UiPreviewGroup>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UiPreviewGroup group in groups)
            group.RestoreRuntimeVisibility();
    }

    public static UiPreviewCategory Categorize(string objectName, string sceneName)
    {
        if (sceneName == "Menu")
            return UiPreviewCategory.Menu;

        string name = objectName.Trim().ToLowerInvariant();

        if (name.Contains("pause"))
            return UiPreviewCategory.Pause | UiPreviewCategory.Dialog;

        if (name.Contains("dialog") || name.Contains("popup") || name.Contains("steal") || name.Contains("lock") ||
            name.Contains("priceinput") || name.Contains("quantityinput"))
            return UiPreviewCategory.Dialog | UiPreviewCategory.Interaction;

        if (name.Contains("inventory") || name.Contains("itempanel") || name.Contains("inv"))
            return UiPreviewCategory.Gameplay | UiPreviewCategory.Inventory;

        if (name.Contains("wanted"))
            return UiPreviewCategory.Gameplay | UiPreviewCategory.Hud | UiPreviewCategory.Wanted;

        if (name.Contains("quest"))
            return UiPreviewCategory.Gameplay | UiPreviewCategory.Hud | UiPreviewCategory.Quest;

        if (name.Contains("stat"))
            return UiPreviewCategory.Gameplay | UiPreviewCategory.Hud | UiPreviewCategory.Stats;

        if (name.Contains("fps"))
            return UiPreviewCategory.Debug;

        if (name.Contains("stage") || name.Contains("timer") || name.Contains("attempt") ||
            name.Contains("amount") || name.Contains("steam") || name.Contains("hud") ||
            name.Contains("header"))
            return UiPreviewCategory.Gameplay | UiPreviewCategory.Hud;

        if (name.Contains("menu"))
            return UiPreviewCategory.Menu;

        return UiPreviewCategory.Gameplay | UiPreviewCategory.Other;
    }
}
