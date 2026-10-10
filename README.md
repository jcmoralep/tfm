# Plataforma BMAD para producto

Plataforma interna para que personas de producto lleven una iniciativa desde la idea hasta artefactos listos para desarrollo, con un asistente que aplica el método BMAD. El alcance y las reglas están en `AGENTS.md`, `REQUISITOS.md` y `PROPUESTA-MVP.md`.

Estado actual: **paso 3 (Asistente guiado con IA falsa)** de `PROPUESTA-MVP.md` §7. Incluye la solución por capas, MySQL, Serilog, el inicio de sesión, el estilo visual base, las iniciativas y la conversación guiada con un asistente de demostración.

## Advertencia sobre Gemini (capa gratuita)

El asistente usará la **capa gratuita de Gemini**. En esa capa, Google puede usar los datos enviados para mejorar sus productos. Mientras se use la capa gratuita, trabaje **solo con datos de ejemplo o no sensibles**. Antes de usar iniciativas reales de la empresa, es necesario migrar a un plan de pago. La clave de API se configurará en una variable de entorno o en user-secrets, nunca en el repositorio.

## Asistente y datos de demostración

La conversación con el asistente (`/iniciativas/{id}/asistente`) funciona hoy con una **implementación falsa** (`FakeAssistantService`): sigue un guion fijo de preguntas y no envía nada a Gemini ni a ningún servicio externo. Aun así, la pantalla muestra siempre un aviso de modo de demostración, porque cuando se conecte Gemini (capa gratuita) Google podrá usar lo que se escriba para mejorar sus productos.

- Escriba **solo datos de ejemplo**: nada de información real de la empresa ni datos personales.
- No use iniciativas reales hasta migrar a un plan de pago de Gemini.
- La clave de API de Gemini irá en una variable de entorno o en user-secrets, nunca en el repositorio.
- Los registros (`Logs/`) no guardan el texto de los mensajes de la conversación.

## Requisitos previos

- SDK de .NET 10 (versión fijada en `global.json`).
- Docker, para la base de datos MySQL local (opcional si ya dispone de un servidor MySQL 8.4).
- Acceso a internet en la primera compilación: el build descarga el CLI independiente de Tailwind CSS v4.3.3 desde las publicaciones oficiales de GitHub, verifica su SHA-256 y lo guarda en `.tools/` (excluido de Git). No se requiere Node.js.

## Estructura

```
src/
  BmadPlatform.Domain/          Entidades y reglas; sin dependencias
  BmadPlatform.Application/     Comandos, consultas, handlers, behaviors e interfaces
  BmadPlatform.Infrastructure/  EF Core + MySQL (Pomelo), Identity, migraciones
  BmadPlatform.Web/             Blazor (Interactive Server), Tailwind, Serilog
tests/                          Pruebas por capa y reglas de dependencia
```

## Variables de entorno

Ningún secreto se guarda en el repositorio. La aplicación lee estas variables:

| Variable | Obligatoria | Descripción |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Sí | Cadena de conexión a MySQL. Ejemplo con el MySQL de Docker Compose: `Server=localhost;Port=3307;Database=bmadplatform;User=bmad;Password=<contraseña>` |
| `Database__MySqlServerVersion` | No | Versión del servidor MySQL (por defecto `8.4.0`, la imagen de Docker Compose). Debe coincidir con el servidor al que se conecta: la imagen de Docker Compose en local o la del servicio MySQL en Railway |
| `SeedUsers__0__Email` | Sí, para poder entrar | Correo del primer usuario inicial |
| `SeedUsers__0__Password` | Sí, para poder entrar | Contraseña del primer usuario inicial |
| `SeedUsers__1__Email`, `SeedUsers__1__Password`, ... | No | Usuarios adicionales, con índices consecutivos |
| `ASPNETCORE_ENVIRONMENT` | No | `Development` activa la salida de registros por consola |

Los usuarios iniciales se crean al arrancar la aplicación si todavía no existen. Si un usuario ya existe, no se modifica (su contraseña no se sobrescribe). La contraseña debe cumplir la política de ASP.NET Core Identity: al menos 6 caracteres, con una mayúscula, una minúscula, un dígito y un carácter no alfanumérico. Si no la cumple, el usuario no se crea y el motivo queda en el registro.

