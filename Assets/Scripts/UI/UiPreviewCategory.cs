using System;
using UnityEngine;

[Flags]
public enum UiPreviewCategory
{
    None = 0,
    Gameplay = 1 << 0,
    Hud = 1 << 1,
    Quest = 1 << 2,
    Dialog = 1 << 3,
    Inventory = 1 << 4,
    Stats = 1 << 5,
    Wanted = 1 << 6,
    Interaction = 1 << 7,
    Pause = 1 << 8,
    Menu = 1 << 9,
    System = 1 << 10,
    Debug = 1 << 11,
    Other = 1 << 12
}

/// <summary>
/// Marks a top-level UI branch with one or more preview categories. The marker
/// only affects the editor preview; runtime visibility is restored automatically.
/// </summary>
public sealed class UiPreviewGroup : MonoBehaviour
{
    [SerializeField]
    private UiPreviewCategory categories = UiPreviewCategory.Other;

    [SerializeField]
    private bool includeInPreview = true;

    [SerializeField, HideInInspector]
    private bool hiddenByPreview;

    public UiPreviewCategory Categories => categories;
    public bool IncludeInPreview => includeInPreview;
    public bool HiddenByPreview => hiddenByPreview;

    public void Configure(UiPreviewCategory newCategories)
    {
        categories = newCategories == UiPreviewCategory.None ? UiPreviewCategory.Other : newCategories;
    }

    public void ApplyPreview(bool previewEnabled, UiPreviewCategory selectedCategories)
    {
        bool matches = !previewEnabled || !includeInPreview || categories == UiPreviewCategory.None ||
                       (categories & selectedCategories) != 0;

        if (matches)
        {
            if (hiddenByPreview)
            {
                hiddenByPreview = false;
                gameObject.SetActive(true);
            }

            return;
        }

        if (gameObject.activeSelf)
        {
            hiddenByPreview = true;
            gameObject.SetActive(false);
        }
    }

    public void RestoreRuntimeVisibility()
    {
        if (!hiddenByPreview)
            return;

        hiddenByPreview = false;
        gameObject.SetActive(true);
    }
}
