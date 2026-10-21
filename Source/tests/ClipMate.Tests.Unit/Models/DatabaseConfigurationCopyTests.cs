using System.Reflection;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.Models;

/// <summary>
/// Every copy of a <see cref="DatabaseConfiguration" /> keeps every setting, including ones added after the copy was
/// written: a hand-written field-by-field copy silently drops new settings (it once dropped Share with Peers).
/// </summary>
public class DatabaseConfigurationCopyTests
{
    [Test]
    public async Task Clone_KeepsEverySetting()
    {
        // Arrange
        var original = CreateWithEverySettingChanged();

        // Act
        var copy = original.Clone();

        // Assert
        await Assert.That(copy).IsNotSameReferenceAs(original);
        await Assert.That(DifferingSettings(original, copy)).IsEmpty();
    }

    [Test]
    public async Task DatabaseBackup_ToConfiguration_KeepsEverySettingButTheBackupFolder()
    {
        // Arrange
        var original = CreateWithEverySettingChanged();
        var viewModel = new DatabaseBackupViewModel(original, 7, 0) { BackupDirectory = @"D:\Backups" };

        // Act
        var copy = viewModel.ToConfiguration();

        // Assert
        await Assert.That(copy.BackupDirectory).IsEqualTo(@"D:\Backups");
        await Assert.That(DifferingSettings(original, copy, nameof(DatabaseConfiguration.BackupDirectory))).IsEmpty();
    }

    [Test]
    public async Task MultipleDatabaseBackup_SelectedDatabases_KeepEverySettingButTheBackupFolder()
    {
        // Arrange
        var original = CreateWithEverySettingChanged();
        var viewModel = new MultipleDatabaseBackupViewModel([original], 7, 0) { SharedBackupDirectory = @"D:\Backups" };

        // Act
        var copy = viewModel.GetSelectedDatabasesWithUpdatedSettings().Single();

        // Assert
        await Assert.That(copy.BackupDirectory).IsEqualTo(@"D:\Backups");
        await Assert.That(DifferingSettings(original, copy, nameof(DatabaseConfiguration.BackupDirectory))).IsEmpty();
    }

    private static IEnumerable<PropertyInfo> Settings =>
        typeof(DatabaseConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p is { CanRead: true, CanWrite: true });

    /// <summary>
    /// A configuration whose every setting differs from its default, so a copy that drops one is caught.
    /// </summary>
    private static DatabaseConfiguration CreateWithEverySettingChanged()
    {
        var configuration = new DatabaseConfiguration();
        foreach (var item in Settings)
            item.SetValue(configuration, ChangedValue(item.PropertyType, item.GetValue(configuration)));

        return configuration;
    }

    private static object? ChangedValue(Type type, object? current)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string))
            return $"changed-{current}";
        if (underlying == typeof(bool))
            return !(bool)(current ?? false);
        if (underlying == typeof(int))
            return (int)(current ?? 0) + 17;
        if (underlying == typeof(DateTime))
            return new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Local);
        if (underlying == typeof(TimeSpan))
            return TimeSpan.FromMinutes(77);
        if (underlying.IsEnum)
        {
            var values = Enum.GetValues(underlying);
            foreach (var item in values)
            {
                if (!Equals(item, current))
                    return item;
            }
        }

        throw new NotSupportedException($"Add a changed value for {type.Name} so new {nameof(DatabaseConfiguration)} settings are covered.");
    }

    private static List<string> DifferingSettings(DatabaseConfiguration expected, DatabaseConfiguration actual, params string[] except) =>
        Settings
            .Where(p => !except.Contains(p.Name) && !Equals(p.GetValue(expected), p.GetValue(actual)))
            .Select(p => $"{p.Name}: expected {p.GetValue(expected)}, got {p.GetValue(actual)}")
            .ToList();
}
