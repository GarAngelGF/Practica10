
using Microsoft.Data.SqlClient;

namespace Practica9.Data
{
    public sealed class DbSingleton
    {
        private static DbSingleton? _instancia;
        private static readonly object _lock = new();
        private readonly string _connectionString;

        private DbSingleton(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static void Inicializar(string connectionString)
        {
            if (_instancia == null)
            {
                lock (_lock)
                {
                    _instancia ??= new DbSingleton(connectionString);
                }
            }
        }

        public static DbSingleton Instancia
        {
            get
            {
                if (_instancia == null)
                    throw new InvalidOperationException("El Singleton de Base de Datos no ha sido inicializado en Program.cs");
                return _instancia;
            }
        }

        public SqlConnection CrearConexion()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
