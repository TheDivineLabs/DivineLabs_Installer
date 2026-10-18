namespace DivineLabs.Installer.Core;

/// <summary>An install/update request. TargetDirectory must be an application-specific directory.</summary>
/// <param name="PackagePath">A local, prebuilt .dlpack package.</param>
/// <param name="TargetDirectory">Empty directory or a matching existing installation.</param>
/// <param name="RuntimeDirectory">Optional standalone maintenance runtime; never the launcher's own directory.</param>
/// <param name="Options">OS integration and shortcut choices. Defaults enable integrations.</param>
public sealed record InstallRequest(string PackagePath, string TargetDirectory,
    string? RuntimeDirectory = null, InstallOptions? Options = null);

/// <summary>Removes owned application files. User data is retained unless explicitly requested.</summary>
public sealed record UninstallRequest(string TargetDirectory, bool RemoveData = false);

/// <summary>UI-independent, asynchronous installation API. All file transactions execute off the caller thread.</summary>
/// <remarks>
/// InstallAsync also updates matching installations. Cancel before the commit point to roll back;
/// cancellation after commit does not undo completed work. Progress is observational and may be delivered
/// through the host's SynchronizationContext. Integration failures are returned as warnings after commit.
/// No method opens UI, launches the application or downloads packages.
/// </remarks>
public interface IInstallerEngine {
    Task<OperationResult> InstallAsync(InstallRequest request, IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task<OperationResult> UninstallAsync(UninstallRequest request, IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task RecoverAsync(string targetDirectory, CancellationToken cancellationToken = default);
    /// <summary>Read a validated receipt, or null when absent. Does not recover transactions or take a writer lock.</summary>
    Receipt? GetInstallation(string targetDirectory);
}

/// <summary>Optional installation discovery, separate from file transactions and OS integration.</summary>
public interface IInstallationLocator {
    IEnumerable<(string Folder, string Version)> FindInstallations(ProductConfig product);
}
