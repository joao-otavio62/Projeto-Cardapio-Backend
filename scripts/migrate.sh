#!/usr/bin/env bash
# Uso: ./scripts/migrate.sh add NomeDaMigration | ./scripts/migrate.sh update
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet tool restore >/dev/null 2>&1 || dotnet tool install --global dotnet-ef
ARGS="-p src/PizzaFlow.Infrastructure -s src/PizzaFlow.API"
case "${1:-}" in
  add)    dotnet ef migrations add "${2:?informe o nome}" $ARGS -o Persistence/Migrations ;;
  update) dotnet ef database update $ARGS ;;
  *) echo "uso: $0 add <Nome> | update"; exit 1 ;;
esac
