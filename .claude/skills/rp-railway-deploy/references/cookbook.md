# Railway cookbook (project `ruta-de-producto`)

Setup used on 2026-10-18 (free trial credit, no card). `RW` = `"$PWD/.tools/railway/node_modules/.bin/railway"`, always with `export MSYS_NO_PATHCONV=1`.

| Thing | Value |
|---|---|
| Project / environment | `ruta-de-producto` (`9bc31d50-80ad-47b3-ae9b-84ef349df186`) / `production` (`14e4c36a-fbbb-4700-b0cd-09ebfd063135`) |
| DB service | `MySQL` (`5dd02454-70fe-4d2c-bb74-8f173ca43685`), image `mysql:8.4`, volume `mysql-volume` at `/var/lib/mysql` (500 MB) |
| Web service | `rutaproducto-web` (`d8c3ddbd-75f7-4a3a-94dc-c64dde218ac4`), Dockerfile build |
| Public URL | `https://rutaproducto-web-production.up.railway.app` (target port 8080) |
| Login state | `C:\Users\<user>\.railway\config.json` (outside the repo, unavoidable); `railway logout` removes it |

## First-time setup (already done; for rebuilding from scratch)

```bash
$RW login                                   # the user types: ! railway login (browser)
$RW init --name ruta-de-producto --json
$RW add --image mysql:8.4 --service MySQL \
  --variables "MYSQL_ROOT_PASSWORD=$(openssl rand -hex 16)" --variables "MYSQL_PASSWORD=$(openssl rand -hex 16)" \
  --variables "MYSQL_USER=rutaproducto" --variables "MYSQL_DATABASE=rutaproducto"
$RW service link MySQL && $RW volume add -m /var/lib/mysql          # the volume needs the linked service
$RW add --service rutaproducto-web                                   # empty service, no cost until deployed
```

MySQL light start command (the free plan allows 0.5 GB per service). `railway environment edit` did NOT apply it; use the API and then a fresh deployment:

```bash
SC='docker-entrypoint.sh mysqld --skip-log-bin --performance-schema=OFF --mysqlx=OFF --innodb-buffer-pool-size=64M --innodb-log-buffer-size=8M --max-connections=30 --table-open-cache=200 --tmp-table-size=16M --max-heap-table-size=16M'
$RW api 'mutation($sid:String!,$eid:String!,$cmd:String!){ serviceInstanceUpdate(serviceId:$sid, environmentId:$eid, input:{startCommand:$cmd}) }' --raw-var sid=<MySQL id> --raw-var eid=<env id> --raw-var cmd="$SC"
$RW api 'mutation($sid:String!,$eid:String!){ serviceInstanceDeployV2(serviceId:$sid, environmentId:$eid) }' --raw-var sid=<MySQL id> --raw-var eid=<env id>
```

Web service settings (one field per call; `builder` is not settable, Railway uses the root Dockerfile by itself): `dockerfilePath`, `preDeployCommand` = `["dotnet RutaProducto.Web.dll --migrate"]`, `healthcheckPath` = `/health`, `healthcheckTimeout` = 120, `restartPolicyType` = `ON_FAILURE`, `restartPolicyMaxRetries` = 3 (limits crash loops that burn credit). Pass JSON through `--variables @file` from a Windows path and run a Windows Python only with `.tools\railway\node_modules\.bin\railway.cmd`.

Variables of the web service (the DB secret is a reference): `ConnectionStrings__DefaultConnection=Server=${{MySQL.RAILWAY_PRIVATE_DOMAIN}};Port=3306;Database=${{MySQL.MYSQL_DATABASE}};User=${{MySQL.MYSQL_USER}};Password=${{MySQL.MYSQL_PASSWORD}}`, `Database__MySqlServerVersion=8.4.0`, `ForwardedHeaders__Enabled=true`, `DataProtection__Provider=Database`, `RateLimiting__Enabled=true`, `Assistant__Provider=Fake`, `PORT=8080`, `Registration__InviteCode=<random, shared with invitees>`. Use single quotes so `${{...}}` reaches Railway untouched.

## Deploy and verify

```bash
$RW up -s rutaproducto-web --ci            # uploads the working tree, builds, runs the pre-deploy migrate
$RW deployment list -s rutaproducto-web ; $RW logs -s rutaproducto-web | tail
U=https://rutaproducto-web-production.up.railway.app
curl -s -o /dev/null -w '%{http_code}\n' $U/health                      # 200
curl -s $U/login | grep -oE '<script[^>]*src="[^"]+"'                    # blazor.web.<hash>.js must answer 200 at $U/<src>
```

Lesson (2026-10-18): the first image had no `_framework/blazor.web.js` (404 in the browser, "Siguiente" never enabled). Cause: the Dockerfile restores with only the `.csproj` files, and the SDK adds `Microsoft.AspNetCore.App.Internal.Assets` only when the project has `.razor` files; fix = `-p:RequiresAspNetWebAssets=true` on that restore. Pinning the SDK image did NOT help.

## Turning off and on (cost control)

Pricing seen in the docs on 2026-10-18: RAM 10 USD/GB-month, vCPU 20 USD/vCPU-month, volume 0.15 USD/GB-month; Free plan 1 USD/month, Trial 5 USD once; when credit ends services stop and volume data is deleted 30 days after the plan expires. Estimate (not measured): web ~1.7 USD + MySQL ~2.7 USD per month if always on.

Off: `railway down -s rutaproducto-web -y`, then `railway down -s MySQL -y` (removes the active deployment; the volume stays).
On: `railway service redeploy -s MySQL -y`, wait until it is up, then `railway service redeploy -s rutaproducto-web -y` (if the image was removed more than 24 h ago use `railway up` again). STATUS: the off/on cycle is documented from the CLI help and NOT yet verified end to end: verify it and edit this paragraph.
