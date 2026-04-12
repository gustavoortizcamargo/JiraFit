# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["JiraFit.API/JiraFit.API.csproj", "JiraFit.API/"]
COPY ["JiraFit.Application/JiraFit.Application.csproj", "JiraFit.Application/"]
COPY ["JiraFit.Domain/JiraFit.Domain.csproj", "JiraFit.Domain/"]
COPY ["JiraFit.Infrastructure/JiraFit.Infrastructure.csproj", "JiraFit.Infrastructure/"]

RUN dotnet restore "JiraFit.API/JiraFit.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/JiraFit.API"
RUN dotnet build "JiraFit.API.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "JiraFit.API.csproj" -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Railway passes $PORT environment variable, ASP.NET Core 8 reads it natively via ASPNETCORE_HTTP_PORTS
ENV ASPNETCORE_HTTP_PORTS=8080
# If Railway uses PORT, our Program.cs intercepts it, otherwise we leave ASPNETCORE_HTTP_PORTS fallback.

ENTRYPOINT ["dotnet", "JiraFit.API.dll"]
