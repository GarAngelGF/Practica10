using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Practica9.Data;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace Practica9.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public HomeController(IWebHostEnvironment env)
        {
            _env = env;
        }

        private int? ObtenerUsuarioSesion() => HttpContext.Session.GetInt32("UsuarioId");

        private static string HashPassword(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        private void AsegurarCatalogosBase(SqlConnection conn)
        {
            string sqlCatalogos = @"
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE Nombre = 'Estudiante')
                    INSERT INTO Roles (Nombre) VALUES ('Estudiante');

                IF NOT EXISTS (SELECT 1 FROM TiposNotificacion)
                BEGIN
                    INSERT INTO TiposNotificacion (Codigo, IconoBootstrap) VALUES 
                    ('success', 'bi-check-circle-fill'), ('warning', 'bi-exclamation-triangle-fill'), 
                    ('danger', 'bi-x-circle-fill'), ('info', 'bi-info-circle-fill');
                END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CategoriasGasto') AND name = 'TipoFlujo')
                BEGIN
                    EXEC('ALTER TABLE CategoriasGasto ADD TipoFlujo VARCHAR(15) NOT NULL DEFAULT ''GASTO''');
                END

                IF NOT EXISTS (SELECT 1 FROM CategoriasGasto)
                BEGIN
                    INSERT INTO CategoriasGasto (Nombre, IconoBootstrap, TipoFlujo) VALUES 
                    ('Transporte', 'bi-bus-front', 'GASTO'), ('Comida', 'bi-egg-fried', 'GASTO'), 
                    ('Escolar', 'bi-book', 'GASTO'), ('Otro', 'bi-lightning', 'GASTO');
                END

                IF NOT EXISTS (SELECT 1 FROM CategoriasGasto WHERE Nombre = 'Aumento de Presupuesto')
                    INSERT INTO CategoriasGasto (Nombre, IconoBootstrap, TipoFlujo) VALUES ('Aumento de Presupuesto', 'bi-plus-circle', 'AUMENTO');

                IF NOT EXISTS (SELECT 1 FROM CategoriasGasto WHERE Nombre = 'Disminución de Presupuesto')
                    INSERT INTO CategoriasGasto (Nombre, IconoBootstrap, TipoFlujo) VALUES ('Disminución de Presupuesto', 'bi-dash-circle', 'DISMINUCION');

                IF OBJECT_ID('PresupuestosDiarios', 'U') IS NULL
                BEGIN
                    CREATE TABLE PresupuestosDiarios (
                        PresupuestoId INT IDENTITY(1,1) PRIMARY KEY,
                        UsuarioId INT NOT NULL,
                        Fecha DATE NOT NULL,
                        PresupuestoInicial DECIMAL(10,2) NOT NULL CHECK (PresupuestoInicial >= 0),
                        Bloqueado BIT NOT NULL DEFAULT 1,
                        BloquearSobregiro BIT NOT NULL DEFAULT 1,
                        SoloContarVerificados BIT NOT NULL DEFAULT 0,
                        CONSTRAINT FK_PresupuestosDiarios_Usuarios FOREIGN KEY (UsuarioId) REFERENCES Usuarios(UsuarioId) ON DELETE CASCADE,
                        CONSTRAINT UQ_Presupuesto_UsuarioFecha UNIQUE (UsuarioId, Fecha),
                        CONSTRAINT CK_Presupuesto_RangoFecha CHECK (Fecha >= '2000-01-01' AND Fecha <= CAST(SYSDATETIME() AS DATE))
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('MovimientosBilletera') AND name = 'FechaMovimiento')
                BEGIN
                    IF OBJECT_ID('MovimientosBilletera', 'U') IS NOT NULL DROP TABLE MovimientosBilletera;
                    CREATE TABLE MovimientosBilletera (
                        MovimientoId INT IDENTITY(1,1) PRIMARY KEY,
                        UsuarioId INT NOT NULL,
                        CategoriaId INT NOT NULL,
                        Concepto NVARCHAR(150) NOT NULL,
                        Monto DECIMAL(10,2) NOT NULL CHECK (Monto > 0),
                        Verificado BIT NOT NULL DEFAULT 1,
                        FechaMovimiento DATE NOT NULL DEFAULT CAST(SYSDATETIME() AS DATE),
                        HoraRegistro TIME(0) NOT NULL DEFAULT CAST(SYSDATETIME() AS TIME(0)),
                        CONSTRAINT FK_Movimientos_Usuarios FOREIGN KEY (UsuarioId) REFERENCES Usuarios(UsuarioId) ON DELETE CASCADE,
                        CONSTRAINT FK_Movimientos_Categorias FOREIGN KEY (CategoriaId) REFERENCES CategoriasGasto(CategoriaId),
                        CONSTRAINT CK_Movimientos_RangoFecha CHECK (FechaMovimiento >= '2000-01-01' AND FechaMovimiento <= CAST(SYSDATETIME() AS DATE))
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM NivelesUrgencia)
                    INSERT INTO NivelesUrgencia (Nombre, PesoPrioridad) VALUES ('Baja', 1), ('Media', 2), ('Alta', 3);

                IF NOT EXISTS (SELECT 1 FROM EstadosKanban)
                    INSERT INTO EstadosKanban (Codigo, NombreMostrar) VALUES ('todo', 'Por Hacer'), ('doing', 'En Proceso'), ('done', 'Terminado');

                IF NOT EXISTS (SELECT 1 FROM DiasSemana)
                    INSERT INTO DiasSemana (DiaId, Nombre) VALUES (1, 'Lunes'), (2, 'Martes'), (3, 'Miércoles'), (4, 'Jueves'), (5, 'Viernes');

                IF NOT EXISTS (SELECT 1 FROM Edificios)
                    INSERT INTO Edificios (Nombre) VALUES ('Ingenieria'), ('Basicas'), ('Ligeros'), ('Pesados'), ('Culturales'), ('Graduados');

                IF (SELECT COUNT(*) FROM ColoresTema) < 10
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#e7f5ff') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Azul Cielo', '#e7f5ff');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#ebfbee') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Verde Menta', '#ebfbee');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#fff9db') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Amarillo Sol', '#fff9db');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#f3f0ff') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Lavanda', '#f3f0ff');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#fff5f5') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Rosa Coral', '#fff5f5');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#fff4e6') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Durazno', '#fff4e6');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#e3fafc') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Turquesa', '#e3fafc');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#f8f0fc') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Orquídea', '#f8f0fc');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#edf2ff') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Índigo Suave', '#edf2ff');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#f4fce3') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Lima Pastel', '#f4fce3');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#f1f3f5') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Gris Perla', '#f1f3f5');
                    IF NOT EXISTS (SELECT 1 FROM ColoresTema WHERE CodigoHex = '#ffe8cc') INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES ('Ámbar', '#ffe8cc');
                END";

            using var cmd = new SqlCommand(sqlCatalogos, conn);
            cmd.ExecuteNonQuery();
        }

        private static bool ValidarRangoFechaPermitido(DateTime fecha, out string mensajeError)
        {
            DateTime minFecha = new(2000, 1, 1);
            DateTime maxFecha = DateTime.Today;

            if (fecha.Date < minFecha)
            {
                mensajeError = "No se permite consultar ni generar presupuestos en fechas anteriores al año 2000.";
                return false;
            }
            if (fecha.Date > maxFecha)
            {
                mensajeError = "Solo se permite generar presupuestos desde el año 2000 hasta la fecha actual (no se permiten fechas futuras).";
                return false;
            }
            mensajeError = "";
            return true;
        }

        // =====================================================================
        // LOGIN Y REGISTRO
        // =====================================================================
        [HttpGet]
        public IActionResult Login()
        {
            if (ObtenerUsuarioSesion() != null)
                return RedirectToAction("Index");

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            AsegurarCatalogosBase(conn);

            return View();
        }

        [HttpGet]
        public IActionResult Registro()
        {
            if (ObtenerUsuarioSesion() != null)
                return RedirectToAction("Index");

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            AsegurarCatalogosBase(conn);

            return View();
        }

        [HttpPost]
        public IActionResult IniciarSesion([FromBody] LoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Correo) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Ingresa tu correo y contraseña.", codigo = "AUTH_EMPTY_FIELDS" });

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

            string hash = HashPassword(dto.Password);
            string sql = @"
                SELECT UsuarioId, Nombre, ApellidoPaterno
                FROM Usuarios
                WHERE Correo = @cor AND (PasswordHash = @hash OR PasswordHash = @raw)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cor", dto.Correo.Trim());
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@raw", dto.Password);

            using var r = cmd.ExecuteReader();
            if (!r.Read())
            {
                return Unauthorized(new { error = "Correo o contraseña incorrectos.", codigo = "AUTH_INVALID_401" });
            }

            int uid = (int)r["UsuarioId"];
            string nombreMostrar = $"{r["Nombre"]} {r["ApellidoPaterno"]}";

            HttpContext.Session.SetInt32("UsuarioId", uid);
            HttpContext.Session.SetString("UsuarioNombre", nombreMostrar);

            return Ok(new { ok = true, redirectUrl = Url.Action("Index", "Home") });
        }

        [HttpPost]
        public IActionResult RegistrarEstudiante([FromBody] RegistroDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.ApellidoPaterno) ||
                string.IsNullOrWhiteSpace(dto.Correo) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new { error = "Todos los campos obligatorios deben completarse.", codigo = "REG_INCOMPLETE_DATA" });
            }

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            AsegurarCatalogosBase(conn);

            using var tx = conn.BeginTransaction();
            try
            {
                using (var cmdCheck = new SqlCommand("SELECT COUNT(1) FROM Usuarios WHERE Correo = @cor", conn, tx))
                {
                    cmdCheck.Parameters.AddWithValue("@cor", dto.Correo.Trim());
                    if ((int)cmdCheck.ExecuteScalar()! > 0)
                    {
                        tx.Rollback();
                        return BadRequest(new { error = "Este correo electrónico ya está registrado.", codigo = "SQL_UQ_EMAIL_EXISTS" });
                    }
                }

                int rolEstudianteId;
                using (var cmdRol = new SqlCommand("SELECT TOP 1 RolId FROM Roles WHERE Nombre = 'Estudiante'", conn, tx))
                {
                    rolEstudianteId = (int)cmdRol.ExecuteScalar()!;
                }

                string sqlInsertUser = @"
                    INSERT INTO Usuarios (Nombre, ApellidoPaterno, ApellidoMaterno, Correo, PasswordHash, RolId)
                    OUTPUT INSERTED.UsuarioId
                    VALUES (@nom, @apPat, @apMat, @cor, @hash, @rolId);";

                int nuevoUsuarioId;
                using (var cmdUser = new SqlCommand(sqlInsertUser, conn, tx))
                {
                    cmdUser.Parameters.AddWithValue("@nom", dto.Nombre.Trim());
                    cmdUser.Parameters.AddWithValue("@apPat", dto.ApellidoPaterno.Trim());
                    cmdUser.Parameters.AddWithValue("@apMat", string.IsNullOrWhiteSpace(dto.ApellidoMaterno) ? DBNull.Value : dto.ApellidoMaterno.Trim());
                    cmdUser.Parameters.AddWithValue("@cor", dto.Correo.Trim());
                    cmdUser.Parameters.AddWithValue("@hash", HashPassword(dto.Password));
                    cmdUser.Parameters.AddWithValue("@rolId", rolEstudianteId);
                    nuevoUsuarioId = (int)cmdUser.ExecuteScalar()!;
                }

                string sqlInitAlumno = @"
                    INSERT INTO TablerosKanban (Nombre, PropietarioId, EsCompartido)
                    VALUES ('Mi Tablero Escolar', @uid, 0);

                    DECLARE @tipoSuccess INT = (SELECT TOP 1 TipoNotificacionId FROM TiposNotificacion WHERE Codigo = 'success');
                    INSERT INTO Notificaciones (UsuarioId, TipoNotificacionId, Titulo, Mensaje, Leida)
                    VALUES (@uid, @tipoSuccess, '¡Bienvenido a GarAngelito Tools Pro!', 'Tu cuenta ha sido creada exitosamente.', 0);";

                using (var cmdInit = new SqlCommand(sqlInitAlumno, conn, tx))
                {
                    cmdInit.Parameters.AddWithValue("@uid", nuevoUsuarioId);
                    cmdInit.ExecuteNonQuery();
                }

                tx.Commit();

                return Ok(new { ok = true, redirectUrl = Url.Action("Login", "Home") });
            }
            catch (SqlException ex)
            {
                tx.Rollback();
                return BadRequest(new { error = ex.Message, codigo = $"SQL_ERR_{ex.Number}" });
            }
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // =====================================================================
        // VISTAS PRINCIPALES
        // =====================================================================
        public IActionResult Index()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return RedirectToAction("Login");

            var hoy = DateTime.Now;
            var diasEnMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
            int diasRestantes = diasEnMes - hoy.Day;

            ViewBag.ProgresoMes = Math.Round((double)hoy.Day / diasEnMes * 100);
            ViewBag.DiasRestantesMes = diasRestantes;
            ViewBag.FechaActual = hoy.ToString("dddd, dd 'de' MMMM yyyy");
            ViewBag.FraseDia = "La disciplina tarde o temprano vencerá a la inteligencia. Organiza tu día y alcanza tus metas.";

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

            using (var cmd = new SqlCommand("SELECT Nombre, ApellidoPaterno, ApellidoMaterno FROM Usuarios WHERE UsuarioId = @uid", conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    ViewBag.AlumnoNombre = $"{r["Nombre"]} {r["ApellidoPaterno"]} {r["ApellidoMaterno"]}".Trim();
                }
            }

            return View();
        }

        public IActionResult Billetera() => ObtenerUsuarioSesion() == null ? RedirectToAction("Login") : View();
        public IActionResult Kanban() => ObtenerUsuarioSesion() == null ? RedirectToAction("Login") : View();
        public IActionResult Horario() => ObtenerUsuarioSesion() == null ? RedirectToAction("Login") : View();

        // =====================================================================
        // API: NOTIFICACIONES
        // =====================================================================
        [HttpGet]
        public IActionResult ObtenerNotificaciones()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Json(new List<object>());

            var lista = new List<object>();
            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            string sql = @"
                SELECT TOP 30 n.NotificacionId, n.Titulo, n.Mensaje, n.Leida, 
                       FORMAT(n.FechaCreacion, 'HH:mm') AS Hora,
                       t.Codigo AS Tipo
                FROM Notificaciones n
                INNER JOIN TiposNotificacion t ON n.TipoNotificacionId = t.TipoNotificacionId
                WHERE n.UsuarioId = @uid
                ORDER BY n.NotificacionId DESC";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new
                {
                    id = (int)r["NotificacionId"],
                    titulo = r["Titulo"].ToString(),
                    mensaje = r["Mensaje"].ToString(),
                    leida = (bool)r["Leida"],
                    hora = r["Hora"].ToString(),
                    tipo = r["Tipo"].ToString()
                });
            }
            return Json(lista);
        }

        [HttpPost]
        public IActionResult CrearNotificacion([FromBody] NotifDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            string sql = @"
                DECLARE @tipoId INT = (SELECT TOP 1 TipoNotificacionId FROM TiposNotificacion WHERE Codigo = @tipo);
                IF @tipoId IS NULL SET @tipoId = 1;
                INSERT INTO Notificaciones (UsuarioId, TipoNotificacionId, Titulo, Mensaje, Leida)
                VALUES (@uid, @tipoId, @tit, @msg, 0);";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.Parameters.AddWithValue("@tipo", dto.Tipo ?? "info");
            cmd.Parameters.AddWithValue("@tit", dto.Titulo ?? "Notificación");
            cmd.Parameters.AddWithValue("@msg", dto.Mensaje ?? "");
            cmd.ExecuteNonQuery();
            return Ok();
        }

        [HttpPost]
        public IActionResult ToggleNotificacion(int id)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE Notificaciones SET Leida = ~Leida WHERE NotificacionId = @id AND UsuarioId = @uid", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.ExecuteNonQuery();
            return Ok();
        }

        [HttpPost]
        public IActionResult MarcarTodasLeidas()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE Notificaciones SET Leida = 1 WHERE UsuarioId = @uid", conn);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.ExecuteNonQuery();
            return Ok();
        }

        [HttpPost]
        public IActionResult LimpiarNotificaciones()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM Notificaciones WHERE UsuarioId = @uid", conn);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.ExecuteNonQuery();
            return Ok();
        }

        // =====================================================================
        // API: BILLETERA DIARIA POR FECHA + AJUSTES DE PRESUPUESTO + EDICIÓN
        // =====================================================================
        [HttpGet]
        public IActionResult ObtenerBilletera(string? fecha)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            DateTime fechaConsulta = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(fecha) && DateTime.TryParse(fecha, out DateTime parsed))
            {
                fechaConsulta = parsed.Date;
            }

            if (!ValidarRangoFechaPermitido(fechaConsulta, out string msgError))
            {
                return BadRequest(new { error = msgError, codigo = "ERR_DATE_OUT_OF_BOUNDS" });
            }

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            AsegurarCatalogosBase(conn);

            decimal presupuestoInicial = 0;
            bool presupuestoBloqueado = false;
            bool bloquearSobregiro = true;
            bool soloContarVerificados = false;

            string sqlPres = @"
                SELECT PresupuestoInicial, Bloqueado, BloquearSobregiro, SoloContarVerificados
                FROM PresupuestosDiarios
                WHERE UsuarioId = @uid AND Fecha = @fec";

            using (var cmd = new SqlCommand(sqlPres, conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                cmd.Parameters.AddWithValue("@fec", fechaConsulta);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    presupuestoInicial = (decimal)r["PresupuestoInicial"];
                    presupuestoBloqueado = (bool)r["Bloqueado"];
                    bloquearSobregiro = (bool)r["BloquearSobregiro"];
                    soloContarVerificados = (bool)r["SoloContarVerificados"];
                }
            }

            var categorias = new List<object>();
            using (var cmd = new SqlCommand("SELECT CategoriaId, Nombre, TipoFlujo FROM CategoriasGasto ORDER BY CategoriaId", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    categorias.Add(new
                    {
                        id = (int)r["CategoriaId"],
                        nombre = r["Nombre"].ToString(),
                        tipoFlujo = r["TipoFlujo"].ToString()
                    });
                }
            }

            var gastos = new List<object>();
            string sqlGastos = @"
                SELECT m.MovimientoId, m.CategoriaId, m.Concepto, m.Monto, m.Verificado, 
                       LEFT(CAST(m.HoraRegistro AS VARCHAR), 5) AS Hora, 
                       c.Nombre AS Categoria, c.TipoFlujo
                FROM MovimientosBilletera m
                INNER JOIN CategoriasGasto c ON m.CategoriaId = c.CategoriaId
                WHERE m.UsuarioId = @uid AND m.FechaMovimiento = @fec
                ORDER BY m.MovimientoId DESC";

            using (var cmd = new SqlCommand(sqlGastos, conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                cmd.Parameters.AddWithValue("@fec", fechaConsulta);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    gastos.Add(new
                    {
                        id = (int)r["MovimientoId"],
                        categoriaId = (int)r["CategoriaId"],
                        concepto = r["Concepto"].ToString(),
                        monto = (decimal)r["Monto"],
                        verificado = (bool)r["Verificado"],
                        hora = r["Hora"].ToString(),
                        categoria = r["Categoria"].ToString(),
                        tipoFlujo = r["TipoFlujo"].ToString()
                    });
                }
            }

            return Json(new
            {
                fecha = fechaConsulta.ToString("yyyy-MM-dd"),
                presupuestoInicial,
                presupuestoBloqueado,
                bloquearSobregiro,
                soloContarVerificados,
                categorias,
                gastos
            });
        }

        [HttpPost]
        public IActionResult GuardarPresupuestoInicial([FromBody] GuardarPresupuestoDiaDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            if (!DateTime.TryParse(dto.Fecha, out DateTime fechaParsed))
                return BadRequest(new { error = "Fecha inválida.", codigo = "ERR_INVALID_DATE" });

            if (!ValidarRangoFechaPermitido(fechaParsed, out string msgError))
                return BadRequest(new { error = msgError, codigo = "ERR_DATE_OUT_OF_BOUNDS" });

            if (dto.PresupuestoInicial <= 0)
                return BadRequest(new { error = "El presupuesto inicial debe ser mayor a $0.00.", codigo = "ERR_BUDGET_ZERO" });

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

            // Verificar si ya estaba bloqueado para esa fecha
            using (var cmdCheck = new SqlCommand("SELECT Bloqueado FROM PresupuestosDiarios WHERE UsuarioId = @uid AND Fecha = @fec", conn))
            {
                cmdCheck.Parameters.AddWithValue("@uid", uid.Value);
                cmdCheck.Parameters.AddWithValue("@fec", fechaParsed.Date);
                var res = cmdCheck.ExecuteScalar();
                if (res != null && (bool)res)
                {
                    return BadRequest(new { error = "El presupuesto inicial de esta fecha ya fue guardado y bloqueado. Usa el apartado de Agregar o Disminuir presupuesto.", codigo = "ERR_BUDGET_LOCKED" });
                }
            }

            string sql = @"
                IF EXISTS (SELECT 1 FROM PresupuestosDiarios WHERE UsuarioId = @uid AND Fecha = @fec)
                    UPDATE PresupuestosDiarios SET PresupuestoInicial = @pres, Bloqueado = 1 WHERE UsuarioId = @uid AND Fecha = @fec;
                ELSE
                    INSERT INTO PresupuestosDiarios (UsuarioId, Fecha, PresupuestoInicial, Bloqueado, BloquearSobregiro, SoloContarVerificados)
                    VALUES (@uid, @fec, @pres, 1, 1, 0);";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.Parameters.AddWithValue("@fec", fechaParsed.Date);
            cmd.Parameters.AddWithValue("@pres", dto.PresupuestoInicial);
            cmd.ExecuteNonQuery();

            return Ok(new { ok = true });
        }

        [HttpPost]
        public IActionResult ActualizarSwitchesBilletera([FromBody] SwitchesBilleteraDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            if (!DateTime.TryParse(dto.Fecha, out DateTime fechaParsed))
                return BadRequest(new { error = "Fecha inválida.", codigo = "ERR_INVALID_DATE" });

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            string sql = @"
                UPDATE PresupuestosDiarios 
                SET BloquearSobregiro = @bloq, SoloContarVerificados = @solo
                WHERE UsuarioId = @uid AND Fecha = @fec";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@bloq", dto.BloquearSobregiro);
            cmd.Parameters.AddWithValue("@solo", dto.SoloContarVerificados);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.Parameters.AddWithValue("@fec", fechaParsed.Date);
            cmd.ExecuteNonQuery();
            return Ok(new { ok = true });
        }

        [HttpPost]
        public IActionResult AgregarMovimientoBilletera([FromBody] MovimientoDiaDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            if (!DateTime.TryParse(dto.Fecha, out DateTime fechaParsed))
                return BadRequest(new { error = "Fecha inválida.", codigo = "ERR_INVALID_DATE" });

            if (!ValidarRangoFechaPermitido(fechaParsed, out string msgError))
                return BadRequest(new { error = msgError, codigo = "ERR_DATE_OUT_OF_BOUNDS" });

            if (string.IsNullOrWhiteSpace(dto.Concepto))
                return BadRequest(new { error = "El nombre/concepto del movimiento es obligatorio.", codigo = "ERR_EMPTY_CONCEPT" });

            if (dto.Monto <= 0)
                return BadRequest(new { error = "El monto debe ser mayor a $0.00.", codigo = "ERR_INVALID_AMOUNT" });

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                decimal? presInicial = null;
                bool bloquear = true;

                using (var cmdPres = new SqlCommand("SELECT PresupuestoInicial, BloquearSobregiro FROM PresupuestosDiarios WHERE UsuarioId = @uid AND Fecha = @fec", conn, tx))
                {
                    cmdPres.Parameters.AddWithValue("@uid", uid.Value);
                    cmdPres.Parameters.AddWithValue("@fec", fechaParsed.Date);
                    using var r = cmdPres.ExecuteReader();
                    if (r.Read())
                    {
                        presInicial = (decimal)r["PresupuestoInicial"];
                        bloquear = (bool)r["BloquearSobregiro"];
                    }
                }

                if (presInicial == null)
                {
                    tx.Rollback();
                    return BadRequest(new { error = "Primero debes guardar el presupuesto inicial de esta fecha.", codigo = "ERR_NO_INITIAL_BUDGET" });
                }

                string tipoFlujo = "GASTO";
                using (var cmdCat = new SqlCommand("SELECT TipoFlujo FROM CategoriasGasto WHERE CategoriaId = @cid", conn, tx))
                {
                    cmdCat.Parameters.AddWithValue("@cid", dto.CategoriaId);
                    var res = cmdCat.ExecuteScalar();
                    if (res != null) tipoFlujo = res.ToString()!;
                }

                decimal aumentos = 0, disminuciones = 0, gastos = 0;
                string sqlSum = @"
                    SELECT 
                        ISNULL(SUM(CASE WHEN c.TipoFlujo = 'AUMENTO' THEN m.Monto ELSE 0 END), 0) AS Aumentos,
                        ISNULL(SUM(CASE WHEN c.TipoFlujo = 'DISMINUCION' THEN m.Monto ELSE 0 END), 0) AS Disminuciones,
                        ISNULL(SUM(CASE WHEN c.TipoFlujo = 'GASTO' THEN m.Monto ELSE 0 END), 0) AS Gastos
                    FROM MovimientosBilletera m
                    INNER JOIN CategoriasGasto c ON m.CategoriaId = c.CategoriaId
                    WHERE m.UsuarioId = @uid AND m.FechaMovimiento = @fec";

                using (var cmdSum = new SqlCommand(sqlSum, conn, tx))
                {
                    cmdSum.Parameters.AddWithValue("@uid", uid.Value);
                    cmdSum.Parameters.AddWithValue("@fec", fechaParsed.Date);
                    using var r = cmdSum.ExecuteReader();
                    if (r.Read())
                    {
                        aumentos = (decimal)r["Aumentos"];
                        disminuciones = (decimal)r["Disminuciones"];
                        gastos = (decimal)r["Gastos"];
                    }
                }

                decimal saldoDisponible = (presInicial.Value + aumentos - disminuciones) - gastos;

                if (bloquear && (tipoFlujo == "GASTO" || tipoFlujo == "DISMINUCION") && dto.Monto > saldoDisponible)
                {
                    tx.Rollback();
                    return BadRequest(new
                    {
                        error = $"Fondos insuficientes. Intentas descontar ${dto.Monto:F2} pero tu saldo disponible del día es ${saldoDisponible:F2}.",
                        codigo = "ERR_OVERDRAFT_SQL"
                    });
                }

                string sqlInsert = @"
                    INSERT INTO MovimientosBilletera (UsuarioId, CategoriaId, Concepto, Monto, Verificado, FechaMovimiento)
                    VALUES (@uid, @cid, @con, @mon, @ver, @fec)";

                using (var cmdIns = new SqlCommand(sqlInsert, conn, tx))
                {
                    cmdIns.Parameters.AddWithValue("@uid", uid.Value);
                    cmdIns.Parameters.AddWithValue("@cid", dto.CategoriaId);
                    cmdIns.Parameters.AddWithValue("@con", dto.Concepto.Trim());
                    cmdIns.Parameters.AddWithValue("@mon", dto.Monto);
                    cmdIns.Parameters.AddWithValue("@ver", dto.Verificado);
                    cmdIns.Parameters.AddWithValue("@fec", fechaParsed.Date);
                    cmdIns.ExecuteNonQuery();
                }

                tx.Commit();
                return Ok(new { ok = true });
            }
            catch (SqlException ex)
            {
                tx.Rollback();
                return BadRequest(new { error = ex.Message, codigo = $"SQL_ERR_{ex.Number}" });
            }
        }

        [HttpPost]
        public IActionResult EditarMovimientoBilletera([FromBody] EditarMovimientoDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Concepto))
                return BadRequest(new { error = "El nombre del movimiento no puede estar vacío.", codigo = "ERR_EMPTY_NAME" });

            if (dto.Monto <= 0)
                return BadRequest(new { error = "El presupuesto/monto del movimiento debe ser mayor a $0.00.", codigo = "ERR_INVALID_AMOUNT" });

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                DateTime? fechaMov = null;
                using (var cmdFec = new SqlCommand("SELECT FechaMovimiento FROM MovimientosBilletera WHERE MovimientoId = @id AND UsuarioId = @uid", conn, tx))
                {
                    cmdFec.Parameters.AddWithValue("@id", dto.Id);
                    cmdFec.Parameters.AddWithValue("@uid", uid.Value);
                    var res = cmdFec.ExecuteScalar();
                    if (res != null) fechaMov = (DateTime)res;
                }

                if (fechaMov == null)
                {
                    tx.Rollback();
                    return BadRequest(new { error = "El movimiento no fue encontrado.", codigo = "ERR_MOV_NOT_FOUND" });
                }

                using (var cmdUpd = new SqlCommand(@"
                    UPDATE MovimientosBilletera 
                    SET CategoriaId = @cid, Concepto = @con, Monto = @mon, Verificado = @ver
                    WHERE MovimientoId = @id AND UsuarioId = @uid", conn, tx))
                {
                    cmdUpd.Parameters.AddWithValue("@cid", dto.CategoriaId);
                    cmdUpd.Parameters.AddWithValue("@con", dto.Concepto.Trim());
                    cmdUpd.Parameters.AddWithValue("@mon", dto.Monto);
                    cmdUpd.Parameters.AddWithValue("@ver", dto.Verificado);
                    cmdUpd.Parameters.AddWithValue("@id", dto.Id);
                    cmdUpd.Parameters.AddWithValue("@uid", uid.Value);
                    cmdUpd.ExecuteNonQuery();
                }

                // Verificar que no viole el bloqueo de sobregiro de ese día
                decimal presInicial = 0;
                bool bloquear = true;
                using (var cmdP = new SqlCommand("SELECT PresupuestoInicial, BloquearSobregiro FROM PresupuestosDiarios WHERE UsuarioId = @uid AND Fecha = @fec", conn, tx))
                {
                    cmdP.Parameters.AddWithValue("@uid", uid.Value);
                    cmdP.Parameters.AddWithValue("@fec", fechaMov.Value.Date);
                    using var r = cmdP.ExecuteReader();
                    if (r.Read())
                    {
                        presInicial = (decimal)r["PresupuestoInicial"];
                        bloquear = (bool)r["BloquearSobregiro"];
                    }
                }

                decimal aumentos = 0, disminuciones = 0, gastos = 0;
                string sqlSum = @"
                    SELECT 
                        ISNULL(SUM(CASE WHEN c.TipoFlujo = 'AUMENTO' THEN m.Monto ELSE 0 END), 0) AS Aumentos,
                        ISNULL(SUM(CASE WHEN c.TipoFlujo = 'DISMINUCION' THEN m.Monto ELSE 0 END), 0) AS Disminuciones,
                        ISNULL(SUM(CASE WHEN c.TipoFlujo = 'GASTO' THEN m.Monto ELSE 0 END), 0) AS Gastos
                    FROM MovimientosBilletera m
                    INNER JOIN CategoriasGasto c ON m.CategoriaId = c.CategoriaId
                    WHERE m.UsuarioId = @uid AND m.FechaMovimiento = @fec";

                using (var cmdSum = new SqlCommand(sqlSum, conn, tx))
                {
                    cmdSum.Parameters.AddWithValue("@uid", uid.Value);
                    cmdSum.Parameters.AddWithValue("@fec", fechaMov.Value.Date);
                    using var r = cmdSum.ExecuteReader();
                    if (r.Read())
                    {
                        aumentos = (decimal)r["Aumentos"];
                        disminuciones = (decimal)r["Disminuciones"];
                        gastos = (decimal)r["Gastos"];
                    }
                }

                if (bloquear && ((presInicial + aumentos - disminuciones) - gastos) < 0)
                {
                    tx.Rollback();
                    return BadRequest(new
                    {
                        error = "No se puede guardar la edición porque excede el presupuesto disponible del día y el bloqueo de sobregiros está activo.",
                        codigo = "ERR_EDIT_OVERDRAFT"
                    });
                }

                tx.Commit();
                return Ok(new { ok = true });
            }
            catch (SqlException ex)
            {
                tx.Rollback();
                return BadRequest(new { error = ex.Message, codigo = $"SQL_ERR_{ex.Number}" });
            }
        }

        [HttpPost]
        public IActionResult ToggleGastoVerificado(int id)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE MovimientosBilletera SET Verificado = ~Verificado WHERE MovimientoId = @id AND UsuarioId = @uid", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.ExecuteNonQuery();
            return Ok();
        }

        [HttpPost]
        public IActionResult EliminarGasto(int id)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM MovimientosBilletera WHERE MovimientoId = @id AND UsuarioId = @uid", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.ExecuteNonQuery();
            return Ok();
        }

        // =====================================================================
        // API: KANBAN
        // =====================================================================
        [HttpGet]
        public IActionResult ObtenerKanban()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

            var tareas = new List<KanbanTareaVm>();
            string sqlTareas = @"
                SELECT t.TareaId, t.Titulo, ISNULL(m.Nombre, 'General') AS Materia,
                       u.Nombre AS Urgencia, e.Codigo AS Estado, t.ImagenRuta
                FROM TareasKanban t
                INNER JOIN TablerosKanban tb ON t.TableroId = tb.TableroId
                LEFT JOIN Materias m ON t.MateriaId = m.MateriaId
                INNER JOIN NivelesUrgencia u ON t.UrgenciaId = u.UrgenciaId
                INNER JOIN EstadosKanban e ON t.EstadoId = e.EstadoId
                WHERE tb.PropietarioId = @uid
                ORDER BY t.TareaId DESC";

            using (var cmd = new SqlCommand(sqlTareas, conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    tareas.Add(new KanbanTareaVm
                    {
                        id = (int)r["TareaId"],
                        titulo = r["Titulo"].ToString()!,
                        materia = r["Materia"].ToString()!,
                        urgencia = r["Urgencia"].ToString()!,
                        estado = r["Estado"].ToString()!,
                        imagen = r["ImagenRuta"] == DBNull.Value ? null : r["ImagenRuta"].ToString()
                    });
                }
            }

            foreach (var t in tareas)
            {
                using var cmdSub = new SqlCommand("SELECT SubtareaId, Texto, Completado FROM SubtareasChecklist WHERE TareaId = @tid ORDER BY SubtareaId", conn);
                cmdSub.Parameters.AddWithValue("@tid", t.id);
                using var rSub = cmdSub.ExecuteReader();
                while (rSub.Read())
                {
                    t.subtareas.Add(new
                    {
                        id = (int)rSub["SubtareaId"],
                        texto = rSub["Texto"].ToString(),
                        hecho = (bool)rSub["Completado"]
                    });
                }
            }

            return Json(tareas);
        }

        [HttpPost]
        public async Task<IActionResult> CrearTareaKanban([FromForm] string titulo, [FromForm] string? materia, [FromForm] string urgencia, [FromForm] string? subtareasComa, [FromForm] IFormFile? archivoImagen)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(titulo))
                return BadRequest(new { error = "El título de la tarea es obligatorio.", codigo = "ERR_EMPTY_TITLE" });

            string? rutaRelativaImg = null;
            if (archivoImagen != null && archivoImagen.Length > 0)
            {
                if (!archivoImagen.ContentType.StartsWith("image/"))
                    return BadRequest(new { error = "Solo se permiten archivos de imagen.", codigo = "ERR_MIME_TYPE" });

                string carpetaUploads = Path.Combine(_env.WebRootPath, "uploads");
                Directory.CreateDirectory(carpetaUploads);

                string nombreArchivo = $"kanban_{DateTime.Now.Ticks}{Path.GetExtension(archivoImagen.FileName)}";
                string rutaCompleta = Path.Combine(carpetaUploads, nombreArchivo);

                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await archivoImagen.CopyToAsync(stream);
                }
                rutaRelativaImg = $"/uploads/{nombreArchivo}";
            }

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                string nombreMat = string.IsNullOrWhiteSpace(materia) ? "General" : materia.Trim();
                int materiaId;
                string sqlMat = @"
                    IF NOT EXISTS (SELECT 1 FROM Materias WHERE UsuarioId = @uid AND Nombre = @nom)
                        INSERT INTO Materias (UsuarioId, Nombre, ColorId) VALUES (@uid, @nom, 1);
                    SELECT MateriaId FROM Materias WHERE UsuarioId = @uid AND Nombre = @nom;";
                using (var cmdMat = new SqlCommand(sqlMat, conn, tx))
                {
                    cmdMat.Parameters.AddWithValue("@uid", uid.Value);
                    cmdMat.Parameters.AddWithValue("@nom", nombreMat);
                    materiaId = (int)cmdMat.ExecuteScalar()!;
                }

                string sqlInsert = @"
                    IF NOT EXISTS (SELECT 1 FROM TablerosKanban WHERE PropietarioId = @uid)
                        INSERT INTO TablerosKanban (Nombre, PropietarioId, EsCompartido) VALUES ('Mi Tablero Escolar', @uid, 0);

                    DECLARE @urgId INT = (SELECT UrgenciaId FROM NivelesUrgencia WHERE Nombre = @urg);
                    DECLARE @estId INT = (SELECT EstadoId FROM EstadosKanban WHERE Codigo = 'todo');
                    DECLARE @tabId INT = (SELECT TOP 1 TableroId FROM TablerosKanban WHERE PropietarioId = @uid);

                    INSERT INTO TareasKanban (TableroId, MateriaId, UrgenciaId, EstadoId, Titulo, ImagenRuta)
                    OUTPUT INSERTED.TareaId
                    VALUES (@tabId, @matId, ISNULL(@urgId, 2), @estId, @tit, @img);";

                int nuevaTareaId;
                using (var cmdTarea = new SqlCommand(sqlInsert, conn, tx))
                {
                    cmdTarea.Parameters.AddWithValue("@uid", uid.Value);
                    cmdTarea.Parameters.AddWithValue("@matId", materiaId);
                    cmdTarea.Parameters.AddWithValue("@urg", urgencia ?? "Media");
                    cmdTarea.Parameters.AddWithValue("@tit", titulo.Trim());
                    cmdTarea.Parameters.AddWithValue("@img", (object?)rutaRelativaImg ?? DBNull.Value);
                    nuevaTareaId = (int)cmdTarea.ExecuteScalar()!;
                }

                if (!string.IsNullOrWhiteSpace(subtareasComa))
                {
                    var items = subtareasComa.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var item in items)
                    {
                        using var cmdSub = new SqlCommand("INSERT INTO SubtareasChecklist (TareaId, Texto, Completado) VALUES (@tid, @txt, 0)", conn, tx);
                        cmdSub.Parameters.AddWithValue("@tid", nuevaTareaId);
                        cmdSub.Parameters.AddWithValue("@txt", item);
                        cmdSub.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                return Ok(new { ok = true });
            }
            catch (SqlException ex)
            {
                tx.Rollback();
                return BadRequest(new { error = ex.Message, codigo = $"SQL_ERR_{ex.Number}" });
            }
        }

        [HttpPost]
        public IActionResult ToggleSubtarea(int subtareaId)
        {
            if (ObtenerUsuarioSesion() == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            string sql = @"
                UPDATE SubtareasChecklist SET Completado = ~Completado WHERE SubtareaId = @sid;
                DECLARE @tid INT = (SELECT TareaId FROM SubtareasChecklist WHERE SubtareaId = @sid);
                DECLARE @total INT = (SELECT COUNT(*) FROM SubtareasChecklist WHERE TareaId = @tid);
                DECLARE @hechos INT = (SELECT SUM(CASE WHEN Completado = 1 THEN 1 ELSE 0 END) FROM SubtareasChecklist WHERE TareaId = @tid);

                DECLARE @nuevoCodigo VARCHAR(15) = NULL;
                IF (@total > 0 AND @total = @hechos) SET @nuevoCodigo = 'done';
                ELSE IF (@hechos > 0) SET @nuevoCodigo = 'doing';

                IF (@nuevoCodigo IS NOT NULL)
                BEGIN
                    UPDATE TareasKanban 
                    SET EstadoId = (SELECT EstadoId FROM EstadosKanban WHERE Codigo = @nuevoCodigo)
                    WHERE TareaId = @tid;
                END
                SELECT ISNULL(@nuevoCodigo, '') AS EstadoAuto;";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@sid", subtareaId);
            string estadoAuto = cmd.ExecuteScalar()?.ToString() ?? "";
            return Json(new { estadoAuto });
        }

        [HttpPost]
        public IActionResult AgregarSubtarea(int tareaId, string texto)
        {
            if (ObtenerUsuarioSesion() == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO SubtareasChecklist (TareaId, Texto, Completado) VALUES (@tid, @txt, 0)", conn);
            cmd.Parameters.AddWithValue("@tid", tareaId);
            cmd.Parameters.AddWithValue("@txt", texto.Trim());
            cmd.ExecuteNonQuery();
            return Ok();
        }

        [HttpPost]
        public IActionResult MoverTareaKanban(int tareaId, string nuevoEstadoCodigo)
        {
            if (ObtenerUsuarioSesion() == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            string sql = "UPDATE TareasKanban SET EstadoId = (SELECT EstadoId FROM EstadosKanban WHERE Codigo = @cod) WHERE TareaId = @tid";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cod", nuevoEstadoCodigo);
            cmd.Parameters.AddWithValue("@tid", tareaId);
            cmd.ExecuteNonQuery();
            return Ok();
        }

        [HttpPost]
        public IActionResult EliminarTareaKanban(int tareaId)
        {
            if (ObtenerUsuarioSesion() == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM TareasKanban WHERE TareaId = @tid", conn);
            cmd.Parameters.AddWithValue("@tid", tareaId);
            cmd.ExecuteNonQuery();
            return Ok();
        }

        // =====================================================================
        // API: HORARIO
        // =====================================================================
        [HttpGet]
        public IActionResult ObtenerHorario()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            AsegurarCatalogosBase(conn);

            var dias = new List<object>();
            using (var cmd = new SqlCommand("SELECT DiaId, Nombre FROM DiasSemana ORDER BY DiaId", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) dias.Add(new { id = (byte)r["DiaId"], nombre = r["Nombre"].ToString() });

            var edificios = new List<object>();
            using (var cmd = new SqlCommand("SELECT EdificioId, Nombre FROM Edificios ORDER BY EdificioId", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) edificios.Add(new { id = (int)r["EdificioId"], nombre = r["Nombre"].ToString() });

            var colores = new List<object>();
            using (var cmd = new SqlCommand("SELECT ColorId, Nombre, CodigoHex FROM ColoresTema ORDER BY ColorId", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) colores.Add(new { id = (int)r["ColorId"], nombre = r["Nombre"].ToString(), hex = r["CodigoHex"].ToString() });

            var clases = new List<object>();
            string sqlClases = @"
                SELECT hc.HorarioId, m.Nombre AS Materia, hc.DiaId, d.Nombre AS DiaNombre,
                       LEFT(CAST(hc.HoraInicio AS VARCHAR), 5) AS HoraInicio,
                       LEFT(CAST(hc.HoraFin AS VARCHAR), 5) AS HoraFin,
                       DATEPART(HOUR, hc.HoraInicio) AS HoraInicioInt,
                       DATEPART(MINUTE, hc.HoraInicio) AS MinInicioInt,
                       DATEPART(HOUR, hc.HoraFin) AS HoraFinInt,
                       DATEPART(MINUTE, hc.HoraFin) AS MinFinInt,
                       e.Nombre AS Edificio, hc.EsLaboratorio, hc.NumeroSalon, hc.NombreLaboratorio,
                       c.CodigoHex, c.Nombre AS ColorNombre
                FROM HorarioClases hc
                INNER JOIN Materias m ON hc.MateriaId = m.MateriaId
                INNER JOIN DiasSemana d ON hc.DiaId = d.DiaId
                INNER JOIN Edificios e ON hc.EdificioId = e.EdificioId
                INNER JOIN ColoresTema c ON m.ColorId = c.ColorId
                WHERE m.UsuarioId = @uid
                ORDER BY hc.HoraInicio";

            using (var cmd = new SqlCommand(sqlClases, conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    bool esLab = (bool)r["EsLaboratorio"];
                    string ubicacion = esLab
                        ? $"Lab: {r["NombreLaboratorio"]}"
                        : $"Salón {r["NumeroSalon"]}";

                    clases.Add(new
                    {
                        id = (int)r["HorarioId"],
                        materia = r["Materia"].ToString(),
                        diaId = (byte)r["DiaId"],
                        diaNombre = r["DiaNombre"].ToString(),
                        horaInicio = r["HoraInicio"].ToString(),
                        horaFin = r["HoraFin"].ToString(),
                        horaInicioInt = (int)r["HoraInicioInt"],
                        minInicioInt = (int)r["MinInicioInt"],
                        horaFinInt = (int)r["HoraFinInt"],
                        minFinInt = (int)r["MinFinInt"],
                        edificio = r["Edificio"].ToString(),
                        esLaboratorio = esLab,
                        numeroSalon = r["NumeroSalon"] == DBNull.Value ? (int?)null : (int)r["NumeroSalon"],
                        nombreLaboratorio = r["NombreLaboratorio"] == DBNull.Value ? null : r["NombreLaboratorio"].ToString(),
                        ubicacionMostrar = ubicacion,
                        color = r["CodigoHex"].ToString(),
                        colorNombre = r["ColorNombre"].ToString()
                    });
                }
            }

            return Json(new { dias, edificios, colores, clases });
        }

        [HttpPost]
        public IActionResult AsignarHorario([FromBody] AsignarHorarioDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Materia))
                return BadRequest(new { error = "El nombre de la materia es obligatorio.", codigo = "ERR_EMPTY_SUBJECT" });

            if (dto.DiasIds == null || dto.DiasIds.Count == 0)
                return BadRequest(new { error = "Selecciona al menos un día de la semana.", codigo = "ERR_NO_DAYS" });

            if (!TimeSpan.TryParse(dto.HoraInicio, out TimeSpan tInicio) || !TimeSpan.TryParse(dto.HoraFin, out TimeSpan tFin))
                return BadRequest(new { error = "Formato de hora inválido.", codigo = "ERR_TIME_FORMAT" });

            if (tInicio < new TimeSpan(7, 0, 0) || tFin > new TimeSpan(22, 0, 0) || tFin <= tInicio)
                return BadRequest(new { error = "El horario debe estar entre 07:00 y 22:00 hrs, y la hora de fin debe ser posterior a la de inicio.", codigo = "ERR_TIME_RANGE" });

            if (dto.EsLaboratorio)
            {
                if (string.IsNullOrWhiteSpace(dto.NombreLaboratorio))
                    return BadRequest(new { error = "Ingresa el nombre del laboratorio.", codigo = "ERR_LAB_EMPTY" });
            }
            else
            {
                if (dto.NumeroSalon == null)
                    return BadRequest(new { error = "Ingresa el número de salón.", codigo = "ERR_ROOM_EMPTY" });

                int s = dto.NumeroSalon.Value;
                bool salonValido = (s >= 101 && s <= 115) || (s >= 201 && s <= 215) || (s >= 301 && s <= 315);
                if (!salonValido)
                    return BadRequest(new { error = "El número de salón solo permite los intervalos: 101 a 115, 201 a 215 o 301 a 315.", codigo = "ERR_ROOM_INTERVAL" });
            }

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                string sqlMat = @"
                    IF NOT EXISTS (SELECT 1 FROM Materias WHERE UsuarioId = @uid AND Nombre = @nom)
                        INSERT INTO Materias (UsuarioId, Nombre, ColorId) VALUES (@uid, @nom, @col);
                    ELSE
                        UPDATE Materias SET ColorId = @col WHERE UsuarioId = @uid AND Nombre = @nom;
                    SELECT MateriaId FROM Materias WHERE UsuarioId = @uid AND Nombre = @nom;";

                int materiaId;
                using (var cmdMat = new SqlCommand(sqlMat, conn, tx))
                {
                    cmdMat.Parameters.AddWithValue("@uid", uid.Value);
                    cmdMat.Parameters.AddWithValue("@nom", dto.Materia.Trim());
                    cmdMat.Parameters.AddWithValue("@col", dto.ColorId);
                    materiaId = (int)cmdMat.ExecuteScalar()!;
                }

                foreach (byte diaId in dto.DiasIds)
                {
                    using var cmdSp = new SqlCommand("sp_AsignarBloqueHorario", conn, tx);
                    cmdSp.CommandType = CommandType.StoredProcedure;
                    cmdSp.Parameters.AddWithValue("@MateriaId", materiaId);
                    cmdSp.Parameters.AddWithValue("@DiaId", diaId);
                    cmdSp.Parameters.AddWithValue("@HoraInicio", tInicio);
                    cmdSp.Parameters.AddWithValue("@HoraFin", tFin);
                    cmdSp.Parameters.AddWithValue("@EdificioId", dto.EdificioId);
                    cmdSp.Parameters.AddWithValue("@EsLaboratorio", dto.EsLaboratorio);
                    cmdSp.Parameters.AddWithValue("@NumeroSalon", dto.EsLaboratorio ? DBNull.Value : dto.NumeroSalon);
                    cmdSp.Parameters.AddWithValue("@NombreLaboratorio", dto.EsLaboratorio ? dto.NombreLaboratorio!.Trim() : DBNull.Value);
                    cmdSp.ExecuteNonQuery();
                }

                tx.Commit();
                return Ok(new { ok = true });
            }
            catch (SqlException ex)
            {
                tx.Rollback();
                return BadRequest(new { error = ex.Message, codigo = $"SQL_ERR_{ex.Number}" });
            }
        }

        [HttpPost]
        public IActionResult EliminarBloqueHorario(int horarioId)
        {
            if (ObtenerUsuarioSesion() == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM HorarioClases WHERE HorarioId = @hid", conn);
            cmd.Parameters.AddWithValue("@hid", horarioId);
            cmd.ExecuteNonQuery();
            return Ok();
        }
    }

    // DTOs
    public class LoginDto { public string Correo { get; set; } = ""; public string Password { get; set; } = ""; }
    public class RegistroDto
    {
        public string Nombre { get; set; } = "";
        public string ApellidoPaterno { get; set; } = "";
        public string? ApellidoMaterno { get; set; }
        public string Correo { get; set; } = "";
        public string Password { get; set; } = "";
    }
    public class NotifDto { public string? Titulo { get; set; } public string? Mensaje { get; set; } public string? Tipo { get; set; } }
    public class GuardarPresupuestoDiaDto { public string Fecha { get; set; } = ""; public decimal PresupuestoInicial { get; set; } }
    public class SwitchesBilleteraDto { public string Fecha { get; set; } = ""; public bool BloquearSobregiro { get; set; } public bool SoloContarVerificados { get; set; } }
    public class MovimientoDiaDto
    {
        public string Fecha { get; set; } = "";
        public int CategoriaId { get; set; }
        public string Concepto { get; set; } = "";
        public decimal Monto { get; set; }
        public bool Verificado { get; set; }
    }
    public class EditarMovimientoDto
    {
        public int Id { get; set; }
        public int CategoriaId { get; set; }
        public string Concepto { get; set; } = "";
        public decimal Monto { get; set; }
        public bool Verificado { get; set; }
    }
    public class KanbanTareaVm { public int id { get; set; } public string titulo { get; set; } = ""; public string materia { get; set; } = ""; public string urgencia { get; set; } = ""; public string estado { get; set; } = ""; public string? imagen { get; set; } public List<object> subtareas { get; set; } = new(); }
    public class AsignarHorarioDto
    {
        public string Materia { get; set; } = "";
        public int EdificioId { get; set; }
        public bool EsLaboratorio { get; set; }
        public int? NumeroSalon { get; set; }
        public string? NombreLaboratorio { get; set; }
        public List<byte> DiasIds { get; set; } = new();
        public string HoraInicio { get; set; } = "07:00";
        public string HoraFin { get; set; } = "08:30";
        public int ColorId { get; set; }
    }
}