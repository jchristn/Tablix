#!/bin/sh

# Resolve the backend server URL (internal Docker hostname for nginx proxy)
SERVER_URL="${TABLIX_SERVER_URL:-http://localhost:9100}"

# Write runtime config so the browser calls back to the dashboard origin
# (nginx proxies /v1/ to the backend server)
cat > /usr/share/nginx/html/config.json << EOF
{
  "serverUrl": ""
}
EOF

# Write nginx config with the backend URL for proxying
cat > /etc/nginx/conf.d/default.conf << EOF
server {
    listen 9101;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    location /v1/ {
        proxy_pass ${SERVER_URL}/v1/;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
    }

    location / {
        try_files \$uri \$uri/ /index.html;
    }
}
EOF

exec nginx -g 'daemon off;'
