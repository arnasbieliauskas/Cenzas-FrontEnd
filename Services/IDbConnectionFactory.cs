using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace CenzasBackend.Services
{
    public interface IDbConnectionFactory
    {
        Task<IDbConnectionWrapper> OpenConnectionAsync(CancellationToken cancellationToken = default);
    }

    public interface IDbConnectionWrapper : IDisposable
    {
        IDbCommandWrapper CreateCommand();
        string DataSource { get; }
    }

    public interface IDbCommandWrapper : IDisposable
    {
        string CommandText { get; set; }
        int CommandTimeout { get; set; }
        DbParameterCollection Parameters { get; }
        DbParameter CreateParameter();
        Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default);
        Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default);
        Task<DbDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default);
    }

    public class MySqlDbConnectionFactory : IDbConnectionFactory
    {
        private readonly MySqlConnector.MySqlDataSource _dataSource;

        public MySqlDbConnectionFactory(MySqlConnector.MySqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public async Task<IDbConnectionWrapper> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            return new MySqlDbConnectionWrapper(connection);
        }
    }

    public class MySqlDbConnectionWrapper : IDbConnectionWrapper
    {
        private readonly DbConnection _connection;

        public MySqlDbConnectionWrapper(DbConnection connection)
        {
            _connection = connection;
        }

        public IDbCommandWrapper CreateCommand()
        {
            return new MySqlDbCommandWrapper(_connection.CreateCommand());
        }

        public string DataSource => _connection.DataSource;

        public void Dispose()
        {
            _connection.Dispose();
        }
    }

    public class MySqlDbCommandWrapper : IDbCommandWrapper
    {
        private readonly DbCommand _command;

        public MySqlDbCommandWrapper(DbCommand command)
        {
            _command = command;
        }

        public string CommandText { get => _command.CommandText; set => _command.CommandText = value; }
        public int CommandTimeout { get => _command.CommandTimeout; set => _command.CommandTimeout = value; }
        public DbParameterCollection Parameters => _command.Parameters;

        public DbParameter CreateParameter() => _command.CreateParameter();

        public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default)
        {
            return _command.ExecuteNonQueryAsync(cancellationToken);
        }

        public Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default)
        {
            return _command.ExecuteScalarAsync(cancellationToken);
        }

        public Task<DbDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default)
        {
            return _command.ExecuteReaderAsync(cancellationToken);
        }

        public void Dispose()
        {
            _command.Dispose();
        }
    }
}
