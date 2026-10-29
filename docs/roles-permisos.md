# Roles y accesos: activación y alcance

## Activación

1. Ejecutar `scripts/migrate-roles-permisos.sql` en la base de datos de la aplicación antes de iniciar el backend actualizado. La migración conserva usuarios y asignaciones, incorpora Permiso y RolPermiso y añade alcance, estado y versión a Rol. No se ha ejecutado desde esta tarea.
2. Compilar y reiniciar el backend, y actualizar el frontend conjuntamente. El backend nuevo requiere las columnas y tablas nuevas incluso durante el inicio de sesión.
3. Iniciar sesión con un administrador que tenga sede asignada. Abrir **Roles y accesos**, junto a Usuarios.
4. Crear un rol o copiar una plantilla, marcar consultas y acciones y guardar. En **Asignar al personal**, seleccionar una cuenta existente y sus roles. Las acciones seleccionadas requieren consulta de la sección.

## Comportamiento implementado

- Roles locales independientes por sede, edición con control de versión y desactivación únicamente cuando no tienen asignaciones.
- Matriz de acciones para las funciones existentes: consulta, creación, edición, desactivación, recepción y reportes. Asignar comodidades a habitaciones sigue siendo parte de editar la habitación. Crear comodidades modifica el catálogo compartido, como antes.
- Permisos efectivos como unión de roles activos y compatibles con la sede. Backend consulta la BD por solicitud; no hay caché de permisos. Cambiar un permiso se refleja en la siguiente petición. Cambiar la categoría de rol del token puede exigir iniciar sesión de nuevo.
- Navegación, rutas y botones consultan los permisos del servidor. Frontend actualiza accesos al recuperar foco, cada minuto y ante denegación de permisos.
- Catálogos públicos de sedes y habitaciones disponibles conservan su función pública; ocultar una sección administrativa no convierte esos catálogos en información privada.
- Solo personal de la propia sede puede recibir roles. Clientes, la propia cuenta y cuentas administrativas protegidas quedan excluidos de reasignaciones.
- Administrador y Superadministrador permanecen protegidos. No se modifican ni desactivan sus cuentas desde gestión de personal, salvo edición de contacto de la propia cuenta. Esto también evita dejar la sede sin administrador desde este módulo.
- Las plantillas globales se consultan y copian; no se editan desde esta pantalla. Un superadministrador sin sede puede consultar las plantillas, pero no administrar personal local sin contexto de sede.
- Permisos de administración de roles, asignación y gestión central no son delegables. Los cambios quedan registrados en Auditoria con antes/después.
- Usuarios permite crear cuentas de personal en la sede del administrador y asignar roles o cambiar los roles de una cuenta existente. El alta exige permisos de edición de usuarios y asignación de roles. La contraseña inicial se establece en el formulario; no existe todavía envío de invitación ni cambio obligatorio al primer ingreso.

## Verificación

Suite backend: 279 pruebas aprobadas y 7 pruebas de integración SQL Server omitidas por configuración. Incluye controles por acción, revocación en la siguiente petición, asignación múltiple, aislamiento, rechazo de autoasignación y concurrencia optimista. Frontend: compilación y ESLint de archivos afectados correctos. Vite advierte del tamaño de un paquete, sin impedir la compilación.

Pendiente en entorno SQL: ejecutar la migración y verificar su segunda ejecución; probar dos administradores editando simultáneamente; comprobar con cuentas reales de distintas sedes el ciclo crear/asignar/revocar. Las pruebas InMemory no verifican aislamiento transaccional SQL. No se realizó una prueba visual autenticada en navegador.

No revertir únicamente el código después de asignar roles personalizados: la versión anterior no reconoce esos roles. Coordinar cualquier reversión con la restauración de asignaciones compatibles.
