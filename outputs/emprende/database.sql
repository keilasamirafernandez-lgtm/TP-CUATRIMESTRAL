/* Casa Nativa - base inicial para SQL Server / LocalDB. */

IF DB_ID(N'CasaNativa') IS NULL
    CREATE DATABASE CasaNativa;
GO

USE CasaNativa;
GO

IF OBJECT_ID('dbo.Categoria','U') IS NULL
CREATE TABLE dbo.Categoria
(
    Id INT IDENTITY PRIMARY KEY,
    Nombre NVARCHAR(80) NOT NULL UNIQUE,
    Activa BIT NOT NULL DEFAULT 1
);

IF OBJECT_ID('dbo.Producto','U') IS NULL
CREATE TABLE dbo.Producto
(
    Id INT IDENTITY PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,
    Descripcion NVARCHAR(500) NOT NULL,
    Precio DECIMAL(12,2) NOT NULL CHECK(Precio > 0),
    CategoriaId INT NOT NULL REFERENCES dbo.Categoria(Id),
    Stock INT NOT NULL CHECK(Stock >= 0),
    Activo BIT NOT NULL DEFAULT 1
);

IF OBJECT_ID('dbo.Usuario','U') IS NULL
CREATE TABLE dbo.Usuario
(
    Id INT IDENTITY PRIMARY KEY,
    Nombre NVARCHAR(80) NOT NULL,
    Apellido NVARCHAR(80) NOT NULL,
    Email NVARCHAR(254) NOT NULL UNIQUE,
    Telefono NVARCHAR(30) NOT NULL,
    PasswordHash VARBINARY(32) NOT NULL,
    PasswordSalt VARBINARY(16) NOT NULL,
    Perfil NVARCHAR(30) NOT NULL CHECK(Perfil IN('Cliente','Administrador')),
    Activo BIT NOT NULL DEFAULT 1
);

IF OBJECT_ID('dbo.Direccion','U') IS NULL
CREATE TABLE dbo.Direccion
(
    Id INT IDENTITY PRIMARY KEY,
    ClienteId INT NOT NULL REFERENCES dbo.Usuario(Id),
    Calle NVARCHAR(120) NOT NULL,
    Numero NVARCHAR(20) NOT NULL,
    Localidad NVARCHAR(100) NOT NULL,
    CodigoPostal NVARCHAR(20) NOT NULL,
    Observaciones NVARCHAR(300) NULL,
    Activa BIT NOT NULL DEFAULT 1
);

IF OBJECT_ID('dbo.FormaPago','U') IS NULL
CREATE TABLE dbo.FormaPago
(
    Id INT IDENTITY PRIMARY KEY,
    Nombre NVARCHAR(60) NOT NULL UNIQUE,
    Activa BIT NOT NULL DEFAULT 1
);

IF OBJECT_ID('dbo.FormaEntrega','U') IS NULL
CREATE TABLE dbo.FormaEntrega
(
    Id INT IDENTITY PRIMARY KEY,
    Nombre NVARCHAR(60) NOT NULL UNIQUE,
    RequiereDireccion BIT NOT NULL,
    Activa BIT NOT NULL DEFAULT 1
);

IF OBJECT_ID('dbo.Pedido','U') IS NULL
CREATE TABLE dbo.Pedido
(
    Id BIGINT IDENTITY PRIMARY KEY,
    ClienteId INT NOT NULL REFERENCES dbo.Usuario(Id),
    Fecha DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    FormaPagoId INT NOT NULL REFERENCES dbo.FormaPago(Id),
    FormaEntregaId INT NOT NULL REFERENCES dbo.FormaEntrega(Id),
    DireccionId INT NULL REFERENCES dbo.Direccion(Id),
    Estado NVARCHAR(30) NOT NULL,
    Total DECIMAL(12,2) NOT NULL CHECK(Total >= 0),
    CHECK(Estado IN
        ('Pendiente','Pagado','En preparación','Enviado','Entregado','Cancelado'))
);

IF OBJECT_ID('dbo.ItemPedido','U') IS NULL
CREATE TABLE dbo.ItemPedido
(
    Id BIGINT IDENTITY PRIMARY KEY,
    PedidoId BIGINT NOT NULL REFERENCES dbo.Pedido(Id),
    ProductoId INT NOT NULL REFERENCES dbo.Producto(Id),
    NombreHistorico NVARCHAR(100) NOT NULL,
    PrecioUnitario DECIMAL(12,2) NOT NULL,
    Cantidad INT NOT NULL CHECK(Cantidad > 0)
);

IF OBJECT_ID('dbo.HistorialPedido','U') IS NULL
CREATE TABLE dbo.HistorialPedido
(
    Id BIGINT IDENTITY PRIMARY KEY,
    PedidoId BIGINT NOT NULL REFERENCES dbo.Pedido(Id),
    Estado NVARCHAR(30) NOT NULL,
    Fecha DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Observacion NVARCHAR(500) NULL
);

IF NOT EXISTS(SELECT 1 FROM dbo.Categoria)
    INSERT dbo.Categoria(Nombre)
    VALUES(N'Hogar'),(N'Bienestar'),(N'Accesorios');

IF NOT EXISTS(SELECT 1 FROM dbo.FormaPago)
    INSERT dbo.FormaPago(Nombre)
    VALUES(N'Transferencia'),(N'Efectivo'),(N'Tarjeta (simulada)');

IF NOT EXISTS(SELECT 1 FROM dbo.FormaEntrega)
    INSERT dbo.FormaEntrega(Nombre,RequiereDireccion)
    VALUES(N'Retiro en el local',0),(N'Envío a domicilio',1);

IF NOT EXISTS(SELECT 1 FROM dbo.Producto)
BEGIN
    INSERT dbo.Producto
        (Nombre,Descripcion,Precio,CategoriaId,Stock,Activo)
    SELECT v.Nombre,v.Descripcion,v.Precio,c.Id,v.Stock,1
    FROM
    (
        VALUES
        (N'Vela de soja',N'Cera vegetal y aroma suave para tus momentos de pausa.',5800,N'Hogar',18),
        (N'Taza de cerámica',N'Pieza artesanal esmaltada, hecha en pequeños lotes.',9200,N'Hogar',12),
        (N'Jabón botánico',N'Aceites vegetales y hierbas, amable con tu piel.',3600,N'Bienestar',25),
        (N'Bolso tejido',N'Liviano, resistente y tejido a mano en algodón.',14500,N'Accesorios',7),
        (N'Difusor natural',N'Un aroma fresco para ambientar cualquier rincón.',7800,N'Hogar',14),
        (N'Cuaderno de lino',N'Tapa textil y hojas de papel ahuesado.',6400,N'Accesorios',0),
        (N'Aceite corporal',N'Mezcla nutritiva de almendras y caléndula.',8100,N'Bienestar',9),
        (N'Maceta de barro',N'Barro cocido con acabado natural.',5200,N'Hogar',16)
    ) v(Nombre,Descripcion,Precio,Categoria,Stock)
    JOIN dbo.Categoria c ON c.Nombre = v.Categoria;
END;
GO