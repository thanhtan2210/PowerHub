# syntax=docker/dockerfile:1
# Build context: src/frontend.
# The build stage runs on the builder's own architecture: the output is static files.
FROM --platform=$BUILDPLATFORM node:24.21.0-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# Unprivileged variant: listens on 8080 and runs as a non-root user.
FROM nginxinc/nginx-unprivileged:1.29-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
