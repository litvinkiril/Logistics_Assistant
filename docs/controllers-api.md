# API после переноса из Razor Pages

Рабочие обработчики из `Pages/Products/Search.cshtml.cs` перенесены в
`Controllers/ProductsController.cs`. Файлы Pages, регистрация/маршрутизация
Razor Pages и пакет Identity.UI удалены. Бизнес-запросы остаются в существующих
сервисах. Модели, миграции и рабочая БД не изменялись.

## Маршруты товаров

| Старый запрос | Новый запрос |
| --- | --- |
| `/Products/Search?handler=ById&idStr=1` | `GET /api/products/1` |
| `/Products/Search?handler=ByName&name=шкаф&offsetStr=0` | `GET /api/products/search?name=шкаф&offset=0` |
| `/Products/Search?handler=ByUrl&url=...` | `GET /api/products/by-url?url=...` |
| `/Products/Search?handler=WarehouseStockByProductId&idStr=1` | `GET /api/products/1/stock` |

Старые URL Pages больше не обслуживаются (404). Alias/редиректов на них нет.

- По ID сохранён прежний формат: массив с одним товаром. Отсутствующий товар — 404.
- По имени — массив, до 10 результатов, сортировка по ID. `offset` по умолчанию 0.
  Пустое имя, отрицательный, нечисловой или слишком большой offset — 400.
- По URL — массив с найденным товаром или пустой массив. Пустой URL — 400.
  URL с `&`, `?`, `#` передавать с URL-кодированием значения параметра.
- Остатки — объект `{ product, warehouses }`, где warehouses содержит и склады,
  и ПВЗ. Для существующего товара без остатков это пустой массив, для неизвестного — 404.
- ID 0 или отрицательный — 400. Нечисловой ID/переполнение long не соответствует
  маршруту и даёт 404; это отличается от старого строкового параметра `idStr`.
- Ошибки типизации query-параметров обрабатывает ApiController (ValidationProblemDetails).

## Другие маршруты

- `GET /` — HomeController возвращает JSON с названием API и ссылками.
- `/error` — ErrorController формирует безопасный ProblemDetails со статусом 500.
  Вне Development необработанные исключения перенаправляются сюда внутри pipeline;
  маршрут работает для любого HTTP-метода, без HTML и раскрытия текста исключения.
- `/api/logistics-points`, `/api/logistics-points/{id}` и
  `/api/logistics-points/search` остались прежними.
- `/Privacy` был незаполненным шаблоном и удалён.

## Identity

Вместо AddDefaultIdentity используется AddIdentityCore + cookies, SignInManager,
EF stores и стандартные провайдеры токенов. Сохранены требования уникального email,
подтверждения аккаунта и длина ключей Identity 128, поэтому модель БД не меняется.
Добавлен явный вызов UseAuthentication перед UseAuthorization.

Готовые страницы `/Identity/Account/Login`, `/Identity/Account/Register` и другие
страницы Identity UI больше не доступны. Собственных API входа, регистрации и
восстановления пароля пока нет — это отдельная функциональность, а не часть
поиска товаров. Сервисы и данные пользователей сохранены; новые ограничения доступа
к публичным поисковым методам не добавлялись. При защите API через Authorize
cookie-аутентификация возвращает 401/403 без ссылки на удалённую страницу входа.

## Проверка

Из папки решения, включая случай отключённых PowerShell-скриптов:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\test-logistics-points.ps1" -AllTests
```

Тесты проверяют новые HTTP-маршруты и ответы, offset, URL с query-строкой, остатки,
отсутствие страниц Razor, обработку исключений GET/POST, ответ 401 без редиректа
и отсутствие изменений модели EF. Используется отдельная временная PostgreSQL.
Для ручных запросов: `docs/products.http`.
