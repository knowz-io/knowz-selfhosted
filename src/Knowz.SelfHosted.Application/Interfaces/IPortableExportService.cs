namespace Knowz.SelfHosted.Application.Interfaces;

using Knowz.Core.Portability;

public interface IPortableExportService
{
    /// <summary>
    /// Export all tenant data as a PortableExportPackage.
    /// </summary>
    Task<PortableExportPackage> ExportAsync(CancellationToken ct = default);
}
