using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Webshop.Model.Repos;

public interface IDbConnectionFactory
{
    Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken);
}
