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

# Substitute the backend URL into the nginx config for proxying
export TABLIX_PROXY_PASS="${SERVER_URL}"
envsubst '${TABLIX_PROXY_PASS}' < /etc/nginx/conf.d/default.conf.template > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'
