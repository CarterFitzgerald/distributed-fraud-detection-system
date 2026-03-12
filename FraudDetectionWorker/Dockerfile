FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore FraudDetectionWorker/FraudDetectionWorker.csproj
RUN dotnet publish FraudDetectionWorker/FraudDetectionWorker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
COPY FraudModelTrainer.OptionA/Model/model.zip /app/Model/model.zip
ENTRYPOINT ["dotnet", "FraudDetectionWorker.dll"]