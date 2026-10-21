using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Practica9.Data;
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

        // Obtiene el UsuarioId de la sesión activa (o null si no ha iniciado sesión)
        private int? ObtenerUsuarioSesion() => HttpContext.Session.GetInt32("UsuarioId");

        // Encriptación SHA-256 para la columna PasswordHash
        private static string HashPassword(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        // Garantiza que los catálogos 3FN existan en GarAngelitoToolsProDB
        private void AsegurarCatalogosBase(SqlConnection conn)
        {
            string sqlCatalogos = @"
                IF NOT EXISTS (SELECT 1 FROM Roles)
                BEGIN
                    INSERT INTO Roles (Nombre) VALUES ('Estudiante'), ('Administrador');
                END

                IF NOT EXISTS (SELECT 1 FROM Carreras)
                BEGIN
                    INSERT INTO Carreras (Nombre, Clave) VALUES 
                    ('Ingeniería en Informática', 'IINF-2020'),
                    ('Ciencias de la Informática', 'CINF-2020'),
                    ('Ingeniería en Sistemas Computacionales', 'ISC-2020'),
                    ('Ingeniería Industrial', 'IIND-2020');
                END

                IF NOT EXISTS (SELECT 1 FROM TiposNotificacion)
                BEGIN
                    INSERT INTO TiposNotificacion (Codigo, IconoBootstrap) VALUES 
                    ('success', 'bi-check-circle-fill'), ('warning', 'bi-exclamation-triangle-fill'), 
                    ('danger', 'bi-x-circle-fill'), ('info', 'bi-info-circle-fill');
                END

                IF NOT EXISTS (SELECT 1 FROM CategoriasGasto)
                BEGIN
                    INSERT INTO CategoriasGasto (Nombre, IconoBootstrap) VALUES 
                    ('Transporte', 'bi-bus-front'), ('Comida', 'bi-egg-fried'), 
                    ('Escolar', 'bi-book'), ('Otro', 'bi-lightning');
                END

                IF NOT EXISTS (SELECT 1 FROM NivelesUrgencia)
                BEGIN
                    INSERT INTO NivelesUrgencia (Nombre, PesoPrioridad) VALUES ('Baja', 1), ('Media', 2), ('Alta', 3);
                END

                IF NOT EXISTS (SELECT 1 FROM EstadosKanban)
                BEGIN
                    INSERT INTO EstadosKanban (Codigo, NombreMostrar) VALUES 
                    ('todo', 'Por Hacer'), ('doing', 'En Proceso'), ('done', 'Terminado');
                END

                IF NOT EXISTS (SELECT 1 FROM DiasSemana)
                BEGIN
                    INSERT INTO DiasSemana (DiaId, Nombre) VALUES 
                    (1, 'Lunes'), (2, 'Martes'), (3, 'Miércoles'), (4, 'Jueves'), (5, 'Viernes');
                END

                IF NOT EXISTS (SELECT 1 FROM BloquesTiempo)
                BEGIN
                    INSERT INTO BloquesTiempo (HoraInicio, HoraFin) VALUES 
                    ('07:00', '08:30'), ('08:30', '10:00'), ('10:00', '11:30'), 
                    ('11:30', '13:00'), ('13:00', '14:30'), ('14:30', '16:00');
                END

                IF NOT EXISTS (SELECT 1 FROM ColoresTema)
                BEGIN
                    INSERT INTO ColoresTema (Nombre, CodigoHex) VALUES 
                    ('Azul Pastel', '#e7f5ff'), ('Verde Pastel', '#ebfbee'), 
                    ('Amarillo Pastel', '#fff9db'), ('Morado Pastel', '#f3f0ff'), ('Rojo Pastel', '#fff5f5');
                END

                IF NOT EXISTS (SELECT 1 FROM TiposFuenteApa)
                BEGIN
                    INSERT INTO TiposFuenteApa (Codigo, Descripcion) VALUES 
                    ('libro', 'Libro Impreso o Digital'), ('web', 'Sitio Web'), ('articulo', 'Artículo de Revista');
                END";

            using var cmd = new SqlCommand(sqlCatalogos, conn);
            cmd.ExecuteNonQuery();
        }

        // =====================================================================
        // VISTAS Y ENDPOINTS DE AUTENTICACIÓN (LOGIN Y REGISTRO DE ESTUDIANTES)
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

            var carreras = new List<object>();
            using (var cmd = new SqlCommand("SELECT CarreraId, Nombre, Clave FROM Carreras ORDER BY Nombre", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    carreras.Add(new
                    {
                        id = (int)r["CarreraId"],
                        nombre = $"{r["Nombre"]} ({r["Clave"]})"
                    });
                }
            }
            ViewBag.CarrerasJson = System.Text.Json.JsonSerializer.Serialize(carreras);
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
                SELECT u.UsuarioId, u.Nombre, u.ApellidoPaterno, r.Nombre AS RolNombre
                FROM Usuarios u
                INNER JOIN Roles r ON u.RolId = r.RolId
                WHERE u.Correo = @cor AND (u.PasswordHash = @hash OR u.PasswordHash = @raw)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cor", dto.Correo.Trim());
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@raw", dto.Password); // Por si insertaste un usuario de prueba manual en SQL

            using var r = cmd.ExecuteReader();
            if (!r.Read())
            {
                return Unauthorized(new { error = "Correo institucional o contraseña incorrectos.", codigo = "AUTH_INVALID_401" });
            }

            int uid = (int)r["UsuarioId"];
            string nombreMostrar = $"{r["Nombre"]} {r["ApellidoPaterno"]}";
            string rol = r["RolNombre"].ToString()!;

            HttpContext.Session.SetInt32("UsuarioId", uid);
            HttpContext.Session.SetString("UsuarioNombre", nombreMostrar);
            HttpContext.Session.SetString("UsuarioRol", rol);

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

            if (dto.SemestreActual < 1 || dto.SemestreActual > 12)
                return BadRequest(new { error = "El semestre debe estar entre 1 y 12 (Restricción CK de BD).", codigo = "REG_INVALID_SEMESTER" });

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            AsegurarCatalogosBase(conn);

            using var tx = conn.BeginTransaction();
            try
            {
                // 1. Verificar que el correo no esté registrado
                using (var cmdCheck = new SqlCommand("SELECT COUNT(1) FROM Usuarios WHERE Correo = @cor", conn, tx))
                {
                    cmdCheck.Parameters.AddWithValue("@cor", dto.Correo.Trim());
                    if ((int)cmdCheck.ExecuteScalar()! > 0)
                    {
                        tx.Rollback();
                        return BadRequest(new { error = "Este correo electrónico ya está registrado en GarAngelito Tools Pro.", codigo = "SQL_UQ_EMAIL_EXISTS" });
                    }
                }

                // 2. Forzar estrictamente el RolId de 'Estudiante' desde la BD
                int rolEstudianteId;
                using (var cmdRol = new SqlCommand("SELECT RolId FROM Roles WHERE Nombre = 'Estudiante'", conn, tx))
                {
                    rolEstudianteId = (int)cmdRol.ExecuteScalar()!;
                }

                // 3. Insertar Usuario con atributos 100% atómicos (1FN)
                string sqlInsertUser = @"
                    INSERT INTO Usuarios (Nombre, ApellidoPaterno, ApellidoMaterno, Correo, PasswordHash, CarreraId, SemestreActual, RolId)
                    OUTPUT INSERTED.UsuarioId
                    VALUES (@nom, @apPat, @apMat, @cor, @hash, @carId, @sem, @rolId);";

                int nuevoUsuarioId;
                using (var cmdUser = new SqlCommand(sqlInsertUser, conn, tx))
                {
                    cmdUser.Parameters.AddWithValue("@nom", dto.Nombre.Trim());
                    cmdUser.Parameters.AddWithValue("@apPat", dto.ApellidoPaterno.Trim());
                    cmdUser.Parameters.AddWithValue("@apMat", string.IsNullOrWhiteSpace(dto.ApellidoMaterno) ? DBNull.Value : dto.ApellidoMaterno.Trim());
                    cmdUser.Parameters.AddWithValue("@cor", dto.Correo.Trim());
                    cmdUser.Parameters.AddWithValue("@hash", HashPassword(dto.Password));
                    cmdUser.Parameters.AddWithValue("@carId", dto.CarreraId);
                    cmdUser.Parameters.AddWithValue("@sem", dto.SemestreActual);
                    cmdUser.Parameters.AddWithValue("@rolId", rolEstudianteId);
                    nuevoUsuarioId = (int)cmdUser.ExecuteScalar()!;
                }

                // 4. Inicializar registros base del Estudiante (Billetera, Tablero Kanban, Checks y Notificación)
                string sqlInitAlumno = @"
                    INSERT INTO ConfiguracionBilletera (UsuarioId, PresupuestoDiario, BloquearSobregiro, SoloContarVerificados)
                    VALUES (@uid, 200.00, 1, 0);

                    INSERT INTO TablerosKanban (Nombre, PropietarioId, EsCompartido)
                    VALUES ('Mi Tablero Escolar', @uid, 0);

                    INSERT INTO RutinaChecks (UsuarioId, Descripcion, CompletadoHoy) VALUES
                    (@uid, 'Credencial escolar', 0),
                    (@uid, 'Cargador Laptop / Celular', 0),
                    (@uid, 'Tarjeta de transporte', 0),
                    (@uid, 'Material de laboratorio', 0);

                    DECLARE @tipoSuccess INT = (SELECT TOP 1 TipoNotificacionId FROM TiposNotificacion WHERE Codigo = 'success');
                    INSERT INTO Notificaciones (UsuarioId, TipoNotificacionId, Titulo, Mensaje, Leida)
                    VALUES (@uid, @tipoSuccess, '¡Bienvenido a GarAngelito Tools Pro!', 'Tu cuenta de Estudiante fue creada exitosamente en 3FN.', 0);";

                using (var cmdInit = new SqlCommand(sqlInitAlumno, conn, tx))
                {
                    cmdInit.Parameters.AddWithValue("@uid", nuevoUsuarioId);
                    cmdInit.ExecuteNonQuery();
                }

                tx.Commit();

                // Iniciar sesión automáticamente tras el registro
                HttpContext.Session.SetInt32("UsuarioId", nuevoUsuarioId);
                HttpContext.Session.SetString("UsuarioNombre", $"{dto.Nombre.Trim()} {dto.ApellidoPaterno.Trim()}");
                HttpContext.Session.SetString("UsuarioRol", "Estudiante");

                return Ok(new { ok = true, redirectUrl = Url.Action("Index", "Home") });
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
        // VISTAS PRINCIPALES (PROTEGIDAS POR SESIÓN)
        // =====================================================================
        public IActionResult Index()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return RedirectToAction("Login");

            var hoy = DateTime.Now;
            var diasEnMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
            ViewBag.ProgresoMes = Math.Round((double)hoy.Day / diasEnMes * 100);
            ViewBag.FechaActual = hoy.ToString("dddd, dd 'de' MMMM yyyy");

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

            string sqlUser = @"
                SELECT u.Nombre, u.ApellidoPaterno, u.ApellidoMaterno, u.SemestreActual, c.Nombre AS Carrera, r.Nombre AS Rol
                FROM Usuarios u
                INNER JOIN Carreras c ON u.CarreraId = c.CarreraId
                INNER JOIN Roles r ON u.RolId = r.RolId
                WHERE u.UsuarioId = @uid";

            using (var cmd = new SqlCommand(sqlUser, conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    ViewBag.AlumnoNombre = $"{r["Nombre"]} {r["ApellidoPaterno"]} {r["ApellidoMaterno"]}".Trim();
                    ViewBag.AlumnoCarrera = $"{r["Carrera"]} • {r["SemestreActual"]}° Semestre ({r["Rol"]})";
                }
            }

            var checks = new List<dynamic>();
            using (var cmd = new SqlCommand("SELECT CheckId, Descripcion, CompletadoHoy FROM RutinaChecks WHERE UsuarioId = @uid", conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    checks.Add(new
                    {
                        CheckId = (int)r["CheckId"],
                        Descripcion = r["Descripcion"].ToString(),
                        CompletadoHoy = (bool)r["CompletadoHoy"]
                    });
                }
            }
            ViewBag.RutinaChecks = checks;

            return View();
        }

        public IActionResult Billetera() => ObtenerUsuarioSesion() == null ? RedirectToAction("Login") : View();
        public IActionResult Kanban() => ObtenerUsuarioSesion() == null ? RedirectToAction("Login") : View();
        public IActionResult Horario() => ObtenerUsuarioSesion() == null ? RedirectToAction("Login") : View();
        public IActionResult Apa() => ObtenerUsuarioSesion() == null ? RedirectToAction("Login") : View();

        // =====================================================================
        // API: CHECKS DE RUTINA (INDEX)
        // =====================================================================
        [HttpPost]
        public IActionResult ToggleRutinaCheck(int checkId, bool estado)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE RutinaChecks SET CompletadoHoy = @est, FechaActualizacion = CAST(SYSDATETIME() AS DATE) WHERE CheckId = @id AND UsuarioId = @uid", conn);
            cmd.Parameters.AddWithValue("@est", estado);
            cmd.Parameters.AddWithValue("@id", checkId);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.ExecuteNonQuery();
            return Ok(new { ok = true });
        }

        // =====================================================================
        // API: CENTRO DE NOTIFICACIONES EN SQL SERVER (_Layout.cshtml)
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
        // API MÓDULO 2: BILLETERA DIARIA (CON sp_RegistrarGastoBilletera)
        // =====================================================================
        [HttpGet]
        public IActionResult ObtenerBilletera()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

            // Asegurar que el usuario tenga su fila en ConfiguracionBilletera
            string sqlEnsureWallet = @"
                IF NOT EXISTS (SELECT 1 FROM ConfiguracionBilletera WHERE UsuarioId = @uid)
                    INSERT INTO ConfiguracionBilletera (UsuarioId, PresupuestoDiario, BloquearSobregiro, SoloContarVerificados)
                    VALUES (@uid, 200.00, 1, 0);";
            using (var cmdE = new SqlCommand(sqlEnsureWallet, conn))
            {
                cmdE.Parameters.AddWithValue("@uid", uid.Value);
                cmdE.ExecuteNonQuery();
            }

            decimal presupuesto = 200;
            bool bloquear = true, soloVerif = false;

            using (var cmd = new SqlCommand("SELECT PresupuestoDiario, BloquearSobregiro, SoloContarVerificados FROM ConfiguracionBilletera WHERE UsuarioId = @uid", conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    presupuesto = (decimal)r["PresupuestoDiario"];
                    bloquear = (bool)r["BloquearSobregiro"];
                    soloVerif = (bool)r["SoloContarVerificados"];
                }
            }

            var categorias = new List<object>();
            using (var cmd = new SqlCommand("SELECT CategoriaId, Nombre FROM CategoriasGasto ORDER BY CategoriaId", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    categorias.Add(new { id = (int)r["CategoriaId"], nombre = r["Nombre"].ToString() });
            }

            var gastos = new List<object>();
            string sqlGastos = @"
                SELECT m.MovimientoId, m.Concepto, m.Monto, m.Verificado, 
                       FORMAT(m.FechaHora, 'HH:mm') AS Hora, c.Nombre AS Categoria
                FROM MovimientosBilletera m
                INNER JOIN CategoriasGasto c ON m.CategoriaId = c.CategoriaId
                WHERE m.UsuarioId = @uid AND CAST(m.FechaHora AS DATE) = CAST(SYSDATETIME() AS DATE)
                ORDER BY m.MovimientoId DESC";

            using (var cmd = new SqlCommand(sqlGastos, conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    gastos.Add(new
                    {
                        id = (int)r["MovimientoId"],
                        concepto = r["Concepto"].ToString(),
                        monto = (decimal)r["Monto"],
                        verificado = (bool)r["Verificado"],
                        hora = r["Hora"].ToString(),
                        categoria = r["Categoria"].ToString()
                    });
                }
            }

            return Json(new { presupuesto, bloquearSobregiro = bloquear, soloContarVerificados = soloVerif, categorias, gastos });
        }

        [HttpPost]
        public IActionResult AgregarGasto([FromBody] GastoDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            try
            {
                using var conn = DbSingleton.Instancia.CrearConexion();
                conn.Open();
                using var cmd = new SqlCommand("sp_RegistrarGastoBilletera", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@UsuarioId", uid.Value);
                cmd.Parameters.AddWithValue("@CategoriaId", dto.CategoriaId);
                cmd.Parameters.AddWithValue("@Concepto", dto.Concepto);
                cmd.Parameters.AddWithValue("@Monto", dto.Monto);
                cmd.Parameters.AddWithValue("@Verificado", dto.Verificado);
                cmd.ExecuteNonQuery();

                return Ok(new { ok = true });
            }
            catch (SqlException ex)
            {
                return BadRequest(new { error = ex.Message, codigo = $"SQL_ERR_{ex.Number}" });
            }
        }

        [HttpPost]
        public IActionResult ActualizarConfigBilletera([FromBody] ConfigBilleteraDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            string sql = @"
                UPDATE ConfiguracionBilletera 
                SET PresupuestoDiario = @pres, BloquearSobregiro = @bloq, SoloContarVerificados = @solo
                WHERE UsuarioId = @uid";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pres", dto.Presupuesto);
            cmd.Parameters.AddWithValue("@bloq", dto.BloquearSobregiro);
            cmd.Parameters.AddWithValue("@solo", dto.SoloContarVerificados);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.ExecuteNonQuery();
            return Ok();
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

        [HttpPost]
        public IActionResult ReiniciarBilleteraHoy()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM MovimientosBilletera WHERE UsuarioId = @uid AND CAST(FechaHora AS DATE) = CAST(SYSDATETIME() AS DATE)", conn);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.ExecuteNonQuery();
            return Ok();
        }

        // =====================================================================
        // API MÓDULO 3: KANBAN + SUBTAREAS CHECKLIST + IMÁGENES
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
                // 1. Obtener o crear Materia en la tabla Materias (3FN)
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

                // 2. Asegurar que el estudiante tenga su TableroKanban antes de insertar la tarea
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

                // 3. Insertar cada Check en SubtareasChecklist
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
        // API MÓDULO 4: HORARIO DE CLASES (CON sp_AsignarBloqueHorario)
        // =====================================================================
        [HttpGet]
        public IActionResult ObtenerHorario()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

            var dias = new List<object>();
            using (var cmd = new SqlCommand("SELECT DiaId, Nombre FROM DiasSemana ORDER BY DiaId", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) dias.Add(new { id = (byte)r["DiaId"], nombre = r["Nombre"].ToString() });

            var horas = new List<object>();
            using (var cmd = new SqlCommand("SELECT BloqueTiempoId, CONCAT(LEFT(CAST(HoraInicio AS VARCHAR), 5), ' - ', LEFT(CAST(HoraFin AS VARCHAR), 5)) AS Rango FROM BloquesTiempo ORDER BY HoraInicio", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) horas.Add(new { id = (int)r["BloqueTiempoId"], rango = r["Rango"].ToString() });

            var colores = new List<object>();
            using (var cmd = new SqlCommand("SELECT ColorId, Nombre, CodigoHex FROM ColoresTema ORDER BY ColorId", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) colores.Add(new { id = (int)r["ColorId"], nombre = r["Nombre"].ToString(), hex = r["CodigoHex"].ToString() });

            var clases = new List<object>();
            string sqlClases = @"
                SELECT hc.HorarioId, m.Nombre AS Materia, ISNULL(hc.SalonAsignado, '') AS Salon,
                       hc.DiaId, hc.BloqueTiempoId, c.CodigoHex,
                       ISNULL(a.Asistio, 0) AS AsistidoHoy
                FROM HorarioClases hc
                INNER JOIN Materias m ON hc.MateriaId = m.MateriaId
                INNER JOIN ColoresTema c ON m.ColorId = c.ColorId
                LEFT JOIN AsistenciasClase a ON hc.HorarioId = a.HorarioId AND a.FechaClase = CAST(SYSDATETIME() AS DATE)
                WHERE m.UsuarioId = @uid";

            using (var cmd = new SqlCommand(sqlClases, conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    clases.Add(new
                    {
                        id = (int)r["HorarioId"],
                        materia = r["Materia"].ToString(),
                        salon = r["Salon"].ToString(),
                        diaId = (byte)r["DiaId"],
                        bloqueTiempoId = (int)r["BloqueTiempoId"],
                        color = r["CodigoHex"].ToString(),
                        asistido = (bool)r["AsistidoHoy"]
                    });
                }
            }

            return Json(new { dias, horas, colores, clases });
        }

        [HttpPost]
        public IActionResult AsignarHorario([FromBody] AsignarHorarioDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                string sqlMat = @"
                    IF NOT EXISTS (SELECT 1 FROM Materias WHERE UsuarioId = @uid AND Nombre = @nom)
                        INSERT INTO Materias (UsuarioId, Nombre, SalonPorDefecto, ColorId) VALUES (@uid, @nom, @sal, @col);
                    ELSE
                        UPDATE Materias SET ColorId = @col WHERE UsuarioId = @uid AND Nombre = @nom;
                    SELECT MateriaId FROM Materias WHERE UsuarioId = @uid AND Nombre = @nom;";

                int materiaId;
                using (var cmdMat = new SqlCommand(sqlMat, conn, tx))
                {
                    cmdMat.Parameters.AddWithValue("@uid", uid.Value);
                    cmdMat.Parameters.AddWithValue("@nom", dto.Materia.Trim());
                    cmdMat.Parameters.AddWithValue("@sal", dto.Salon ?? "");
                    cmdMat.Parameters.AddWithValue("@col", dto.ColorId);
                    materiaId = (int)cmdMat.ExecuteScalar()!;
                }

                foreach (byte diaId in dto.DiasIds)
                {
                    using var cmdSp = new SqlCommand("sp_AsignarBloqueHorario", conn, tx);
                    cmdSp.CommandType = CommandType.StoredProcedure;
                    cmdSp.Parameters.AddWithValue("@MateriaId", materiaId);
                    cmdSp.Parameters.AddWithValue("@DiaId", diaId);
                    cmdSp.Parameters.AddWithValue("@BloqueTiempoId", dto.BloqueTiempoId);
                    cmdSp.Parameters.AddWithValue("@SalonAsignado", dto.Salon ?? "");
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
        public IActionResult ToggleAsistenciaHorario(int horarioId)
        {
            if (ObtenerUsuarioSesion() == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            string sql = @"
                IF EXISTS (SELECT 1 FROM AsistenciasClase WHERE HorarioId = @hid AND FechaClase = CAST(SYSDATETIME() AS DATE))
                    UPDATE AsistenciasClase SET Asistio = ~Asistio WHERE HorarioId = @hid AND FechaClase = CAST(SYSDATETIME() AS DATE);
                ELSE
                    INSERT INTO AsistenciasClase (HorarioId, FechaClase, Asistio) VALUES (@hid, CAST(SYSDATETIME() AS DATE), 1);";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@hid", horarioId);
            cmd.ExecuteNonQuery();
            return Ok();
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

        // =====================================================================
        // API MÓDULO 5: CITAS APA 7 (CON vw_ReferenciasApaFormateadas)
        // =====================================================================
        [HttpGet]
        public IActionResult ObtenerReferenciasApa()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            var lista = new List<object>();
            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

            string sql = @"
                SELECT ReferenciaId, CitaHtmlCalculada, SeleccionadaExportar 
                FROM vw_ReferenciasApaFormateadas 
                WHERE UsuarioId = @uid 
                ORDER BY ReferenciaId DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new
                {
                    id = (int)r["ReferenciaId"],
                    html = r["CitaHtmlCalculada"].ToString(),
                    seleccionada = (bool)r["SeleccionadaExportar"]
                });
            }
            return Json(lista);
        }

        [HttpPost]
        public IActionResult CrearReferenciaApa([FromBody] CrearApa3FnDto dto)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            if (dto == null || string.IsNullOrWhiteSpace(dto.ApellidoAutor) || string.IsNullOrWhiteSpace(dto.Titulo))
                return BadRequest(new { error = "El apellido del autor y el título de la obra son obligatorios.", codigo = "ERR_APA_REQUIRED" });

            short? anioNum = null;
            if (!string.IsNullOrWhiteSpace(dto.Anio))
            {
                if (!short.TryParse(dto.Anio, out short parsed) || parsed < 1500 || parsed > 2100)
                {
                    if (dto.ValidarAnioEstricto)
                        return BadRequest(new { error = "Check de validación estricta activo: El año debe ser numérico entre 1500 y 2100.", codigo = "ERR_APA_YEAR_CHECK" });
                }
                else
                {
                    anioNum = parsed;
                }
            }
            else if (dto.ValidarAnioEstricto)
            {
                return BadRequest(new { error = "Check de validación estricta activo: No se permite dejar el año vacío.", codigo = "ERR_APA_EMPTY_YEAR" });
            }

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                string sqlRef = @"
                    DECLARE @tfId INT = (SELECT TipoFuenteId FROM TiposFuenteApa WHERE Codigo = @tipo);
                    INSERT INTO ReferenciasApa (UsuarioId, TipoFuenteId, AnioPublicacion, TituloObra, FuenteEditorialUrl, IncluirFechaConsulta, IncluirRecuperadoDe, SeleccionadaExportar)
                    OUTPUT INSERTED.ReferenciaId
                    VALUES (@uid, ISNULL(@tfId, 1), @anio, @tit, @fuen, @chkFecha, @chkRecup, 1);";

                int refId;
                using (var cmd = new SqlCommand(sqlRef, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@uid", uid.Value);
                    cmd.Parameters.AddWithValue("@tipo", dto.Tipo ?? "libro");
                    cmd.Parameters.AddWithValue("@anio", (object?)anioNum ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tit", dto.Titulo.Trim());
                    cmd.Parameters.AddWithValue("@fuen", string.IsNullOrWhiteSpace(dto.Fuente) ? DBNull.Value : dto.Fuente.Trim());
                    cmd.Parameters.AddWithValue("@chkFecha", dto.IncluirFechaConsulta);
                    cmd.Parameters.AddWithValue("@chkRecup", dto.IncluirRecuperadoDe);
                    refId = (int)cmd.ExecuteScalar()!;
                }

                string inicialFormateada = string.IsNullOrWhiteSpace(dto.InicialAutor)
                    ? "A."
                    : dto.InicialAutor.Trim().Substring(0, 1).ToUpper() + ".";

                using (var cmdAutor = new SqlCommand("INSERT INTO AutoresReferenciaApa (ReferenciaId, ApellidoPaterno, InicialNombre, OrdenAutoria) VALUES (@rid, @ape, @ini, 1)", conn, tx))
                {
                    cmdAutor.Parameters.AddWithValue("@rid", refId);
                    cmdAutor.Parameters.AddWithValue("@ape", dto.ApellidoAutor.Trim());
                    cmdAutor.Parameters.AddWithValue("@ini", inicialFormateada);
                    cmdAutor.ExecuteNonQuery();
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
        public IActionResult ToggleSeleccionApa(int id)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE ReferenciasApa SET SeleccionadaExportar = ~SeleccionadaExportar WHERE ReferenciaId = @id AND UsuarioId = @uid", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
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
        public int CarreraId { get; set; }
        public byte SemestreActual { get; set; } = 1;
    }
    public class NotifDto { public string? Titulo { get; set; } public string? Mensaje { get; set; } public string? Tipo { get; set; } }
    public class GastoDto { public int CategoriaId { get; set; } public string Concepto { get; set; } = ""; public decimal Monto { get; set; } public bool Verificado { get; set; } }
    public class ConfigBilleteraDto { public decimal Presupuesto { get; set; } public bool BloquearSobregiro { get; set; } public bool SoloContarVerificados { get; set; } }
    public class KanbanTareaVm { public int id { get; set; } public string titulo { get; set; } = ""; public string materia { get; set; } = ""; public string urgencia { get; set; } = ""; public string estado { get; set; } = ""; public string? imagen { get; set; } public List<object> subtareas { get; set; } = new(); }
    public class AsignarHorarioDto { public string Materia { get; set; } = ""; public string? Salon { get; set; } public List<byte> DiasIds { get; set; } = new(); public int BloqueTiempoId { get; set; } public int ColorId { get; set; } }
    public class CrearApa3FnDto { public string Tipo { get; set; } = "libro"; public string ApellidoAutor { get; set; } = ""; public string InicialAutor { get; set; } = "A."; public string? Anio { get; set; } public string Titulo { get; set; } = ""; public string? Fuente { get; set; } public bool IncluirFechaConsulta { get; set; } public bool IncluirRecuperadoDe { get; set; } public bool ValidarAnioEstricto { get; set; } }
}