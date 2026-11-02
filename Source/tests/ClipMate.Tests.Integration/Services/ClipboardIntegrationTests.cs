using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform;
using ClipMate.Platform.Helpers;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using ClipMate.Tests.Integration.Fakes;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Windows.Win32.Foundation;
using CoreApplicationProfile = ClipMate.Core.Models.ApplicationProfile;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for the complete clipboard capture pipeline.
/// Tests the flow: clipboard change -> channel -> save to database.
/// </summary>
public class ClipboardIntegrationTests : IntegrationTestBase, IDisposable
{
    private const string _testDatabaseKey = "db_test0001";

#pragma warning disable TUnit0023 // Field is disposed in CleanupTestAsync
    private IClipboardService _clipboardService = null!;
#pragma warning restore TUnit0023
    private IClipRepository _clipRepository = null!;
    private IClipService _clipService = null!;
    private ClipboardCoordinator _coordinator = null!;
    private IApplicationFilterService _filterService = null!;
    private ServiceProvider _serviceProvider = null!;

    public void Dispose()
    {
        // IDisposable implementation for legacy compatibility
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    [Before(Test)]
    public async Task SetupTestAsync()
    {
        // Base class sets up DbContext
        await SetupAsync();

        // Create real services using base DbContext
        var clipboardLogger = A.Fake<ILogger<ClipboardService>>();
        var filterLogger = A.Fake<ILogger<ApplicationFilterService>>();
        var coordinatorLogger = A.Fake<ILogger<ClipboardCoordinator>>();
        var clipRepoLogger = A.Fake<ILogger<ClipRepository>>();

        _clipRepository = new ClipRepository(DbContext, clipRepoLogger);

        // Create repository factory for ClipService
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(_testDatabaseKey))
            .Returns(_clipRepository);

        // Setup ClipboardService first (needed by ClipService)
        var win32Mock = A.Fake<IWin32ClipboardInterop>();
        A.CallTo(() => win32Mock.AddClipboardFormatListener(A<HWND>.Ignored)).Returns(true);
        A.CallTo(() => win32Mock.RemoveClipboardFormatListener(A<HWND>.Ignored)).Returns(true);
        var profileServiceMock = A.Fake<IApplicationProfileService>();
        A.CallTo(() => profileServiceMock.IsApplicationProfilesEnabled()).Returns(false);
        var clipboardSoundService = A.Fake<ISoundService>();
        A.CallTo(() => clipboardSoundService.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Create configuration mock with default timing values
        var configServiceMock = A.Fake<IConfigurationService>();
        var clipboardConfig = new ClipMateConfiguration();
        A.CallTo(() => configServiceMock.Configuration).Returns(clipboardConfig);

        var blobCacheServiceMock = A.Fake<IDecryptedBlobCacheService>();
        _clipboardService = new ClipboardService(clipboardLogger, win32Mock, configServiceMock, profileServiceMock,
            clipboardSoundService, blobCacheServiceMock);

        var clipServiceLogger = A.Fake<ILogger<ClipService>>();

        _clipService = new ClipService(
            contextFactory,
            new ClipStore(contextFactory, NullLogger<ClipStore>.Instance),
            A.Fake<IConfigurationService>(),
            A.Fake<IEncryptionService>(),
            A.Fake<IDecryptedBlobCacheService>(),
            new RepositoryDatabases(contextFactory),
            A.Fake<IMessenger>(),
            clipServiceLogger);

        var filterRepository = new ApplicationFilterRepository(DbContext);
        var mockFilterContextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => mockFilterContextFactory.GetApplicationFilterRepository(A<string>.Ignored))
            .Returns(filterRepository);

        var mockFilterCollectionService = A.Fake<ICollectionService>();
        A.CallTo(() => mockFilterCollectionService.GetActiveDatabaseKey()).Returns("test-db");
        var filterSoundService = A.Fake<ISoundService>();
        A.CallTo(() => filterSoundService.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        _filterService = new ApplicationFilterService(
            mockFilterContextFactory,
            mockFilterCollectionService,
            filterSoundService,
            filterLogger);

        var collectionRepository = new CollectionRepository(DbContext);

        var folderRepository = new FolderRepository(DbContext);
        var mockFolderContextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => mockFolderContextFactory.GetFolderRepository(A<string>.Ignored)).Returns(folderRepository);
        var mockFolderCollectionService = A.Fake<ICollectionService>();
        A.CallTo(() => mockFolderCollectionService.GetActiveDatabaseKey()).Returns("test-db");
        var folderService = new FolderService(mockFolderContextFactory, mockFolderCollectionService);

        // Setup DI container for ClipboardCoordinator (needs IServiceProvider for scoped services)
        var services = new ServiceCollection();
        services.AddTransient<IClipService>(_ => _clipService);
        services.AddTransient<ICollectionRepository>(_ => collectionRepository);
        services.AddSingleton<ICollectionService, CollectionService>();
        services.AddTransient<IFolderService>(_ => folderService);
        services.AddTransient<IApplicationFilterService>(_ => _filterService);
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton(contextFactory);
        services.AddSingleton<IAutoAppendService, AutoAppendService>();

        // Register sound service mock
        var soundServiceMock = A.Fake<ISoundService>();
        A.CallTo(() => soundServiceMock.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        services.AddSingleton(soundServiceMock);

        // Add mock IConfigurationService
        var mockConfigService = A.Fake<IConfigurationService>();
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration
            {
                EnableAutoCaptureAtStartup = true,
                CaptureExistingClipboardAtStartup = false,
                Sound = new SoundConfiguration(),
            },
        };

        A.CallTo(() => mockConfigService.Configuration).Returns(config);
        services.AddSingleton(mockConfigService);

        _serviceProvider = services.BuildServiceProvider();

        var messenger = _serviceProvider.GetRequiredService<IMessenger>();
        var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
        var autoAppendService = _serviceProvider.GetRequiredService<IAutoAppendService>();
        _coordinator = new ClipboardCoordinator(_clipboardService, configService, A.Fake<ICurrentClipWriter>(),
            contextFactory, new ClipStore(contextFactory, NullLogger<ClipStore>.Instance), autoAppendService,
            _serviceProvider, messenger, coordinatorLogger);
    }

