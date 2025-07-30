# Scheduling Application

A full-stack web application for scheduling with authentication and production planning features.

## Architecture

- **Backend**: .NET 9 Web API with JWT authentication and Google OAuth
- **Frontend**: React with TypeScript and Tailwind CSS
- **Database**: SQLite with Entity Framework Core
- **Authentication**: JWT tokens with Google OAuth integration

## Environment Configuration

This application uses a robust environment-specific configuration system:

### Configuration Layers
1. **Base Configuration**: `appsettings.json` (committed, no secrets)
2. **Environment-Specific**: `appsettings.{Environment}.json` (committed, placeholders only)
3. **Secrets**: User Secrets (development) / Environment Variables (production/staging)

### Supported Environments
- **Development**: Uses user secrets for sensitive data, SQLite database `auth-dev.db`
- **Staging**: Uses environment variables, SQLite database `staging.db`
- **Production**: Uses environment variables, SQLite database `production.db`
- **Testing**: Uses in-memory database for unit tests

## Quick Start

### 1. Backend Setup

```bash
cd Login-and-Registration-Backend-.NET-

# Install dependencies
dotnet restore

# Configure user secrets for development (required)
dotnet user-secrets set "Jwt:Key" "your-super-secure-jwt-key-at-least-32-characters-long"
dotnet user-secrets set "Authentication:Google:ClientId" "your-google-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-google-client-secret"

# Run the application
dotnet run
```

### 2. Frontend Setup

```bash
cd Login-and-Registration-Frontend-React-

# Install dependencies
npm install

# Start development server
npm run dev
```

## Configuration Management

### Development
- Secrets are managed via .NET User Secrets
- See `SECRETS_MANAGEMENT.md` for detailed setup instructions

### Production/Staging
- Configuration via environment variables
- See `SECRETS_MANAGEMENT.md` for required environment variables

### Testing Configuration
Run the configuration test script to validate your setup:

```powershell
.\test-config.ps1
```

## Project Structure

```
├── Login-and-Registration-Backend-.NET-/    # .NET Web API
│   ├── Controllers/                         # API controllers
│   ├── Data/                               # Entity Framework context
│   ├── Models/                             # Data models
│   ├── Services/                           # Business logic services
│   ├── Configuration/                      # Configuration helpers
│   └── appsettings.{Environment}.json      # Environment-specific config
└── Login-and-Registration-Frontend-React-/ # React frontend
    └── src/
        ├── components/                     # React components
        ├── pages/                          # Page components
        ├── contexts/                       # React contexts
        └── services/                       # API services
```

## Security Features

- JWT token authentication
- Google OAuth integration
- Environment-specific configuration
- Secure secret management
- CORS protection
- HTTPS enforcement in production

## Development

### Prerequisites
- .NET 9 SDK
- Node.js 18+
- Git
