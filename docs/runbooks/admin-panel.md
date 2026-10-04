[← Runbooks](README.md)

# Веб-админка

`https://admin.lewdventure.online` — отдельный сервис `src/Lewdventure.Server.Admin` (ASP.NET Razor Pages). Вход по логину и паролю, роли `admin` и `tester`. Переключатель окружений dev / stage сверху; prod подсвечивается красной полосой.

| Раздел | tester | admin |
| --- | --- | --- |
| Обзор: мастер-версия конфигов, идущие эксперименты | да | да |
| Эксперименты: список, карточка, журнал | смотреть | создать, запустить, остановить набор, убрать группу, закончить, раскатить, удалить черновик |
| Конфиги: снапшоты и мастер | смотреть | сделать мастером, перечитать активную |
| Игроки: группа, версия конфигов, профиль, журнал выдач | смотреть, выдавать награды | + удалить аккаунт |
| Аналитика (`/grafana/`, Grafana с тем же входом) | Viewer | Admin: правка дашбордов |

## Как устроено

```text
браузер → Cloudflare → Caddy (sites/admin.caddy) → lewdventure-admin:8080
админка → сеть lewdventure-edge → lewdventure-<env>-api:9090 (ops-порт, X-Admin-Key)
```

- Ключи `X-Admin-Key` окружений лежат только в `/opt/lewdventure/admin/.env`, в браузер не попадают.
- Админка передаёт логин в `X-Admin-Actor`: в журналах экспериментов, активаций и выдач видно `admin:<логин>`.
- Пароли — PBKDF2-SHA256, 210 000 итераций; после 5 неудачных попыток логин блокируется на 15 минут; сессия — cookie на 12 часов (`Secure`, `HttpOnly`, `SameSite=Strict`), все формы с antiforgery.
- Данные в томе `admin-data` (`/app/data`): `admin-accounts.json`, `audit.log` (все действия и входы), ключи Data Protection.

## Аккаунты

```bash
cd /opt/lewdventure/admin
docker compose exec -T admin dotnet Lewdventure.Server.Admin.dll accounts list
docker compose exec -T admin dotnet Lewdventure.Server.Admin.dll accounts add <login> <admin|tester>
docker compose exec -T admin dotnet Lewdventure.Server.Admin.dll accounts reset <login>
docker compose exec -T admin dotnet Lewdventure.Server.Admin.dll accounts disable <login>
docker compose exec -T admin dotnet Lewdventure.Server.Admin.dll accounts enable <login>
```

`add` и `reset` печатают новый пароль один раз.

## Развёртывание

1. DNS в Cloudflare: A-запись `admin` на IP VPS, Proxied. Origin-сертификат `*.lewdventure.online` уже покрывает поддомен.
2. `/opt/lewdventure/admin/compose.yaml` — из `deploy/admin/compose.yaml`; `.env` — по `deploy/admin/.env.example`, ключи `ApiKey` берутся из `Admin__ApiKey` окружений.
3. `deploy/proxy/sites/admin.caddy` → `/opt/lewdventure/proxy/sites/`, в `/opt/lewdventure/proxy/.env` строка `ADMIN_DOMAIN=admin.lewdventure.online`, затем `docker compose up -d --force-recreate caddy`.
4. `docker compose up -d --wait` в `/opt/lewdventure/admin`.

Перенос на отдельный VPS: админке нужен только доступ к ops-портам окружений — через приватную сеть или туннель; меняются `BaseUrl` в `.env`.

## Выкладка из локального master без push

Используется, пока изменения не запушены. Следующий штатный деплой из GitHub перезапишет dev тем, что лежит в `master` на GitHub.

```bash
git bundle create server.bundle master
scp server.bundle root@<vps>:/opt/lewdventure/build/
ssh root@<vps>
cd /opt/lewdventure/build
git -C repo fetch ../server.bundle master:refs/heads/incoming && git -C repo checkout -f incoming
cd repo && TAG=sha-$(git rev-parse --short=12 HEAD) && R=ghcr.io/lewdventure-game
docker build -f deploy/docker/Dockerfile --target api -t $R/lewdventure-server:$TAG .
docker build -f deploy/docker/Dockerfile --target config-tool -t $R/lewdventure-config-tool:$TAG .
docker build -f deploy/docker/Dockerfile --target admin -t $R/lewdventure-admin:$TAG .
cp deploy/compose/compose.yaml deploy/compose/compose.vps.yaml deploy/compose/compose.dev.yaml /opt/lewdventure/dev/compose/
cp deploy/scripts/*.sh /opt/lewdventure/dev/scripts/
LEWD_LOCAL_IMAGE=1 DEPLOY_ACTOR=local-build /opt/lewdventure/dev/scripts/deploy.sh dev $TAG
```

`LEWD_LOCAL_IMAGE=1` отключает `docker compose pull` в `deploy.sh`. Админка обновляется сменой `IMAGE_TAG` в её `.env` и `docker compose up -d --wait`.
