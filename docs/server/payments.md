# Покупки Steam и Nutaku

API реализует Steam Wallet и Nutaku Gold через GI2 для F2P DL Client. В обоих каналах одна покупка выдаёт одну единицу настроенного платного права. Единицы хранятся в отдельном инвентаре по каналу и возвращаются клиенту через API. Игровые ресурсы и прогресс не меняются. Если два заказа дают одинаковое право, возврат отзовёт единицу только по своему заказу. Показ и применение права на конкретный игровой контент подключаются вместе с Unity-витриной.

## Включение

Платёжные маршруты появляются при заданном `Payments:SteamAppId` или `Payments:NutakuTitleId`. Для запуска требуются включённая авторизация, Mongo replica set и успешное создание индексов. Оба канала по умолчанию в sandbox. Продажи включаются отдельными флагами `Payments:*PurchasesEnabled`. При отключении продаж оставьте ID, ключи и окружение без изменений: сервис продолжит обрабатывать существующие заказы, колбэки и возвраты. Изменение параметров применяется после перезапуска API.

Пример конфигурации тестового предложения в переменных окружения. App ID, Title ID и значения SKU приведены как примеры.

```dotenv
Payments__SteamAppId=123456
Payments__SteamSandbox=true
Payments__SteamPurchasesEnabled=true
Payments__NutakuTitleId=lewdventure-sandbox
Payments__NutakuSandbox=true
Payments__NutakuPurchasesEnabled=true
Payments__Offers__0__Sku=starter-access
Payments__Offers__0__Name=Starter Access
Payments__Offers__0__Entitlement=starter-access
Payments__Offers__0__SteamItemId=1001
Payments__Offers__0__SteamPrices__USD=199
Payments__Offers__0__NutakuGold=200
```

В env или `secrets/` укажите `Payments__SteamPublisherKey` с доступом к нужному App ID и Microtransactions и `Payments__NutakuServerKey` из CMS. Настройте Nutaku CMS Game Handshake URL на `/api/payments/nutaku/handshake` и Game Payment Handler URL на `/api/payments/nutaku/handler` по HTTPS. Sandbox и production должны использовать свои настройки, ключи и базы. Суммы Steam задаются целыми числами в единицах API Valve с учётом валютных шагов. `NutakuGold` указывается в целых Gold от 10 до 30 000. Ноль или отсутствующий маппинг скрывает оффер на соответствующей площадке. При замене каталога существующие заказы сохраняют первоначальные условия. [Steam Supported Currencies](https://partner.steamgames.com/doc/store/pricing/currencies) · [Nutaku GI2](https://nutakudoc.atlassian.net/wiki/spaces/ND/pages/3482386433)

## Вход, витрина и восстановление заказа

| Метод и путь | Контракт |
| --- | --- |
| `POST /api/auth/steam` | `{"credential":"<билет в hex>"}`, билет от `GetAuthTicketForWebApi` с identity `lewdventure-server`. |
| `POST /api/auth/nutaku` | `{"credential":"<код handshake>"}`. В GI2-ответе `message` содержит JSON от игрового сервера. Из него нужно взять `code` и обменять его в течение двух минут. Код одноразовый. |
| `GET /api/payments/catalog` | Предложения подтверждённого канала с `sku`, `name`, `currency`, `amount`. |
| `POST /api/payments/steam/orders` | `{"requestKey":"<UUID>","sku":"starter-access","currency":"USD","amount":199}`. Сохраняйте UUID до начала checkout и используйте его повторно для той же попытки. |
| `GET /api/payments/orders` | Последние 50 заказов аккаунта в текущем канале. |
| `GET /api/payments/orders/{orderId}` | `orderId`, `sku`, `amount`, `currency`, `state`, `createdAt`. ID передаётся строкой. |
| `GET /api/payments/entitlements` | Объект `{ "<entitlement>": <количество> }` с действующими правами. |

Вход возвращает `userId`, `accessToken`, `accessExpiresAt`. Bearer действует в игровом API и в магазине только своего канала. После истечения срока Unity повторяет платформенный вход. Существующие аккаунты устройства и аккаунты разных платформ остаются самостоятельными. Авторизация по `deviceId` не даёт доступ к платёжным маршрутам. Витрина, статус и инвентарь требуют Bearer.

Steam запускает Overlay после `InitTxn`. Колбэк Unity может обновить экран ожидания. Последующее подтверждение выполняет сервер, который сверяет `QueryTxn` и завершает одобренный заказ через `FinalizeTxn`. После `502`, обрыва или перезапуска проверьте заказ по ID или повторите запрос с тем же `requestKey`. Покажите успех после состояния `Granted`, затем обновите права. `Created`, `Initiating` и `AwaitingApproval` означают ожидание, `Failed` означает отказ, `Reversed` означает отзыв. Сервер опрашивает ожидающие операции до суток и продолжает сверять отчёт Steam после этого. Неопределённый заказ старше суток разбирает поддержка.

В Nutaku Unity отправляет данные оффера в GI2 по DL-инструкции. GPHS получает отдельные POST и PUT от Nutaku. Для клиентского запроса статуса используется `paymentId` из GI2. Повторный PUT подтверждает уже сохранённую выдачу, повторное начисление исключено. В live отклоняются операции с флагом тестового пользователя. Провайдерские ключи остаются на сервере. [Steam Microtransactions](https://partner.steamgames.com/doc/features/microtransactions/implementation) · [Nutaku DL Client](https://nutakudoc.atlassian.net/wiki/spaces/ND/pages/3483664391)

## Поддержка и возвраты

Операторские маршруты доступны на ops-порту с действующим admin API key. При ручном возврате укажите проверяемый номер обращения или записи площадки в `{"reference":"case-123"}`. Заголовок `X-Admin-Actor` связывает запись с оператором.

- `GET /api/admin/payments/{platform}/{orderId}` показывает локальный заказ по Steam order ID или Nutaku payment ID.
- `POST /api/admin/payments/steam/{orderId}/reverse` запрашивает полный возврат в Steam. `204` означает сохранённый отзыв, `202` — неопределённый результат, который нужно сверить. Провайдерские возвраты и chargeback также обрабатывает фоновая сверка Steam.
- `GET /api/admin/payments/nutaku/{orderId}/provider-status` показывает статус GI2 для аварийной диагностики. Учитывайте задержку репликации Nutaku. Статус `purchase` может сохраняться после обычного возврата.
- `POST /api/admin/payments/nutaku/{orderId}/reverse` записывает уже подтверждённый площадкой возврат и отзывает право локально. Сам возврат Gold выполняет Nutaku.

Коллекции `payment_orders`, `payment_movements` и `payment_entitlements` хранят заказы, основания движений и действующие права. Они переживают сброс игрового профиля. Перед production согласуйте сроки хранения, процесс обработки удалённых аккаунтов, сверку выплат и подтверждение Nutaku-возвратов. После удаления платформенного аккаунта новый вход создаст новый игровой идентификатор. Удалённые покупки остаются доступны для операторской сверки по внешнему ID.

На sandbox проверьте оба потока с Unity, повтор колбэков, перезапуск клиента, смену цены посередине и возврат. Для Nutaku отдельно воспроизведите разрыв соединения после выдачи, когда у пользователя может одновременно остаться право и восстановиться Gold. Порядок принятия решения по этому случаю зависит от подтверждения площадки. [Steam GetReport](https://partner.steamgames.com/doc/webapi/ISteamMicroTxn#GetReport) · [Nutaku GPHS](https://nutakudoc.atlassian.net/wiki/spaces/ND/pages/3482124347)