En desarrollo también puede usar user-secrets en lugar de variables de entorno:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3307;Database=bmadplatform;User=bmad;Password=<contraseña>" --project src/BmadPlatform.Web
dotnet user-secrets set "SeedUsers:0:Email" "<correo>" --project src/BmadPlatform.Web
dotnet user-secrets set "SeedUsers:0:Password" "<contraseña>" --project src/BmadPlatform.Web
```

Para Docker Compose se usan además estas variables, que puede definir en un archivo `.env` en la raíz (excluido de Git):

| Variable | Descripción |
|---|---|
| `MYSQL_PASSWORD` | Contraseña del usuario de la aplicación (obligatoria) |
| `MYSQL_ROOT_PASSWORD` | Contraseña del usuario `root` (obligatoria) |
| `MYSQL_USER` | Usuario de la aplicación (por defecto `bmad`) |
| `MYSQL_PORT` | Puerto local (por defecto `3307`, para no chocar con otro MySQL en `3306`; use la misma cifra en la cadena de conexión) |

## Base de datos

1. Inicie MySQL 8.4:

   ```bash
   docker compose up -d
   ```

2. Aplique las migraciones. Este comando lee **solo** la variable de entorno `ConnectionStrings__DefaultConnection` (no lee user-secrets):

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/BmadPlatform.Infrastructure --startup-project src/BmadPlatform.Infrastructure
   ```

   En PowerShell, defina antes la variable con `$env:ConnectionStrings__DefaultConnection = "<cadena>"`; en Bash, con `export ConnectionStrings__DefaultConnection="<cadena>"`.

3. Para crear una migración nueva (no necesita conexión, porque la versión de MySQL es explícita):

   ```bash
   dotnet ef migrations add <Nombre> --project src/BmadPlatform.Infrastructure --startup-project src/BmadPlatform.Infrastructure --output-dir Persistence/Migrations
   ```

Para detener la base de datos: `docker compose down` (agregue `-v` para borrar también los datos).

### Base de datos en Railway

La base de datos de producción es la plantilla MySQL de Railway. Se conecta por configuración, sin cambiar código ni migraciones:

1. Agregue el servicio MySQL desde la plantilla de Railway.
2. En el servicio de la aplicación, defina `ConnectionStrings__DefaultConnection` con variables de referencia. El nombre del servicio en la referencia debe coincidir con el del servicio MySQL (aquí, `MySQL`):

   ```
   Server=${{MySQL.MYSQLHOST}};Port=${{MySQL.MYSQLPORT}};Database=${{MySQL.MYSQLDATABASE}};User=${{MySQL.MYSQLUSER}};Password=${{MySQL.MYSQLPASSWORD}}
   ```

3. Defina `Database__MySqlServerVersion` con la versión de MySQL que ejecute el servicio. Debe coincidir con la imagen.
4. Las migraciones se aplicarán como comando previo al despliegue (pendiente de implementar; ver `AGENTS.md` §6).

`MYSQL_URL` de Railway tiene formato de URL y no sirve directamente como cadena de conexión de .NET.

## Ejecutar

```bash
dotnet run --project src/BmadPlatform.Web
```

La aplicación queda disponible en `http://localhost:5080` (perfil `http`) o `https://localhost:7080` (perfil `https`). La página de inicio requiere sesión; sin ella, redirige a `/login`.

## Pruebas

```bash
dotnet test
```

Las pruebas no necesitan base de datos. Cubren el orden del pipeline de MediatR (registro, validación, handler), que los registros no incluyan el contenido de los comandos, la validación del inicio de sesión, la sincronía entre el modelo de EF Core y las migraciones, la protección contra redirecciones externas y las reglas de dependencia entre capas.

## Registros

Serilog escribe archivos de texto en `Logs/log-AAAAMMDD.log`, relativos al directorio de trabajo de la aplicación, con rotación diaria y un máximo de 7 archivos. En desarrollo, los registros también se muestran en la consola. Cada línea incluye un `CorrelationId` por petición HTTP o por circuito de Blazor. Nunca se registra el contenido de los comandos, solo su nombre y su duración.

## Estilos

Tailwind CSS se compila durante el build de .NET (`src/BmadPlatform.Web/Tailwind.targets`): analiza los archivos `.razor` y genera `wwwroot/css/app.css`, que es un resultado de compilación y no se versiona. Todos los colores, tipografías, radios y sombras están en un único archivo, `src/BmadPlatform.Web/Styles/theme.css`, con una paleta neutra provisional. Cuando se agregue `DESIGN.md`, basta con ajustar ese archivo.

## Versiones de dependencias relevantes

- **EF Core 9.0.x sobre .NET 10:** `Pomelo.EntityFrameworkCore.MySql` 9.0.0 es la versión estable más reciente y solo admite EF Core 9. Cuando Pomelo publique una versión estable para EF Core 10, se actualizarán juntos Pomelo, EF Core e Identity.EntityFrameworkCore (`Directory.Packages.props`).
- **MediatR 12.5.0:** es la última versión con licencia Apache-2.0. Desde la 13.0.0 la licencia es comercial.
