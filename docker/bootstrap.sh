#!/usr/bin/env bash
set -e

echo "===> Bootstrap starting"

MODEL_PATH="/workspace/FraudModelTrainer.OptionA/Model/model.zip"
GENERATOR_PATH="/workspace/generate_training_data.py"

echo "===> Cleaning Windows build artifacts"
find /workspace -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +

echo "===> Restoring TransactionService"
dotnet restore /workspace/TransactionService/TransactionService.csproj

echo "===> Restoring FraudDetectionWorker"
dotnet restore /workspace/FraudDetectionWorker/FraudDetectionWorker.csproj

echo "===> Applying TransactionService database migrations"
dotnet ef database update \
  --project /workspace/TransactionService/TransactionService.csproj \
  --startup-project /workspace/TransactionService/TransactionService.csproj

echo "===> Applying FraudDetectionWorker database migrations"
dotnet ef database update \
  --project /workspace/FraudDetectionWorker/FraudDetectionWorker.csproj \
  --startup-project /workspace/FraudDetectionWorker/FraudDetectionWorker.csproj

if [ -f "$MODEL_PATH" ]; then
  echo "===> Model already exists at $MODEL_PATH"
else
  echo "===> Model not found. Generating training data and training model."

  if [ ! -f "$GENERATOR_PATH" ]; then
    echo "ERROR: Generator not found at $GENERATOR_PATH"
    exit 1
  fi

  echo "===> Running fraud data generator"
  python3 "$GENERATOR_PATH"

  echo "===> Restoring trainer"
  dotnet restore /workspace/FraudModelTrainer.OptionA/FraudModelTrainer.OptionA.csproj

  echo "===> Training OptionA model"
  dotnet run --project /workspace/FraudModelTrainer.OptionA/FraudModelTrainer.OptionA.csproj
fi

echo "===> Bootstrap completed successfully"