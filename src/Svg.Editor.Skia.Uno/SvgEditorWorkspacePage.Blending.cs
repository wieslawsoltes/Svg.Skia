using System;
using System.Collections.ObjectModel;
using Svg;
using Svg.Editor.Skia.Uno.Models;
using Svg.Model.Services;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    private string _selectedBlendModeId = BlendModeService.NormalToken;
    private bool _isUpdatingBlendModeState;

    public string SelectedBlendModeId
    {
        get => _selectedBlendModeId;
        set
        {
            if (SetField(ref _selectedBlendModeId, value) && !_isUpdatingBlendModeState)
            {
                ApplyBlendModeState();
            }
        }
    }

    public bool CanEditBlendMode => _selectedElements.Count > 0;

    public ObservableCollection<EditorBlendModeItem> BlendModes { get; } = new(EditorBlendModeCatalog.CreateDefault());

    private void ApplyBlendModeState()
    {
        if (_selectedElements.Count == 0
            || string.Equals(SelectedBlendModeId, EditorBlendModeCatalog.MixedId, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var element in _selectedElements)
        {
            BlendModeService.SetBlendModeToken(element, SelectedBlendModeId);
        }

        RefreshDocumentVisual(rebuildOutline: false, reloadProperties: true);
    }

    private void RefreshBlendModeInspectorState()
    {
        _isUpdatingBlendModeState = true;
        try
        {
            if (_selectedElements.Count == 0)
            {
                SelectedBlendModeId = BlendModeService.NormalToken;
                return;
            }

            string? selectedMode = null;
            foreach (var element in _selectedElements)
            {
                var elementMode = GetInspectorBlendModeId(element);
                if (selectedMode is null)
                {
                    selectedMode = elementMode;
                    continue;
                }

                if (!string.Equals(selectedMode, elementMode, StringComparison.Ordinal))
                {
                    SelectedBlendModeId = EditorBlendModeCatalog.MixedId;
                    return;
                }
            }

            SelectedBlendModeId = selectedMode ?? BlendModeService.NormalToken;
        }
        finally
        {
            _isUpdatingBlendModeState = false;
        }
    }

    private static string GetInspectorBlendModeId(SvgVisualElement element)
    {
        var token = BlendModeService.GetBlendModeToken(element);
        if (token is not null)
        {
            return token;
        }

        return element is SvgGroup
            ? BlendModeService.PassThroughToken
            : BlendModeService.NormalToken;
    }
}
