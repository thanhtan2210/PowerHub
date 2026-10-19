#!/bin/sh
# Creates the database and roles of every service on any PostgreSQL server.
# Each service gets its own database, a migrator role that owns the schema, and a
# runtime role limited to data access (NFR-SEC-020). Add new services to the list.
#
# Local Compose runs this once when the PostgreSQL volume is first created.
# Against another server, supply an administrative connection through the standard
# libpq variables (PGHOST, PGUSER, PGPASSWORD, PGSSLMODE) and the role passwords:
#   IDENTITY_MIGRATOR_PASSWORD, IDENTITY_SVC_PASSWORD,
#   DEVICE_MIGRATOR_PASSWORD, DEVICE_SVC_PASSWORD
# It is not idempotent: it stops at the first object that already exists.
set -eu

PGUSER="${PGUSER:-${POSTGRES_USER:-postgres}}"
PGDATABASE="${PGDATABASE:-postgres}"
export PGUSER PGDATABASE

create_service_database() {
  service="$1"
  migrator_password="$2"
  runtime_password="$3"

  psql -v ON_ERROR_STOP=1 -v migrator_password="$migrator_password" -v runtime_password="$runtime_password" <<SQL
CREATE ROLE ${service}_migrator LOGIN PASSWORD :'migrator_password';
CREATE ROLE ${service}_svc LOGIN PASSWORD :'runtime_password';
-- A non-superuser administrator must be a member of the role it hands a database to.
GRANT ${service}_migrator TO CURRENT_USER;
CREATE DATABASE ${service}_db OWNER ${service}_migrator;
REVOKE ALL ON DATABASE ${service}_db FROM PUBLIC;
GRANT CONNECT ON DATABASE ${service}_db TO ${service}_svc;
SQL

  # Default privileges are set by the migrator itself, so tables it creates later are
  # readable and writable by the runtime role without any administrator involvement.
  PGUSER="${service}_migrator" PGPASSWORD="$migrator_password" PGDATABASE="${service}_db" \
    psql -v ON_ERROR_STOP=1 <<SQL
GRANT USAGE ON SCHEMA public TO ${service}_svc;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ${service}_svc;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO ${service}_svc;
SQL
}

create_service_database identity "$IDENTITY_MIGRATOR_PASSWORD" "$IDENTITY_SVC_PASSWORD"
create_service_database device "$DEVICE_MIGRATOR_PASSWORD" "$DEVICE_SVC_PASSWORD"