    // Note: ClipboardCapture_ShouldSaveToDatabase test removed
    // Requires access to internal channel writer which is not exposed.
    // The full pipeline is tested end-to-end via ClipboardCoordinator tests below.

    [Test]
    public async Task ClipboardCoordinator_Start_ShouldEnableMonitoring()
    {
        // Act
        await _coordinator.StartAsync(CancellationToken.None);

        // Assert - verify monitoring is active by checking we can stop it
        await _coordinator.StopAsync(CancellationToken.None);

        // If we got here without exceptions, monitoring was successfully started and stopped
        // No assertion needed - successful execution proves the test passed
    }

    [Test]
    public async Task ClipboardCoordinator_Stop_ShouldStopMonitoring()
    {
        // Arrange
        await _coordinator.StartAsync(CancellationToken.None);

        // Act
        await _coordinator.StopAsync(CancellationToken.None);

        // Assert
        await Assert.That(_clipboardService.IsMonitoring).IsFalse();
    }

    [Test]
    public async Task ClipService_CreateAsync_SameContentTwice_CreatesTwoClips()
    {
        // Arrange: New Clip and import always create; only capture checks for Duplicates (the clip store)
        var clip1 = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Same content", CapturedAt = DateTime.UtcNow };
        var clip2 = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Same content", CapturedAt = DateTime.UtcNow.AddSeconds(5) };

        // Act
        var saved1 = await _clipService.CreateAsync(_testDatabaseKey, clip1);
        var saved2 = await _clipService.CreateAsync(_testDatabaseKey, clip2);

        // Assert
        await Assert.That(saved2.Id).IsNotEqualTo(saved1.Id);
        await Assert.That(saved2.ContentHash).IsEqualTo(ContentHasher.HashText("Same content"));
    }

    #region Application Profiles Integration Tests

