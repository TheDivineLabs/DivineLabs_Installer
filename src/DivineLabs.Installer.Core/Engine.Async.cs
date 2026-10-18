namespace DivineLabs.Installer.Core;

public sealed partial class Engine {
    public Task<OperationResult> InstallAsync(InstallRequest request, IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => Install(request.PackagePath, request.TargetDirectory, request.RuntimeDirectory,
            request.Options, progress, cancellationToken), cancellationToken);
    }
    public Task<OperationResult> UninstallAsync(UninstallRequest request, IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => Uninstall(request.TargetDirectory, request.RemoveData, progress, cancellationToken), cancellationToken);
    }
    public Task RecoverAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
        Task.Run(() => RecoverInterrupted(targetDirectory), cancellationToken);
    public Receipt? GetInstallation(string targetDirectory) => ReadReceipt(targetDirectory);
}
