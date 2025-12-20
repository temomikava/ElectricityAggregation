# Electricity Aggregation

A .NET application for aggregating electricity consumption data from multiple providers.

## Prerequisites

- Docker and Docker Compose

## Running the Application

Build and start all services:

```bash
docker-compose up --build
```

The API will be available at `http://localhost:8080`

Before testing the application, verify that all services are running correctly and the database connection is established.

Invoke the health check endpoint:

http://localhost:8080/api/Health


If the endpoint returns a healthy status, the application and database are properly connected.

If the health check fails, verify the PostgreSQL credentials and connection settings in the docker-compose.yml file.
Demo credentials are included in `docker-compose.yml` for ease of setup


## Architecture

- **API**: REST API for electricity data operations
- **Worker**: Background service that applies database migrations on startup and processes data
- **PostgreSQL**: Database for storing electricity consumption data

## Notes

- The Worker service handles database migrations automatically on startup, ensuring the database schema is up to date before the application begins processing data
- Demo credentials are included in `docker-compose.yml` for ease of setup. In production, use environment variables or secrets management.
