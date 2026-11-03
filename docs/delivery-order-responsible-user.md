# Ответственный за рейс

`delivery_orders.responsible_user_id` — необязательная ссылка на `AspNetUsers.Id`.
`NULL` означает, что ответственный ещё не назначен. Пустая строка не используется.
Поле независимо от обязательного `user_id`; назначение ответственного не меняет его.

Модель: `DeliveryOrder.ResponsibleUserId` (`string?`). Добавлены индекс и внешний ключ.
Удалить назначенного пользователя можно после снятия назначения или выбора другого
ответственного: для связи используется `Restrict`, как и для остальных пользователей.

В консоли диспетчера пакетов Visual Studio с проектом `Logistics_Assistant`:

```powershell
Update-Database -Migration AddDeliveryOrderResponsibleUser -Context ApplicationDbContext
```

Миграция уже создана, вызывать `Add-Migration` ещё раз не нужно. Она сохраняет рейсы
и добавляет им `responsible_user_id = NULL`. Удалять и генерировать данные заново
для этого не требуется.

Все рейсы без назначенного ответственного:

```sql
SELECT *
FROM delivery_orders
WHERE responsible_user_id IS NULL
ORDER BY id;
```

Обновлённый генератор `seed-logistics.sql` оставляет ответственного пустым у всех
новых тестовых рейсов; `user_id` по-прежнему содержит технического пользователя.
