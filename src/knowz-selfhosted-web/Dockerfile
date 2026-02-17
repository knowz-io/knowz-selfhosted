# Stage 1: Build
FROM node:22-alpine AS builder

WORKDIR /app

# Copy package.json only (not package-lock.json to avoid platform mismatch)
COPY package.json ./

# Install dependencies fresh for Linux Alpine platform
RUN npm install

# Copy source files (excluding node_modules via .dockerignore)
COPY . .

# Build argument for API URL (empty = relative paths, proxied by nginx)
ARG VITE_API_URL=
ENV VITE_API_URL=${VITE_API_URL}

# Build the application
RUN npm run build

# Stage 2: Serve with nginx
FROM nginx:alpine

# Default API upstream (Docker Compose: api:8080, Container Apps: override via env var)
ENV API_UPSTREAM=api:8080

# Write nginx config template (envsubst processes ${API_UPSTREAM} on container start)
COPY <<'EOF' /etc/nginx/templates/default.conf.template
server {
    listen 8080;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    # API reverse proxy to backend container
    location /api/ {
        proxy_pass http://${API_UPSTREAM}/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 300s;
    }

    # Health check proxy
    location /healthz {
        proxy_pass http://${API_UPSTREAM}/healthz;
    }

    # SPA routing - serve index.html for all non-file routes
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # Gzip compression
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml application/xml+rss text/javascript;
}
EOF

# Copy built assets from builder
COPY --from=builder /app/dist /usr/share/nginx/html

EXPOSE 8080

CMD ["nginx", "-g", "daemon off;"]
