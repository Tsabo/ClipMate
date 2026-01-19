using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using ClipMate.Core.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Timer = System.Timers.Timer;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the encryption key dialog.
/// Manages key retention with timeout and secure memory handling.
/// </summary>
public partial class EncryptionKeyDialogViewModel : ObservableObject, IDisposable
{
    // Per-clip key tracking
    private static readonly Dictionary<Guid, string> _clipKeyHashes = new();
    private static readonly Dictionary<string, SecureString> _keysByHash = new();
    private static readonly Dictionary<string, DateTime?> _expirationsByHash = new();
    private static readonly Dictionary<string, Timer> _timersByHash = new();
    private static IMessenger? _messenger;
    private static readonly Lock _lock = new();

    private SecureString? _currentPassphrase;
    private bool _disposed;

    [ObservableProperty]
    private bool _hideKey = true;

    [ObservableProperty]
    private bool _isExtendedMode;

    [ObservableProperty]
    private string _plainTextPassword = string.Empty;

    [ObservableProperty]
    private bool _rememberForMinutes = true;

    [ObservableProperty]
    private bool _rememberUntilShutdown;

    [ObservableProperty]
    private int _retentionMinutes = 1;

    public EncryptionKeyDialogViewModel(IMessenger messenger)
    {
        // Store messenger for timer callback (static method can't access instance)
        _messenger = messenger;
    }

