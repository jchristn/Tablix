#!/bin/sh

# Resolve the backend server URL (internal Docker hostname for nginx proxy)
SERVER_URL="${TABLIX_SERVER_URL:-http://localhost:9100}"
STARTUP_DELAY_SECONDS="${TABLIX_UI_STARTUP_DELAY_SECONDS:-15}"

if [ "$STARTUP_DELAY_SECONDS" -gt 0 ] 2>/dev/null; then
  echo "Delaying dashboard startup for ${STARTUP_DELAY_SECONDS}s"
  sleep "$STARTUP_DELAY_SECONDS"
fi

# Write runtime config so the browser calls back to the dashboard origin
# (nginx proxies /v1/ to the backend server)
cat > /usr/share/nginx/html/config.json << EOF
{
  "serverUrl": "",
  "displayServerUrl": "${SERVER_URL}"
}
EOF

# Write nginx config with the backend URL for proxying.
# Uses a variable for proxy_pass so nginx resolves the upstream at request
# time rather than at startup (avoids failure when DNS isn't ready yet).
cat > /etc/nginx/conf.d/default.conf << EOF
server {
    listen 9101;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    resolver 127.0.0.11 valid=10s ipv6=off;

    location /v1/ {
        set \$backend "${SERVER_URL}";
        proxy_pass \$backend\$request_uri;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_buffering off;
        proxy_connect_timeout 60s;
        proxy_send_timeout 3600s;
        proxy_read_timeout 3600s;
    }

    location / {
        try_files \$uri \$uri/ /index.html;
    }
}
EOF

exec nginx -g 'daemon off;'
