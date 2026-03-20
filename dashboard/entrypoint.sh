#!/bin/sh

# Write runtime config from environment variable
SERVER_URL="${TABLIX_SERVER_URL:-http://localhost:9100}"
cat > /usr/share/nginx/html/config.json << EOF
{
  "serverUrl": "${SERVER_URL}"
}
EOF

exec nginx -g 'daemon off;'
