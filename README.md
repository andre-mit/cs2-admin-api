# CS2 Admin - API (Backend)

This is the RESTful backend for the CS2 administration panel. Built with ASP.NET Core 10, the project manages maps, servers, lobbies, and communicates via RCON with Counter-Strike 2 instances.

## Technologies Used

- **Framework:** .NET 10 (ASP.NET Core Web API)
- **Database:** MySQL (via Entity Framework Core `Pomelo.EntityFrameworkCore.MySql`)
- **Cache / PubSub:** Redis (via `StackExchange.Redis`)
- **File Storage:** AWS S3 (via `AWSSDK.S3`)
- **Game Communication:** `CoreRCON`
- **Authentication:** JWT Bearer Token

## Implemented Features

- **File Uploads (AWS S3):** Generation of Presigned URLs for direct frontend uploads or an upload proxy through the backend for maps (background and badge).
- **RCON Integration:** Basic service for remote communication with CS2 servers.
- **Management (CRUD):** Endpoints for managing Maps, Servers, Lobbies, Matches, and Teams.
- **JWT Authentication:** Endpoint protection using Bearer Tokens.

### CS2 Orchestration & FastDL Features
- **Fast Base Updating:** Bypasses heavy hash validation by running `STEAMAPPVALIDATE=0` on short-lived containers for rapid server updates.
- **Structured Plugin Ingestion:** Overlays structured plugin directories (`addons`, `cfg`, `materials`, `models`, `sound`) directly into the CS2 root using OverlayFS. Flat plugins automatically land in `counterstrikesharp/plugins/`.
- **FastDL Extraction:** Automatically detects `materials`, `models`, and `sound` folders within plugins and synchronizes them to the `FastDlBaseDir` mapped volume for fast static serving via NGINX.
- **Dynamic CVAR Injection:** Automatically detects FastDL presence and overrides `server.cfg` with `sv_downloadurl`, `sv_allowdownload 1` and `sv_allowupload 0`.
- **Programmatic Pre-execution (`pre.sh`):** Handles `chmod +x` file modes, UNIX line endings (LF), stdout/stderr rerouting (`fd/1`), and dynamically patches `gameinfo.gi` with Metamod entries using `.NET` APIs natively instead of static shell scripts.
- **Database-Backed Presets & Game Modes:** Modular configuration support mapped to dynamic `ServerPreset` entities.

## Guide: Structured Plugins, FastDL & Database Presets

### 1. Structured Plugins & Models (FastDL)
When uploading a plugin `.zip` file via the dashboard (or API), the backend handles content smartly:
- **Root-level folders** such as `addons`, `cfg`, `materials`, `models`, `sound`, and `particles` are extracted directly into a dedicated isolated directory for that plugin.
- If **`models`**, **`materials`**, **`sound`**, or **`particles`** are present, they are *automatically copied* to the FastDL base directory (`FastDlBaseDir`), preserving their folder structure.
- When a server is launched, the system checks if the server utilizes plugins containing FastDL assets. If true, the system dynamically injects `sv_downloadurl "http://<fastdl-domain>/"`, `sv_allowdownload 1`, and `sv_allowupload 0` into the server's `server.cfg`.
- **Note for Custom Models (e.g. custom characters/skins):** Just compress the folders `models/`, `materials/`, etc., into a `.zip` and upload as a Plugin. Assign this plugin to a preset or server. Clients will automatically download the models upon connecting.

### 2. Database-Backed Presets
Instead of hardcoded constants, presets are managed dynamically in the database via the `ServerPresets` table and the `/api/v1/presets` endpoints.
- **What they store:** A preset contains a name, an array of `PluginIds` to load, and a dictionary of `ServerVariables` (CVARs).
- **Creating a Preset:** You can create "Surf", "Bhop", "Retakes", or "Competitive (FACEIT-style)" presets via the Frontend Dashboard.
- **Example Surf Preset Configuration:**
  - **Plugins:** `SurfTimer`
  - **CVARs:** `sv_airaccelerate 1000`, `sv_enablebunnyhopping 1`, `sv_autobunnyhopping 1`, `sv_staminamax 0`, `CS2_GAMEALIAS casual`.
- **Applying Presets:** When deploying a dynamic server in the UI, selecting a preset instantly populates the plugins and CVARs required. The underlying container is built specifically tailored to these settings in seconds.

## Setup and Installation

1. Make sure you have the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed.
2. In the root of the `Cs2Admin.API` project, configure the `appsettings.Development.json` (or `appsettings.json`) file with the following data:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=cs2admin;User=root;Password=yourpassword;"
     },
     "Redis": {
       "ConnectionString": "localhost:6379"
     },
     "S3": {
       "AccessKey": "...",
       "SecretKey": "...",
       "BucketName": "...",
       "Region": "us-east-1"
     },
     "JwtSettings": {
       "SecretKey": "a_super_secret_key_with_at_least_32_characters"
     }
   }
   ```

3. Run the Entity Framework migrations to create the database:
   ```bash
   dotnet ef database update
   ```

4. Run the project:
   ```bash
   dotnet run
   ```
The Swagger UI will be available at `http://localhost:<port>/swagger` in the development environment.

## Use latest Docker Image

```yaml
services:
  cs2admin-api:
    image: ghcr.io/andre-mit/cs2-admin-api:latest
    ports:
      - "8080:8080"
    environment:
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION_STRING}

      - S3__ServiceUrl=${S3_URL}
      - S3__AccessKey=${S3_ACCESS_KEY}
      - S3__SecretKey=${S3_SECRET_KEY}
      - S3__BucketName=${S3_BUCKET_NAME}

      - Redis__ConnectionString=${REDIS_CONNECTION}

      - JwtSettings__SecretKey=${JWT_SECRET}

      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://0.0.0.0:8080
    restart: unless-stopped

```

## Next Steps (Suggested Improvements)

- **Layered Architecture:** Implement the Repository and Unit of Work patterns to isolate the `DbContext` (EF Core) from the Controllers.
- **Object Mapping:** Use AutoMapper (or Mapster) to map database Models to input/output DTOs. This prevents over-posting and information leakage.
- **Validation with FluentValidation:** Create robust validators injected via dependency injection to validate requests before they hit the controller.
- **Global Exception Handling:** Replace multiple `try/catch` blocks in controllers with a Global Exception Middleware to standardize HTTP responses (400, 404, 500).
- **XML Documentation:** Add XML comments to Swagger to describe endpoint returns (`ProducesResponseType`).
