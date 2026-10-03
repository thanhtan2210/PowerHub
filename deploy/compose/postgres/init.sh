#!/bin/sh
# Runs once when the local PostgreSQL volume is first created.
# Each service gets its own database, a migrator role that owns the schema, and a
# runtime role limited to data access (NFR-SEC-020). Add new services to the list.
set -eu

create_service_database() {
  service="$1"
  migrator_password="$2"
  runtime_password="$3"

  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<SQL
CREATE ROLE ${service}_migrator LOGIN PASSWORD '${migrator_password}';
CREATE ROLE ${service}_svc LOGIN PASSWORD '${runtime_password}';
CREATE DATABASE ${service}_db OWNER ${service}_migrator;
REVOKE ALL ON DATABASE ${service}_db FROM PUBLIC;
GRANT CONNECT ON DATABASE ${service}_db TO ${service}_svc;
\connect ${service}_db
ALTER DEFAULT PRIVILEGES FOR ROLE ${service}_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ${service}_svc;
ALTER DEFAULT PRIVILEGES FOR ROLE ${service}_migrator IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO ${service}_svc;
SQL
}

create_service_database identity "$IDENTITY_MIGRATOR_PASSWORD" "$IDENTITY_SVC_PASSWORD"
