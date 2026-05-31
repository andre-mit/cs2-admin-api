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
