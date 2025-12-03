using System.Text.RegularExpressions;
using ClipMate.Core.Models;
using ClipMate.Core.Repositories;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing text templates with variable substitution.
/// Supports built-in variables ({DATE}, {TIME}, {USERNAME}, {COMPUTERNAME})
/// and custom variables with format strings and prompt dialogs.
/// </summary>
public partial class TemplateService : ITemplateService
{
    private readonly ITemplateRepository _repository;

    /// <summary>
    /// Initializes a new instance of the TemplateService class.
    /// </summary>
    /// <param name="repository">The template repository.</param>
    /// <exception cref="ArgumentNullException">Thrown when repository is null.</exception>
    public TemplateService(ITemplateRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<Template?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => await _repository.GetByIdAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken cancellationToken = default) => await _repository.GetAllAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Template>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default) => await _repository.GetByCollectionAsync(collectionId, cancellationToken);

    /// <inheritdoc />
    public async Task<Template> CreateAsync(string name, string content, string? description = null, Guid? collectionId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be null or whitespace.", nameof(name));

        if (string.IsNullOrEmpty(content))
            throw new ArgumentException("Template content cannot be null or empty.", nameof(content));

        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = name,
            Content = content,
            Description = description,
            CollectionId = collectionId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            UseCount = 0,
            SortOrder = 0
        };

        return await _repository.CreateAsync(template, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Template template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        template.ModifiedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(template, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => await _repository.DeleteAsync(id, cancellationToken);

    /// <inheritdoc />
    public async Task<string> ExpandTemplateAsync(Guid templateId, Dictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(templateId, cancellationToken);

        if (template == null)
            throw new KeyNotFoundException($"Template with ID {templateId} not found.");

        var expandedContent = template.Content;

        // Replace built-in variables
        expandedContent = ReplaceBuiltInVariables(expandedContent);

        // Replace custom variables
        foreach (var variable in variables)
        {
            var pattern = $"{{{variable.Key}}}";
            expandedContent = expandedContent.Replace(pattern, variable.Value, StringComparison.OrdinalIgnoreCase);
        }

        // Increment use count
        template.UseCount++;
        await _repository.UpdateAsync(template, cancellationToken);

        return expandedContent;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ExtractVariables(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var regex = VariableRegex();
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                // Extract variable name (before colon if format string exists)
                var variableName = match.Groups[1].Value;
                var colonIndex = variableName.IndexOf(':');
                if (colonIndex > 0)
                    variableName = variableName[..colonIndex];

                variables.Add(variableName);
            }
        }

        return variables.ToList();
    }

    /// <summary>
    /// Replaces built-in variables with their current values.
    /// </summary>
    /// <param name="content">The template content.</param>
    /// <returns>Content with built-in variables replaced.</returns>
    private string ReplaceBuiltInVariables(string content)
    {
        // Replace DATE variables with format strings
        content = ReplaceDateVariables(content);

        // Replace TIME variables with format strings
        content = ReplaceTimeVariables(content);

        // Replace USERNAME
        var username = Environment.UserName;
        content = content.Replace("{USERNAME}", username, StringComparison.OrdinalIgnoreCase);

        // Replace COMPUTERNAME
        var computerName = Environment.MachineName;
        content = content.Replace("{COMPUTERNAME}", computerName, StringComparison.OrdinalIgnoreCase);

        return content;
    }

    /// <summary>
    /// Replaces DATE variables with formatted date strings.
    /// </summary>
    /// <param name="content">The template content.</param>
    /// <returns>Content with DATE variables replaced.</returns>
    private string ReplaceDateVariables(string content)
    {
        var dateRegex = DateVariableRegex();

        return dateRegex.Replace(content, match =>
        {
            var format = match.Groups[1].Success
                ? match.Groups[1].Value
                : "d"; // Default short date format

            try
            {
                return DateTime.Now.ToString(format);
            }
            catch (FormatException)
            {
                // If format is invalid, use default
                return DateTime.Now.ToShortDateString();
            }
        });
    }

    /// <summary>
    /// Replaces TIME variables with formatted time strings.
    /// </summary>
    /// <param name="content">The template content.</param>
    /// <returns>Content with TIME variables replaced.</returns>
    private string ReplaceTimeVariables(string content)
    {
        var timeRegex = TimeVariableRegex();

        return timeRegex.Replace(content, match =>
        {
            var format = match.Groups[1].Success
                ? match.Groups[1].Value
                : "t"; // Default short time format

            try
            {
                return DateTime.Now.ToString(format);
            }
            catch (FormatException)
            {
                // If format is invalid, use default
                return DateTime.Now.ToShortTimeString();
            }
        });
    }

    /// <summary>
    /// Regular expression for matching variables in {braces}.
    /// </summary>
    [GeneratedRegex(@"\{([^}]+)\}", RegexOptions.IgnoreCase)]
    private static partial Regex VariableRegex();

    /// <summary>
    /// Regular expression for matching DATE variables with optional format strings.
    /// </summary>
    [GeneratedRegex(@"\{DATE(?::([^}]+))?\}", RegexOptions.IgnoreCase)]
    private static partial Regex DateVariableRegex();

    /// <summary>
    /// Regular expression for matching TIME variables with optional format strings.
    /// </summary>
    [GeneratedRegex(@"\{TIME(?::([^}]+))?\}", RegexOptions.IgnoreCase)]
    private static partial Regex TimeVariableRegex();
}
