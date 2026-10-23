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
                BEGIN
                    INSERT INTO Roles (Nombre) VALUES ('Estudiante');
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

                IF NOT EXISTS (SELECT 1 FROM Edificios)
                BEGIN
                    INSERT INTO Edificios (Nombre) VALUES 
                    ('Ingenieria'), ('Basicas'), ('Ligeros'), ('Pesados'), ('Culturales'), ('Graduados');
                END

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

        // =====================================================================
        // LOGIN Y REGISTRO (REDIRIGIENDO AL LOGIN TRAS REGISTRAR)
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
                    INSERT INTO ConfiguracionBilletera (UsuarioId, PresupuestoDiario, BloquearSobregiro, SoloContarVerificados)
                    VALUES (@uid, 200.00, 1, 0);

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

                // No inicia sesión automáticamente; redirige al Login
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
        public IActionResult HistorialBilletera() => ObtenerUsuarioSesion() == null ? RedirectToAction("Login") : View();
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
        // API: BILLETERA DIARIA + HISTORIAL POR CALENDARIO (SOLO LECTURA)
        // =====================================================================
        [HttpGet]
        public IActionResult ObtenerBilletera()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

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
            DateTime fechaCorte = DateTime.Today;

            using (var cmd = new SqlCommand("SELECT PresupuestoDiario, BloquearSobregiro, SoloContarVerificados, FechaInicioDiaActual FROM ConfiguracionBilletera WHERE UsuarioId = @uid", conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    presupuesto = (decimal)r["PresupuestoDiario"];
                    bloquear = (bool)r["BloquearSobregiro"];
                    soloVerif = (bool)r["SoloContarVerificados"];
                    fechaCorte = (DateTime)r["FechaInicioDiaActual"];
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
                WHERE m.UsuarioId = @uid 
                  AND m.FechaHora >= @corte
                  AND CAST(m.FechaHora AS DATE) = CAST(SYSDATETIME() AS DATE)
                ORDER BY m.MovimientoId DESC";

            using (var cmd = new SqlCommand(sqlGastos, conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid.Value);
                cmd.Parameters.AddWithValue("@corte", fechaCorte);
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

        [HttpGet]
        public IActionResult ObtenerGastosPorFecha(string fecha)
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            if (!DateTime.TryParse(fecha, out DateTime fechaParsed))
                fechaParsed = DateTime.Today;

            var gastos = new List<object>();
            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();

            string sql = @"
                SELECT m.MovimientoId, m.Concepto, m.Monto, m.Verificado, 
                       FORMAT(m.FechaHora, 'HH:mm') AS Hora, c.Nombre AS Categoria
                FROM MovimientosBilletera m
                INNER JOIN CategoriasGasto c ON m.CategoriaId = c.CategoriaId
                WHERE m.UsuarioId = @uid AND CAST(m.FechaHora AS DATE) = @fecha
                ORDER BY m.FechaHora DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid", uid.Value);
            cmd.Parameters.AddWithValue("@fecha", fechaParsed.Date);
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

            return Json(gastos);
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

        // Genera un nuevo ciclo/día sin borrar el historial para el calendario
        [HttpPost]
        public IActionResult GenerarNuevoDia()
        {
            int? uid = ObtenerUsuarioSesion();
            if (uid == null) return Unauthorized();

            using var conn = DbSingleton.Instancia.CrearConexion();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE ConfiguracionBilletera SET FechaInicioDiaActual = SYSDATETIME() WHERE UsuarioId = @uid", conn);
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
        // API: HORARIO (7 AM A 10 PM, HORAS/MINUTOS MANUALES, EDIFICIOS, LAB/SALÓN)
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
    public class GastoDto { public int CategoriaId { get; set; } public string Concepto { get; set; } = ""; public decimal Monto { get; set; } public bool Verificado { get; set; } }
    public class ConfigBilleteraDto { public decimal Presupuesto { get; set; } public bool BloquearSobregiro { get; set; } public bool SoloContarVerificados { get; set; } }
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