using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using System.Data;

var builder = WebApplication.CreateBuilder(args);
var cs = builder.Configuration.GetConnectionString("Tienda") ?? Environment.GetEnvironmentVariable("EMPRENDE_SQL");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => { o.Cookie.Name = "CasaNativa.Auth"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax; o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = true; o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; }; o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; }; });
builder.Services.AddAuthorization();
var keyPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath));
var app = builder.Build();
app.UseDefaultFiles(); app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization();
SqlConnection Open() => new(cs ?? throw new InvalidOperationException("Falta configurar ConnectionStrings:Tienda o EMPRENDE_SQL."));
if (!string.IsNullOrWhiteSpace(cs))
{
    try
    {
        var master = new SqlConnectionStringBuilder(cs) { InitialCatalog = "master" };
        await using var setup = new SqlConnection(master.ConnectionString);
        await setup.OpenAsync();
        var scriptPath = Path.Combine(app.Environment.ContentRootPath, "database.sql");
        var script = await File.ReadAllTextAsync(scriptPath);
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(script, @"(?im)^\s*GO\s*(?:--.*)?$"))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var command = new SqlCommand(batch, setup) { CommandTimeout = 60 };
            await command.ExecuteNonQueryAsync();
        }
        app.Logger.LogInformation("Base CasaNativa verificada e inicializada con database.sql.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "No se pudo preparar CasaNativa. Verificá que SQL Server LocalDB esté instalado e iniciado.");
    }
}
app.MapGet("/api/status", () => Results.Ok(new { database = !string.IsNullOrWhiteSpace(cs) }));
app.MapGet("/api/products", async () => {
    if (string.IsNullOrWhiteSpace(cs)) return Results.Problem("Configure la conexión SQL en appsettings.json.", statusCode: 503);
    await using var cn = Open(); await cn.OpenAsync();
    await using var cmd = new SqlCommand("SELECT p.Id,p.Nombre,p.Descripcion,p.Precio,c.Nombre AS Categoria,p.Stock,p.Activo FROM dbo.Producto p JOIN dbo.Categoria c ON c.Id=p.CategoriaId ORDER BY p.Nombre", cn);
    await using var rd = await cmd.ExecuteReaderAsync(); var rows = new List<object>();
    while (await rd.ReadAsync()) rows.Add(new { id=rd.GetInt32(0),name=rd.GetString(1),description=rd.GetString(2),price=rd.GetDecimal(3),category=rd.GetString(4),stock=rd.GetInt32(5),active=rd.GetBoolean(6),emoji="📦" });
    return Results.Ok(rows);
});
app.MapPost("/api/register", async (Register r) => {
    if (string.IsNullOrWhiteSpace(cs)) return Results.Problem("Configure SQL Server.", statusCode:503);
    if (string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Surname)||!System.Text.RegularExpressions.Regex.IsMatch(r.Email??"",@"^[^\s@]+@[^\s@]+\.[^\s@]+$")||(r.Password?.Length??0)<8) return Results.BadRequest(new {error="Completá nombre, apellido, email válido y contraseña de 8 caracteres como mínimo."});
    var salt=RandomNumberGenerator.GetBytes(16); var hash=Rfc2898DeriveBytes.Pbkdf2(r.Password!,salt,210000,HashAlgorithmName.SHA256,32);
    try { await using var cn=Open(); await cn.OpenAsync(); await using var cmd=new SqlCommand("INSERT dbo.Usuario(Nombre,Apellido,Email,Telefono,PasswordHash,PasswordSalt,Perfil,Activo) VALUES(@n,@a,@e,@t,@h,@s,'Cliente',1)",cn); cmd.Parameters.AddWithValue("@n",r.Name.Trim());cmd.Parameters.AddWithValue("@a",r.Surname.Trim());cmd.Parameters.AddWithValue("@e",r.Email!.Trim().ToLowerInvariant());cmd.Parameters.AddWithValue("@t",r.Phone?.Trim()??"");cmd.Parameters.Add("@h",SqlDbType.VarBinary,32).Value=hash;cmd.Parameters.Add("@s",SqlDbType.VarBinary,16).Value=salt; await cmd.ExecuteNonQueryAsync(); return Results.Ok(new {message="Cuenta creada. Iniciá sesión."}); }
    catch(SqlException e) when(e.Number is 2601 or 2627) { return Results.Conflict(new {error="Ya existe una cuenta con ese email."}); }
});
app.MapPost("/api/login", async (Login l, HttpContext ctx) => {
    if(string.IsNullOrWhiteSpace(cs)) return Results.Problem("Configure SQL Server.",statusCode:503);
    await using var cn=Open(); await cn.OpenAsync(); await using var cmd=new SqlCommand("SELECT Id,Nombre,Apellido,Email,PasswordHash,PasswordSalt,Perfil FROM dbo.Usuario WHERE Email=@e AND Activo=1",cn);cmd.Parameters.AddWithValue("@e",l.Email?.Trim().ToLowerInvariant()??"");await using var rd=await cmd.ExecuteReaderAsync();
    if(!await rd.ReadAsync())return Results.Unauthorized();var uid=rd.GetInt32(0);var name=rd.GetString(1);var surname=rd.GetString(2);var email=rd.GetString(3);var expected=(byte[])rd[4];var salt=(byte[])rd[5];var role=rd.GetString(6);var actual=Rfc2898DeriveBytes.Pbkdf2(l.Password??"",salt,210000,HashAlgorithmName.SHA256,32);if(!CryptographicOperations.FixedTimeEquals(actual,expected))return Results.Unauthorized();
    await rd.DisposeAsync();var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,uid.ToString()),new Claim(ClaimTypes.Name,$"{name} {surname}"),new Claim(ClaimTypes.Email,email),new Claim(ClaimTypes.Role,role)},CookieAuthenticationDefaults.AuthenticationScheme);await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(identity));return Results.Ok(new {name=$"{name} {surname}",email,role});
});
app.MapPost("/api/logout", async (HttpContext c)=>{await c.SignOutAsync();return Results.Ok();});
app.MapGet("/api/me", (ClaimsPrincipal u)=>Results.Ok(new {name=u.Identity?.Name,email=u.FindFirstValue(ClaimTypes.Email),role=u.FindFirstValue(ClaimTypes.Role)})).RequireAuthorization();
app.MapPost("/api/products", async (Product p)=>{
    if(string.IsNullOrWhiteSpace(p.Name)||p.Price<=0||p.Stock<0)return Results.BadRequest(new {error="Nombre, precio positivo y stock válido son obligatorios."});
    await using var cn=Open();await cn.OpenAsync();await using var tx=await cn.BeginTransactionAsync();
    try { await using var cat=new SqlCommand("SELECT Id FROM dbo.Categoria WHERE Nombre=@n",cn,(SqlTransaction)tx);cat.Parameters.AddWithValue("@n",p.Category);var cid=await cat.ExecuteScalarAsync();if(cid is null){await using var add=new SqlCommand("INSERT dbo.Categoria(Nombre) VALUES(@n); SELECT SCOPE_IDENTITY()",cn,(SqlTransaction)tx);add.Parameters.AddWithValue("@n",p.Category);cid=await add.ExecuteScalarAsync();}await using var cmd=new SqlCommand("INSERT dbo.Producto(Nombre,Descripcion,Precio,CategoriaId,Stock,Activo) OUTPUT INSERTED.Id VALUES(@n,@d,@p,@c,@s,1)",cn,(SqlTransaction)tx);cmd.Parameters.AddWithValue("@n",p.Name.Trim());cmd.Parameters.AddWithValue("@d",p.Description??"");cmd.Parameters.AddWithValue("@p",p.Price);cmd.Parameters.AddWithValue("@c",cid!);cmd.Parameters.AddWithValue("@s",p.Stock);var id=await cmd.ExecuteScalarAsync();await tx.CommitAsync();return Results.Ok(new {id});}catch{await tx.RollbackAsync();throw;}
}).RequireAuthorization(policy=>policy.RequireRole("Administrador"));
app.MapPut("/api/products/{id:int}", async (int id,Product p)=>{await using var cn=Open();await cn.OpenAsync();await using var tx=await cn.BeginTransactionAsync();try{await using var cat=new SqlCommand("SELECT Id FROM dbo.Categoria WHERE Nombre=@n",cn,(SqlTransaction)tx);cat.Parameters.AddWithValue("@n",p.Category);var cid=await cat.ExecuteScalarAsync();if(cid is null){await using var add=new SqlCommand("INSERT dbo.Categoria(Nombre) VALUES(@n); SELECT SCOPE_IDENTITY()",cn,(SqlTransaction)tx);add.Parameters.AddWithValue("@n",p.Category);cid=await add.ExecuteScalarAsync();}await using var cmd=new SqlCommand("UPDATE dbo.Producto SET Nombre=@n,Descripcion=@d,Precio=@p,CategoriaId=@c,Stock=@s,Activo=@a WHERE Id=@id",cn,(SqlTransaction)tx);cmd.Parameters.AddWithValue("@n",p.Name);cmd.Parameters.AddWithValue("@d",p.Description??"");cmd.Parameters.AddWithValue("@p",p.Price);cmd.Parameters.AddWithValue("@c",cid!);cmd.Parameters.AddWithValue("@s",p.Stock);cmd.Parameters.AddWithValue("@a",p.Active);cmd.Parameters.AddWithValue("@id",id);var count=await cmd.ExecuteNonQueryAsync();if(count==0){await tx.RollbackAsync();return Results.NotFound();}await tx.CommitAsync();return Results.NoContent();}catch{await tx.RollbackAsync();throw;}}).RequireAuthorization(policy=>policy.RequireRole("Administrador"));
app.MapFallbackToFile("index.html"); app.Run();
record Register(string? Name,string? Surname,string? Email,string? Phone,string? Password); record Login(string? Email,string? Password); record Product(string Name,string? Description,decimal Price,string Category,int Stock,bool Active=true);