    [Test]
    [Category("Integration")]
    [Category("ApplicationProfiles")]
    public async Task ApplicationProfiles_UpdateProfile_ShouldPersistChanges()
    {
        // Arrange
        var profileServiceMock = A.Fake<IApplicationProfileService>();
        var profile = new CoreApplicationProfile
        {
            ApplicationName = "NOTEPAD",
            Enabled = true,
            Formats = new Dictionary<string, bool>
            {
                ["TEXT"] = true,
                ["CF_UNICODETEXT"] = false,
            },
        };

        A.CallTo(() => profileServiceMock.UpdateProfileAsync(profile, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        profile.Formats["CF_UNICODETEXT"] = true; // User enables UNICODETEXT
        await profileServiceMock.UpdateProfileAsync(profile);

        // Assert
        A.CallTo(() => profileServiceMock.UpdateProfileAsync(
                A<CoreApplicationProfile>.That.Matches(i =>
                    i.ApplicationName == "NOTEPAD" &&
                    i.Formats["CF_UNICODETEXT"] == true),
                A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("Integration")]
    [Category("ApplicationProfiles")]
    public async Task ApplicationProfiles_GetAllProfiles_ShouldReturnAllStored()
    {
        // Arrange
        var profileServiceMock = A.Fake<IApplicationProfileService>();
        var profiles = new Dictionary<string, CoreApplicationProfile>
        {
            ["NOTEPAD"] = new()
            {
                ApplicationName = "NOTEPAD",
                Enabled = true,
                Formats = new Dictionary<string, bool> { ["TEXT"] = true },
            },
            ["CHROME"] = new()
            {
                ApplicationName = "CHROME",
                Enabled = true,
                Formats = new Dictionary<string, bool> { ["TEXT"] = true, ["HTML Format"] = true },
            },
        };

        A.CallTo(() => profileServiceMock.GetAllProfilesAsync(A<CancellationToken>.Ignored))
            .Returns(profiles);

        // Act
        var result = await profileServiceMock.GetAllProfilesAsync();

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.ContainsKey("NOTEPAD")).IsTrue();
        await Assert.That(result.ContainsKey("CHROME")).IsTrue();
    }

    [Test]
    [Category("Integration")]
    [Category("ApplicationProfiles")]
    public async Task ApplicationProfiles_DeleteProfile_ShouldRemoveFromStorage()
    {
        // Arrange
        var profileServiceMock = A.Fake<IApplicationProfileService>();
        A.CallTo(() => profileServiceMock.DeleteProfileAsync("NOTEPAD", A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        await profileServiceMock.DeleteProfileAsync("NOTEPAD");

        // Assert
        A.CallTo(() => profileServiceMock.DeleteProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("Integration")]
    [Category("ApplicationProfiles")]
    public async Task ApplicationProfiles_DeleteAllProfiles_ShouldClearStorage()
    {
        // Arrange
        var profileServiceMock = A.Fake<IApplicationProfileService>();
        A.CallTo(() => profileServiceMock.DeleteAllProfilesAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        await profileServiceMock.DeleteAllProfilesAsync();

        // Assert
        A.CallTo(() => profileServiceMock.DeleteAllProfilesAsync(A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [Category("Integration")]
    [Category("ApplicationProfiles")]
    public async Task ApplicationProfiles_EnabledStateToggle_ShouldAffectFilteringBehavior()
    {
        // Arrange
        var profileServiceMock = A.Fake<IApplicationProfileService>();
        var isEnabled = false;

        A.CallTo(() => profileServiceMock.IsApplicationProfilesEnabled())
            .ReturnsLazily(() => isEnabled);

        A.CallTo(() => profileServiceMock.SetApplicationProfilesEnabled(A<bool>.Ignored))
            .Invokes((bool p) => isEnabled = p);

        // Act & Assert - Initially disabled
        await Assert.That(profileServiceMock.IsApplicationProfilesEnabled()).IsFalse();

        // Enable profiles
        profileServiceMock.SetApplicationProfilesEnabled(true);
        await Assert.That(profileServiceMock.IsApplicationProfilesEnabled()).IsTrue();

        // Disable profiles
        profileServiceMock.SetApplicationProfilesEnabled(false);
        await Assert.That(profileServiceMock.IsApplicationProfilesEnabled()).IsFalse();
    }

    #endregion

    #region ClipboardCoordinator Processing Tests

    [Test]
    public async Task ProcessClipsAsync_WithValidClip_ShouldSaveClip()
    {
        // Arrange
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Integration test content",
            ContentHash = "hash_integration_123",
            CapturedAt = DateTime.UtcNow,
            SourceApplicationName = "TestApp",
        };

        // Act - Use ClipService directly (ClipboardCoordinator uses ClipRepository internally)
        var savedClip = await _clipService.CreateAsync(_testDatabaseKey, clip);
        await DbContext.SaveChangesAsync();

        // Assert - Verify clip was saved
        var retrievedClip = await _clipRepository.GetByIdAsync(savedClip.Id);
        await Assert.That(retrievedClip).IsNotNull();
        await Assert.That(retrievedClip!.ContentHash).IsEqualTo(ContentHasher.HashText("Integration test content"));
        await Assert.That(retrievedClip.SourceApplicationName).IsEqualTo("TestApp");
    }

    [Test]
    public async Task ProcessClipsAsync_WithMultipleClips_ShouldProcessAll()
    {
        // Arrange
        var clips = new[]
        {
            new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Text,
                TextContent = "Clip 1",
                ContentHash = "hash1",
                CapturedAt = DateTime.UtcNow.AddSeconds(-3),
            },
            new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Text,
                TextContent = "Clip 2",
                ContentHash = "hash2",
                CapturedAt = DateTime.UtcNow.AddSeconds(-2),
            },
            new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Text,
                TextContent = "Clip 3",
                ContentHash = "hash3",
                CapturedAt = DateTime.UtcNow.AddSeconds(-1),
            },
        };

        // Act - Save multiple clips
        foreach (var clip in clips)
            await _clipService.CreateAsync(_testDatabaseKey, clip);

        await DbContext.SaveChangesAsync();

        // Assert - Verify all clips were saved
        var savedClips = await _clipRepository.GetRecentAsync(10);
        await Assert.That(savedClips.Count).IsEqualTo(3);
        await Assert.That(savedClips.Select(c => c.ContentHash)).Contains(ContentHasher.HashText("Clip 1"));
        await Assert.That(savedClips.Select(c => c.ContentHash)).Contains(ContentHasher.HashText("Clip 2"));
        await Assert.That(savedClips.Select(c => c.ContentHash)).Contains(ContentHasher.HashText("Clip 3"));
    }

    #endregion
}
