#!/bin/bash

export PG_HOST=postgres
export PG_DB=boxtotabletop
export POSTGRES_USER=postgres
export POSTGRES_PASSWORD=postgres
export AUTH0_DOMAIN=dev-6duts2ta.us.auth0.com
export AUTH0_AUDIENCE=http://localhost:5000
export AUTH0_CLIENTID=znj5EvPfoPrzk7B7JF2hGmws8mdXVXqJ
export CORS_ORIGINS=http://localhost:8090

dotnet watch run
# -- --postgreshost postgres --authdomain "dev-6duts2ta.us.auth0.com" --authaudience "http://localhost:5000" --authclientid "znj5EvPfoPrzk7B7JF2hGmws8mdXVXqJ"