using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PrismMonitor.App.Interop;
using PrismMonitor.Core.Monitoring;
using PrismMonitor.Core.Processes;

namespace PrismMonitor.App.Processes;

internal sealed class Win32ProcessEnricher : IProcessEnricher
{
    private const int MaxPath = 32_767;

    public Task<ProcessEnrichmentInfo> EnrichAsync(
        ProcessSnapshotInfo snapshot,
        ProcessEnrichmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Enrich(snapshot, request, cancellationToken), cancellationToken);
    }

    private static ProcessEnrichmentInfo Enrich(
        ProcessSnapshotInfo snapshot,
        ProcessEnrichmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using Process process = Process.GetProcessById(snapshot.ProcessId);
            DateTimeOffset? currentStartTime = TryReadProcessStartTime(process);
            if (snapshot.CreationTime is not null
                && currentStartTime is not null
                && snapshot.CreationTime != currentStartTime)
            {
                return Limited(request, "Process identity changed before enrichment.");
            }

            if (!ProcessInterop.IsWow64Process2(
                    process.SafeHandle,
                    out ushort processMachine,
                    out ushort nativeMachine))
            {
                return Limited(request, new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }

            bool needsPath = processMachine == 0
                || request.IdentityFields.HasFlag(ProcessIdentityFields.ExecutablePath)
                || request.IdentityFields.HasFlag(ProcessIdentityFields.PublisherIdentity);
            string? executablePath = needsPath ? TryReadProcessImagePath(process) : null;
            PeImageArchitecture? imageArchitecture = processMachine == 0
                ? TryReadImageArchitecture(executablePath)
                : null;

            if (processMachine == 0 && imageArchitecture is null)
            {
                return Limited(request, "PE architecture metadata is unavailable.", executablePath);
            }

            ProcessArchitectureInfo architecture = ProcessArchitectureClassifier.Classify(
                processMachine,
                nativeMachine,
                imageArchitecture?.Machine,
                imageArchitecture?.HasArm64XMetadata ?? false);
            if (!architecture.IsCompatibility)
            {
                return ProcessEnrichmentInfo.Native with
                {
                    Level = request.Level,
                    AttemptedFields = request.IdentityFields
                };
            }

            string? packageIdentity = request.IdentityFields.HasFlag(ProcessIdentityFields.PackageIdentity)
                ? TryReadPackageFullName(process)
                : null;
            string? publisherIdentity = request.IdentityFields.HasFlag(ProcessIdentityFields.PublisherIdentity)
                ? TryReadPublisherIdentity(executablePath)
                : null;
            bool missingRequestedPath = request.IdentityFields.HasFlag(ProcessIdentityFields.ExecutablePath)
                && string.IsNullOrWhiteSpace(executablePath);

            return new ProcessEnrichmentInfo(
                ProcessCompatibilityState.Compatible,
                architecture.DisplayName,
                executablePath,
                packageIdentity,
                publisherIdentity,
                IconCacheKey: executablePath,
                request.Level,
                request.IdentityFields,
                HasLimitedDetails: missingRequestedPath,
                LastError: missingRequestedPath ? "Executable path is unavailable." : null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException exception)
        {
            return Limited(request, exception.Message);
        }
        catch (Win32Exception exception)
        {
            return Limited(request, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return Limited(request, exception.Message);
        }
    }

    private static ProcessEnrichmentInfo Limited(
        ProcessEnrichmentRequest request,
        string error,
        string? executablePath = null)
    {
        return ProcessEnrichmentInfo.UnknownLimited(error) with
        {
            ExecutablePath = executablePath,
            IconCacheKey = executablePath,
            Level = request.Level,
            AttemptedFields = request.IdentityFields
        };
    }

    private static PeImageArchitecture? TryReadImageArchitecture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using PEReader reader = new(stream);
            return new PeImageArchitecture(
                (ushort)reader.PEHeaders.CoffHeader.Machine,
                PeImageArchitectureDetector.HasArm64XMetadata(
                    reader.PEHeaders.SectionHeaders.Select(section => section.Name)));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string? TryReadProcessImagePath(Process process)
    {
        char[] buffer = new char[MaxPath];
        uint length = (uint)buffer.Length;
        return ProcessInterop.QueryFullProcessImageName(process.SafeHandle, 0, buffer, ref length)
            && length > 0
                ? new string(buffer, 0, checked((int)length))
                : null;
    }

    private static DateTimeOffset? TryReadProcessStartTime(Process process)
    {
        try
        {
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string? TryReadPackageFullName(Process process)
    {
        uint length = 0;
        int result = ProcessInterop.GetPackageFullName(process.SafeHandle, ref length, []);
        if (result != ProcessInterop.ErrorInsufficientBuffer || length <= 1)
        {
            return null;
        }

        char[] buffer = new char[length];
        result = ProcessInterop.GetPackageFullName(process.SafeHandle, ref length, buffer);
        return result == 0 && length > 1
            ? new string(buffer, 0, checked((int)length - 1))
            : null;
    }

    private static string? TryReadPublisherIdentity(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        try
        {
#pragma warning disable SYSLIB0057
            using X509Certificate certificate = X509Certificate.CreateFromSignedFile(executablePath);
#pragma warning restore SYSLIB0057
            return string.IsNullOrWhiteSpace(certificate.Subject) ? null : certificate.Subject;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private sealed record PeImageArchitecture(ushort Machine, bool HasArm64XMetadata);
}
