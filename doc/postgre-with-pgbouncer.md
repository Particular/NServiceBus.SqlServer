## Postgre Sql transport with pgBounder

## Connection string changes

Running with external connection pool like pgBounder requries changes to the connection string as described in [NpgSQL compatibility guide](https://www.npgsql.org/doc/compatibility.html#pgbouncer).

## Sample deployment

```docker-compose
version: '3.8'
services:
  db:
    image: postgres
    container_name: local_pgdb
    restart: always
    command: -c 'max_connections=100'
    environment:
      POSTGRES_USER: user
      POSTGRES_PASSWORD: admin
    volumes:
      - local_pgdata:/var/lib/postgresql/data

  pgbouncer:
    image: edoburu/pgbouncer:latest
    environment:
      - DB_HOST=local_pgdb
      - DB_PORT=5432
      - DB_USER=user
      - DB_PASSWORD=admin
      - ADMIN_USERS=postgres,admin
      - AUTH_TYPE=scram-sha-256
      - MAX_CLIENT_CONN=70
    ports:
      - "5432:5432"

  pgadmin:
    image: dpage/pgadmin4
    container_name: pgadmin4_container
    restart: always
    ports:
      - "5050:80"
    environment:
      PGADMIN_DEFAULT_EMAIL: admin@admin.com
      PGADMIN_DEFAULT_PASSWORD: admin
    volumes:
      - pgadmin-data:/var/lib/pgadmin

volumes:
  local_pgdata:
  pgadmin-data:
```