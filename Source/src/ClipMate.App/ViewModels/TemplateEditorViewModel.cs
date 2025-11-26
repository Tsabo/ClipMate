using System.Collections.ObjectModel;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Template Editor dialog, managing template CRUD operations and preview functionality.
/// </summary>
public partial class TemplateEditorViewModel : ObservableObject
{
    private readonly ITemplateService _templateService;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ObservableCollection<string> _extractedVariables = [];

    [ObservableProperty]
    private string? _previewText;

    [ObservableProperty]
    private Template? _selectedTemplate;

    [ObservableProperty]
    private string _templateContent = string.Empty;

    [ObservableProperty]
    private string? _templateDescription = string.Empty;

    [ObservableProperty]
    private string _templateName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Template> _templates = [];

    /// <summary>
    /// Initializes a new instance of the TemplateEditorViewModel class.
    /// </summary>
    /// <param name="templateService">The template service.</param>
    /// <exception cref="ArgumentNullException">Thrown when templateService is null.</exception>
    public TemplateEditorViewModel(ITemplateService templateService)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
    }

    /// <summary>
    /// Gets whether the form has valid data for creating/updating a template.
    /// </summary>
    public bool IsFormValid => !string.IsNullOrWhiteSpace(TemplateName) && !string.IsNullOrWhiteSpace(TemplateContent);

    partial void OnTemplateContentChanged(string value)
    {
        // Extract variables when content changes
        if (!string.IsNullOrWhiteSpace(value))
        {
            var variables = _templateService.ExtractVariables(value);
            ExtractedVariables = new ObservableCollection<string>(variables);
        }
        else
            ExtractedVariables.Clear();

        // Notify that form validity may have changed
        OnPropertyChanged(nameof(IsFormValid));
    }

    partial void OnTemplateNameChanged(string value)
    {
        // Notify that form validity may have changed
        OnPropertyChanged(nameof(IsFormValid));
    }

    partial void OnSelectedTemplateChanged(Template? value)
    {
        if (value != null)
        {
            TemplateName = value.Name;
            TemplateContent = value.Content;
            TemplateDescription = value.Description;
        }
    }

    /// <summary>
    /// Loads all templates from the repository.
    /// </summary>
    [RelayCommand]
    private async Task LoadTemplatesAsync()
    {
        try
        {
            ErrorMessage = null;
            var templates = await _templateService.GetAllAsync();
            Templates = new ObservableCollection<Template>(templates);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load templates: {ex.Message}";
        }
    }

    /// <summary>
    /// Creates a new template with the current form data.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreateTemplate))]
    private async Task CreateTemplateAsync()
    {
        try
        {
            ErrorMessage = null;
            _ = await _templateService.CreateAsync(
                TemplateName,
                TemplateContent,
                TemplateDescription);

            // Reload templates to include the new one
            await LoadTemplatesAsync();

            // Clear form after successful creation
            ClearForm();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create template: {ex.Message}";
        }
    }

    private bool CanCreateTemplate() => IsFormValid;

    /// <summary>
    /// Updates the selected template with the current form data.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdateTemplate))]
    private async Task UpdateTemplateAsync()
    {
        if (SelectedTemplate == null)
            return;

        try
        {
            ErrorMessage = null;
            SelectedTemplate.Name = TemplateName;
            SelectedTemplate.Content = TemplateContent;
            SelectedTemplate.Description = TemplateDescription;

            await _templateService.UpdateAsync(SelectedTemplate);

            // Reload templates to reflect changes
            await LoadTemplatesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update template: {ex.Message}";
        }
    }

    private bool CanUpdateTemplate() => SelectedTemplate != null;

    /// <summary>
    /// Deletes the selected template.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteTemplate))]
    private async Task DeleteTemplateAsync()
    {
        if (SelectedTemplate == null)
            return;

        try
        {
            ErrorMessage = null;
            await _templateService.DeleteAsync(SelectedTemplate.Id);

            // Clear form and reload templates
            ClearForm();
            await LoadTemplatesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete template: {ex.Message}";
        }
    }

    private bool CanDeleteTemplate() => SelectedTemplate != null;

    /// <summary>
    /// Clears all form fields.
    /// </summary>
    [RelayCommand]
    private void ClearForm()
    {
        TemplateName = string.Empty;
        TemplateContent = string.Empty;
        TemplateDescription = string.Empty;
        SelectedTemplate = null;
        PreviewText = null;
        ExtractedVariables.Clear();
        ErrorMessage = null;
    }

    /// <summary>
    /// Previews the template with variable expansion.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPreviewTemplate))]
    private async Task PreviewTemplateAsync()
    {
        if (SelectedTemplate == null)
            return;

        try
        {
            ErrorMessage = null;

            // For preview, we'll expand with empty custom variables
            // Built-in variables like DATE, TIME, USERNAME will still be expanded
            var expandedContent = await _templateService.ExpandTemplateAsync(
                SelectedTemplate.Id,
                new Dictionary<string, string>());

            PreviewText = expandedContent;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to preview template: {ex.Message}";
            PreviewText = null;
        }
    }

    private bool CanPreviewTemplate() => SelectedTemplate != null;
}
