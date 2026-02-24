# ===========================
# Build stage
# ===========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY TransactionService/TransactionService.csproj TransactionService/
RUN dotnet restore TransactionService/TransactionService.csproj

# Copy the rest of the source
COPY . .

# Publish the app in Release mode
WORKDIR /src/TransactionService
RUN dotnet publish TransactionService.csproj -c Release -o /app/publish /p:UseAppHost=false

# ===========================
# Runtime stage
# ===========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# ASP.NET Core (official images) listen on port 8080 by default from .NET 8 onward.:contentReference[oaicite:1]{index=1}
EXPOSE 8080

# Configure Kestrel to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080

# Start the app
ENTRYPOINT ["dotnet", "TransactionService.dll"]