#!/bin/bash
# Standalone-compose equivalent of the schema/role slice this service owns
# in orchestration's cluster init script — same shape, one service only.
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
  CREATE SCHEMA IF NOT EXISTS orders;
  CREATE ROLE orders_role LOGIN PASSWORD '$ORDERS_DB_PASSWORD';
  ALTER ROLE orders_role SET search_path TO orders;
  GRANT USAGE, CREATE ON SCHEMA orders TO orders_role;
  GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA orders TO orders_role;
  ALTER DEFAULT PRIVILEGES IN SCHEMA orders GRANT ALL PRIVILEGES ON TABLES TO orders_role;
  ALTER DEFAULT PRIVILEGES IN SCHEMA orders GRANT ALL PRIVILEGES ON SEQUENCES TO orders_role;
EOSQL
