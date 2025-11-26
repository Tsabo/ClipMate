using System.IO;
using System.Windows;
using ClipMate.Core.Models.Configuration;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ClipMate.App.Views;

/// <summary>
/// First-run setup wizard for ClipMate.
/// Allows user to choose database name, location, and initializes the database with configuration.
/// </summary>
public partial class SetupWizard
{
    private readonly string _configurationDirectory;
    private readonly ILogger<SetupWizard> _logger;

    public SetupWizard(ILogger<SetupWizard> logger, string? configurationDirectory = null)
    {
        InitializeComponent();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default configuration directory
        _configurationDirectory = configurationDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        // Default path
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        DatabasePath = Path.Combine(appDataPath, "clipmate.db");
        DatabasePathTextBox.Text = DatabasePath;
        DatabaseNameTextBox.Text = "My Clips";
    }

    public string DatabasePath { get; private set; }

    public string DatabaseName { get; private set; } = "My Clips";
    public bool SetupCompleted { get; private set; }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Choose Database Location",
            FileName = "clipmate.db",
            DefaultExt = ".db",
            Filter = "SQLite Database|*.db|All Files|*.*",
            InitialDirectory = Path.GetDirectoryName(DatabasePath),
        };

        if (dialog.ShowDialog() == true)
        {
            DatabasePath = dialog.FileName;
            DatabasePathTextBox.Text = DatabasePath;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Setup cancelled by user");
        SetupCompleted = false;
        Close();
    }

    private async void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate database name
        var databaseName = DatabaseNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            MessageBox.Show(
                "Please enter a name for your database.",
                "Database Name Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            DatabaseNameTextBox.Focus();
            return;
        }

        // Validate path
        var directory = Path.GetDirectoryName(DatabasePath);
        if (string.IsNullOrEmpty(directory))
        {
            MessageBox.Show(
                "Invalid database path. Please choose a valid location.",
                "Invalid Path",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // Show progress overlay
        ProgressOverlay.Visibility = Visibility.Visible;
        ContinueButton.IsEnabled = false;
        BrowseButton.IsEnabled = false;
        DatabaseNameTextBox.IsEnabled = false;

        try
        {
            _logger.LogInformation("Starting database setup: Name={Name}, Path={Path}", databaseName, DatabasePath);

            // Create directory if it doesn't exist
            if (!Directory.Exists(directory))
            {
                _logger.LogInformation("Creating directory: {Directory}", directory);
                Directory.CreateDirectory(directory);
            }

            // Update progress
            ProgressText.Text = "Creating database schema...";
            await Task.Delay(100); // Let UI update

            // Create DbContext options
            var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
            optionsBuilder.UseSqlite($"Data Source={DatabasePath}");

            // Create database and migrate schema
            await using var context = new ClipMateDbContext(optionsBuilder.Options);

            _logger.LogInformation("Creating database and applying schema migrations...");
            await context.Database.EnsureCreatedAsync();
            
            var migrationService = new DatabaseSchemaMigrationService(_logger as ILogger<DatabaseSchemaMigrationService>);
            await migrationService.MigrateAsync(context);

            // Update progress
            ProgressText.Text = "Seeding default data...";
            await Task.Delay(100);

            // Seed default collections
            _logger.LogInformation("Seeding default collections...");
            var seeder = new DefaultDataSeeder(context, _logger as ILogger<DefaultDataSeeder>);
            await seeder.SeedDefaultDataAsync();

            // Update progress
            ProgressText.Text = "Saving configuration...";
            await Task.Delay(100);

            // Save configuration
            _logger.LogInformation("Creating configuration...");
            await SaveConfigurationAsync(databaseName, directory);

            _logger.LogInformation("Database setup completed successfully");

            DatabaseName = databaseName;
            SetupCompleted = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up database");

            ProgressOverlay.Visibility = Visibility.Collapsed;
            ContinueButton.IsEnabled = true;
            BrowseButton.IsEnabled = true;
            DatabaseNameTextBox.IsEnabled = true;

            MessageBox.Show(
                $"Failed to set up database:\n\n{ex.Message}\n\nPlease try a different location or check permissions.",
                "Setup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task SaveConfigurationAsync(string databaseName, string databaseDirectory)
    {
        try
        {
            // Create configuration service
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var configLogger = loggerFactory.CreateLogger<ConfigurationService>();
            var configService = new ConfigurationService(_configurationDirectory, configLogger);

            // Load or create configuration
            var config = await configService.LoadAsync();

            // Add database configuration
            var dbConfig = new DatabaseConfiguration
            {
                Name = databaseName,
                Directory = databaseDirectory,
                AutoLoad = true,
                AllowBackup = true,
                ReadOnly = false,
                CleanupMethod = 3, // Daily
                PurgeDays = 7,
            };

            // Use "default" as the key for the first database
            config.Databases["default"] = dbConfig;
            config.DefaultDatabase = "default";

            // Save configuration
            await configService.SaveAsync(config);

            _logger.LogInformation("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
            throw new InvalidOperationException("Failed to save database configuration", ex);
        }
    }
}
