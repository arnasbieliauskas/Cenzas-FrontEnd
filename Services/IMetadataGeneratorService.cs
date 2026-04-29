using System.Threading;
using System.Threading.Tasks;

namespace CenzasBackend.Services
{
    public interface IMetadataGeneratorService
    {
        Task RefreshMetadataAsync(CancellationToken ct = default);
    }
}
