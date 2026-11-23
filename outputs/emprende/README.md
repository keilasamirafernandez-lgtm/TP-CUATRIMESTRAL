# Emprende — tienda y gestión de pedidos

Aplicación web académica para cubrir las cuatro etapas del TPC de e-commerce. El front corre sin servicios externos y persiste los datos de demostración en `localStorage`.

## Ejecutar

Requiere .NET 10 SDK. Desde esta carpeta: `dotnet run`. Abrir la URL que indica la consola.

## Recorrido de demostración

- Tienda: buscar y filtrar el catálogo, añadir artículos, actualizar cantidades y confirmar el checkout.
- Cuenta: cambiar entre perfil cliente y gestión desde el selector superior. La cuenta cliente de demostración usa el email `cliente@demo.com`; gestión se habilita con el selector.
- Administración: productos, categorías, medios de pago y entrega se pueden crear, editar, activar o desactivar. Pedidos se filtran, consultan, actualizan y anotan.
- Mis compras: consultar pedidos del cliente de demostración.
- Reinicio: `localStorage.removeItem('emprende-v1')` en la consola del navegador.

## Etapas entregadas

1. Dominio documentado, navegación y lectura de catálogo persistido.
2. ABM de entidades de configuración y catálogo, filtros y validaciones.
3. Carrito, checkout con dirección y pago/entrega, confirmación, historial y ciclo de pedido.
4. Perfiles de interfaz, validaciones, historial de estados, filtros y presentación adaptable.

## Alcance de la demo

Es una demo autocontenida: el perfil se selecciona en la interfaz, no hay autenticación real ni almacenamiento centralizado. Antes de publicar con datos reales, conectar backend y base de datos, implementar Identity/roles y proteger operaciones en el servidor. Los precios de pedido se guardan como copia histórica.

## Modelo de dominio

`Cliente 1—N Dirección`; `Cliente 1—N Pedido`; `Pedido 1—N ÍtemPedido`; `Producto N—1 Categoría`; `Pedido N—1 FormaPago`; `Pedido N—1 FormaEntrega`; `Pedido 1—N HistorialEstado`.
