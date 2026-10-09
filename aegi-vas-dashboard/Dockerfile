# One container: the React build is published inside the ASP.NET Core API, which serves both.
# Build from this folder:  docker build -t aegi-vas-dashboard .

# 1. React assets. Vite writes into ../backend/src/Aegi.Vas.Dashboard.Api/wwwroot (see vite.config.js).
FROM node:22-alpine AS web
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2. API, with the React build copied into its wwwroot before publish.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/backend
COPY backend/src/Aegi.Vas.Dashboard.Api/Aegi.Vas.Dashboard.Api.csproj src/Aegi.Vas.Dashboard.Api/
RUN dotnet restore src/Aegi.Vas.Dashboard.Api/Aegi.Vas.Dashboard.Api.csproj
COPY backend/src/ src/
COPY --from=web /src/backend/src/Aegi.Vas.Dashboard.Api/wwwroot/ src/Aegi.Vas.Dashboard.Api/wwwroot/
RUN dotnet publish src/Aegi.Vas.Dashboard.Api/Aegi.Vas.Dashboard.Api.csproj -c Release -o /app --no-restore

# 3. Runtime. The default (Ubuntu) image is used on purpose: Microsoft.Data.SqlClient needs the ICU
#    globalization libraries, which the Alpine and chiseled images leave out.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=api /app ./
# App Service forwards HTTPS traffic to this port; set WEBSITES_PORT=8080 on the Web App.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Aegi.Vas.Dashboard.Api.dll"]