    /// <summary>
    /// Gets whether a key is currently cached in memory.
    /// </summary>
    public static bool HasCachedKey
    {
        get
        {
            lock (_lock)
            {
                // Check if any non-expired keys exist
                var now = DateTime.Now;
                return _keysByHash.Any(p =>
                {
                    var hash = p.Key;
                    var expiration = _expirationsByHash.GetValueOrDefault(hash);
                    return expiration == null || now < expiration;
                });
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _currentPassphrase?.Dispose();
        _currentPassphrase = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Initializes the dialog for encryption (simple mode).
    /// </summary>
    public void InitializeForEncryption() => IsExtendedMode = false;

    /// <summary>
    /// Initializes the dialog for decryption (extended mode with remember options).
    /// </summary>
    public void InitializeForDecryption()
    {
        IsExtendedMode = true;

        // Try to use any cached key if available
        lock (_lock)
        {
            if (HasCachedKey)
            {
                // Get first non-expired key
                var now = DateTime.Now;
                var validKey = _keysByHash.FirstOrDefault(kvp =>
                {
                    var expiration = _expirationsByHash.GetValueOrDefault(kvp.Key);
                    return expiration == null || now < expiration;
                });

                if (validKey.Value != null)
                    _currentPassphrase = validKey.Value.Copy();
            }
        }
    }

    /// <summary>
    /// Sets the passphrase from the PasswordBox.
    /// </summary>
    public void SetPassphrase(SecureString securePassword)
    {
        _currentPassphrase?.Dispose();
        _currentPassphrase = securePassword.Copy();
    }

    /// <summary>
    /// Gets the current passphrase as a SecureString.
    /// </summary>
    public SecureString? GetPassphrase() => _currentPassphrase?.Copy();

    /// <summary>
    /// Caches the key in memory with the configured retention settings.
    /// Associates the key with the specified clip ID.
    /// </summary>
    public void CacheKey(Guid clipId)
    {
        if (_currentPassphrase == null || _currentPassphrase.Length == 0)
            return;

        lock (_lock)
        {
            // Compute hash of passphrase for deduplication
            var keyHash = ComputeKeyHash(_currentPassphrase);

            // Track which key this clip uses
            _clipKeyHashes[clipId] = keyHash;

            // If we already have this key cached, just update the clip mapping
            if (_keysByHash.ContainsKey(keyHash))
            {
                // Extend expiration if needed
                if (RememberForMinutes)
                {
                    _expirationsByHash[keyHash] = DateTime.Now.AddMinutes(RetentionMinutes);

                    // Reset timer
                    if (!_timersByHash.TryGetValue(keyHash, out var existingTimer))
                        return;

                    existingTimer.Stop();
                    existingTimer.Interval = RetentionMinutes * 60 * 1000;
                    existingTimer.Start();
                }
                else // RememberUntilShutdown or neither (treat as RememberUntilShutdown)
                    _expirationsByHash[keyHash] = null;

                return;
            }

            // Cache new key
            _keysByHash[keyHash] = _currentPassphrase.Copy();

            if (RememberForMinutes)
            {
                // Set expiration time
                _expirationsByHash[keyHash] = DateTime.Now.AddMinutes(RetentionMinutes);

                // Start timer
                var timer = new Timer(RetentionMinutes * 60 * 1000);
                var capturedHash = keyHash; // Capture for closure
                timer.Elapsed += (_, _) => OnTimerElapsed(capturedHash);
                timer.AutoReset = false;
                timer.Start();
                _timersByHash[keyHash] = timer;
            }
            else // RememberUntilShutdown or neither (treat as RememberUntilShutdown)
            {
                // No expiration - clear on app shutdown only
                _expirationsByHash[keyHash] = null;
            }
        }
    }

    /// <summary>
    /// Computes a hash of the passphrase for key deduplication.
    /// </summary>
    private static string ComputeKeyHash(SecureString passphrase)
    {
        // Convert SecureString to string temporarily for hashing
        var unmanagedString = IntPtr.Zero;
        try
        {
            unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(passphrase);
            var passphraseString = Marshal.PtrToStringUni(unmanagedString);
            if (passphraseString == null)
                return string.Empty;

            // Compute SHA256 hash
            var bytes = Encoding.UTF8.GetBytes(passphraseString);
            var hash = SHA256.HashData(bytes);
            Array.Clear(bytes, 0, bytes.Length);
            return Convert.ToBase64String(hash);
        }
        finally
        {
            if (unmanagedString != IntPtr.Zero)
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
        }
    }

    private static void OnTimerElapsed(string keyHash)
    {
        lock (_lock)
        {
            // Remove this key
            ForgetKeyByHash(keyHash);

            // Notify that encryption key has expired so temporarily decrypted clips can be re-locked
            _messenger?.Send(new EncryptionKeyExpiredEvent());
        }
    }

    /// <summary>
    /// Forgets keys for specific clips.
    /// </summary>
    public static void ForgetKeysForClips(IEnumerable<Guid> clipIds)
    {
        lock (_lock)
        {
            var keyHashesToCheck = new HashSet<string>();

            foreach (var item in clipIds)
            {
                if (_clipKeyHashes.Remove(item, out var keyHash))
                {
                    keyHashesToCheck.Add(keyHash);
                }
            }

            // For each key hash, check if any clips still use it
            foreach (var item in keyHashesToCheck)
            {
                if (!_clipKeyHashes.ContainsValue(item))
                {
                    // No more clips use this key - remove it
                    ForgetKeyByHash(item);
                }
            }
        }
    }

    /// <summary>
    /// Forgets a specific key by its hash.
    /// </summary>
    private static void ForgetKeyByHash(string keyHash)
    {
        if (_keysByHash.TryGetValue(keyHash, out var key))
        {
            key.Dispose();
            _keysByHash.Remove(keyHash);
        }

        _expirationsByHash.Remove(keyHash);

        if (!_timersByHash.TryGetValue(keyHash, out var timer))
            return;

        timer.Stop();
        timer.Dispose();
        _timersByHash.Remove(keyHash);
    }

    /// <summary>
    /// Forgets (clears) all cached encryption keys from memory.
    /// </summary>
    public static void ForgetKey()
    {
        lock (_lock)
        {
            _clipKeyHashes.Clear();

            foreach (var item in _keysByHash.Values)
                item.Dispose();

            _keysByHash.Clear();

            _expirationsByHash.Clear();

            foreach (var item in _timersByHash.Values)
            {
                item.Stop();
                item.Dispose();
            }

            _timersByHash.Clear();
        }
    }

    [RelayCommand]
    private void Ok()
    {
        // Note: CacheKey will be called by the coordinator after successful decryption
        // with the actual clip ID, since we don't know it here
    }

    [RelayCommand]
    private void Help()
    {
        // TODO: Open help documentation for encryption
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://jeremy.browns.info/ClipMate/docs/using-encryption",
            UseShellExecute = true,
        });
    }
}
