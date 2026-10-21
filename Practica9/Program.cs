using Practica9.Data;
var builder = WebApplication.CreateBuilder(args);

// Inicializar Singleton apuntando a GarAngelitoToolsProDB
DbSingleton.Inicializar(builder.Configuration.GetConnectionString("GarAngelitoConnection")!);

builder.Services.AddControllersWithViews();

// Habilitar sesiones en memoria para el Login de Estudiantes
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession(); // Activar sesión antes de Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();