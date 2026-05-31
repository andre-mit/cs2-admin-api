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
     "Jwt": {
       "Key": "a_super_secret_key_with_at_least_32_characters",
       "Issuer": "cs2-admin",
       "Audience": "cs2-admin-client"
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

## Next Steps (Suggested Improvements)

- **Layered Architecture:** Implement the Repository and Unit of Work patterns to isolate the `DbContext` (EF Core) from the Controllers.
- **Object Mapping:** Use AutoMapper (or Mapster) to map database Models to input/output DTOs. This prevents over-posting and information leakage.
- **Validation with FluentValidation:** Create robust validators injected via dependency injection to validate requests before they hit the controller.
- **Global Exception Handling:** Replace multiple `try/catch` blocks in controllers with a Global Exception Middleware to standardize HTTP responses (400, 404, 500).
- **XML Documentation:** Add XML comments to Swagger to describe endpoint returns (`ProducesResponseType`).
