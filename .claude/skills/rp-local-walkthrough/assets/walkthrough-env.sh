#!/usr/bin/env bash
# Writes <scratch-dir>/rp.env with random throwaway credentials for a local walkthrough.
# Usage: bash walkthrough-env.sh <scratch-dir> [seed-email]
# Prints only the file path and the seed password (needed to log in); never the DB passwords.
set -euo pipefail
dir="${1:?scratch dir required (outside the repo)}"
email="${2:-valida@example.com}"
# docker-compose.yml fixes these names; they may hold the developer's own database. Never reuse or remove them.
if [ -n "$(docker ps -aq --filter name=^rutaproducto-mysql$)" ] || [ -n "$(docker volume ls -q --filter name=^rutaproducto_mysql-data$)" ]; then
  echo "rutaproducto-mysql or rutaproducto_mysql-data already exists: it may be the developer's data. Stop and ask before removing it." >&2
  exit 2
fi
mkdir -p "$dir"
db=$(openssl rand -hex 12)
root=$(openssl rand -hex 12)
seed="$(openssl rand -hex 6)Aa1!"
cat > "$dir/rp.env" <<EOF
export MYSQL_PASSWORD=$db
export MYSQL_ROOT_PASSWORD=$root
export MYSQL_PORT=3308
export ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3308;Database=rutaproducto;User=rutaproducto;Password=$db"
export SeedUsers__0__Email=$email
export SeedUsers__0__Password="$seed"
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5080
export Assistant__Provider=Fake
EOF
echo "env: $dir/rp.env"
echo "seed: $email / $seed"
