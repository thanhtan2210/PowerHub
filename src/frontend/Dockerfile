# syntax=docker/dockerfile:1
# Build context: src/frontend.
FROM node:24.21.0-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# Unprivileged variant: listens on 8080 and runs as a non-root user.
FROM nginxinc/nginx-unprivileged:1.30-alpine3.24
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
