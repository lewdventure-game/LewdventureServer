# Боевые характеристики и бонусы

Источник: GDD «Боевые характеристики и Бонусы».  
Использование в бою — [`01-battle-loop.md`](01-battle-loop.md).

## Принцип

Сервер в каждый момент действия использует **актуальные итоговые** характеристики.  
Клиент показывает HP/energy/flytext только по командам сервера.

## Источники бонусов аккаунта игрока (пополняемо)

- стартовые значения Constants;
- тренировки;
- снаряжение;
- персонажи;
- саммоны;
- артефакты;
- аспекты (созвездия);
- перки;
- события уровней / статусы / временные work_mode.

Характеристики врагов — итоговые значения из конфигов сущностей (могут модифицироваться эффектами игрока).

## Список характеристик (battle-relevant)

| Характеристика | Тех. бонусы (local / minor / major / perk) | Формула итога |
| --- | --- | --- |
| МАКС.ХП | max_health_* / health_perk | 1 |
| ТЕКУЩЕЕ ХП | current_health_local | 4 / healing-формулы |
| ВАМПИРИЗМ | vampyrism_local / vampyrism_perk | 6 |
| УВЕЛИЧЕНИЕ_ЛЕЧЕНИЯ | healing_boost_local / healing_boost_perk | 7 |
| ДМГ | damage_* | 1 |
| АТК_МН | atk_multiplier_local / atk_multiplier_perk | 2 |
| УКЛОНЕНИЕ | evasion_local / evasion_perk | 2 |
| КРИТ_ШАНС | crit_chance_local / crit_chance_perk | 2 |
| КРИТ_МН | crit_multiplier_local / crit_multiplier_perk | 2 |
| КОМБО_1_ШАНС | combo_1_chance_* | 2 |
| КОМБО_2_ШАНС | combo_2_chance_* | 2 |
| КОМБО_1_МН | combo_1_multiplier_* | 2 |
| КОМБО_2_МН | combo_2_multiplier_* | 2 |
| КОМБО_МН (legacy) | combo_multiplier_* | 2, копится в оба combo_1 и combo_2 |
| КОНТР_ШАНС | counter_chance_* | 2 |
| КОНТР_МН | counter_multiplier_* | 2 |
| СПЕЛЛ_МН | spell_multiplier_* | 2 |
| ЗАЩИТА | defence_local / defence_perk | 5 |
| ЭНЕРГИЯ (gain) | energy_local / energy_perk | 2 |
| ЭНЕРГИЯ_МАКС | из Constants, непрокачиваемая | — |
| ЩИТ | не в демо | — |

`equip_spell_multiplier_*` в GDD зачёркнут — не планировать без отдельного решения.

АТК_МН, УКЛОНЕНИЕ, КРИТ_ШАНС, КРИТ_МН, КОМБО_*_ШАНС, КОМБО_*_МН, КОНТР_ШАНС, КОНТР_МН, СПЕЛЛ_МН, энергия (gain) — **формула 2**. Живые константы уже в шкале 0..1 (`atk_multiplier_base = 1`, `crit_chance_base = 0.05` и т.п.).

## Формулы

1. **Сложный множитель:**  
   `round((((base + sum_local) * (1 + sum_minor)) * (1 + sum_major)) * (1 + sum_perk_and_events))`  
   Итог ≥ 1 (кроме текущего ХП).  
   Слоя `*_global` **нет**: в `BonusType` и buckets нет Global. Не выдумывать, пока Sheets не привезут колонки.

2. **Простой множитель (канон для шансов и АТК_МН):**  
   `(base + sum_local) * (1 + sum_perk_and_events)`

3. **Healing from max (ceil):**  
   `ceil(prev + maxHp * (healingBonus * healingBoost))`

4. **Урон по текущему ХП:**  
   `prev - damageTaken`

5. **Защита:**  
   `def_coeff_const * raw / (1 + def_coeff * |raw|)`  
   где `raw = (base + sum_local) * (1 + sum_perk)`. Отрицательная броня: модуль только в знаменателе, знак raw в числителе сохраняется.

6. **Вампиризм (ceil):**  
   `ceil(dealtDamage * (const + sum_local) * (1 + sum_perk) * healingBoost)`

7. **Healing boost:**  
   `round((1 + healing_boost_base + sum_local) * (1 + sum_perk_and_events))`

### Канон vs сырой текст Аксёнова

В сыром GDD «формула 3» для шансов/АТК_МН записана как `(1 + base + local) * …`. Это **ошибка документа**, не канон. При живых константах получится АТК_МН ≈ 2 и крит ≈ 105%. Код (`ApplyFormula2`) считает формулу 2 этого файла: `(base + local) * (1 + perk)`.

Нумерация в **этом** файле другая: формула 3 здесь — healing from max, не шансы.

`combo_mn` vs `combo_1_mn` / `combo_2_mn`: канон — раздельные множители. Legacy `combo_multiplier_*` (constants / bonus / enemy pack) копится в оба bucket, если раздельных ключей нет.

При изменении МАКС.ХП текущее ХП пересчитывается **пропорционально**.

## Бонусы-эффекты

Не складываются в «итоговую шкалу», а срабатывают эффектом:

| bonus | Эффект |
| --- | --- |
| `healing_from_max` | `+ maxHp * value * healingBoost` к текущему ХП (value может быть < 0) |
| `healing` | `+ currentHp * value * healingBoost` |

Обычно `operator = add`, `work_mode = permanent` (разово при получении для эффектов).

## Выдача бонусов

Через reward: `reward_type=bonus`, `reward_id`, `reward_value`.

### operator

| Значение | Поведение |
| --- | --- |
| `add` | Суммируется с такими же бонусами |
| `replace` | Жёстко подменяет итог характеристики, пока действует |

### work_mode

| Значение | Поведение |
| --- | --- |
| `permanent` | Постоянно |
| `if_equipped:entity:id` | Пока экипирована сущность (equipments/characters/summons) |
| `end_of_game` | До конца забега |
| `end_of_battle` | До конца боя |
| `next_battles:N` | На N следующих битв; внутри simulate: RemainingBattles, decrement на battle end |
| `first_turns:N` | Первые N ходов каждого боя в забеге |
| `every_turn` | Накапливается каждый ход в бою, сбрасывается после боя |

Для текущего battle-скоупа обязательны режимы, влияющие **внутри боя** (`end_of_battle`, `first_turns`, `every_turn`, `next_battles`, статусные бонусы). Meta-режимы — учесть в модели, полная выдача забега/аккаунта позже.

## ДМГ саммона

Отдельный от ДМГ аккаунта:

`summonDmg = dmg_on_lvls[level] * mastery.dmg_multiplier`

Подробнее — [`06-entities.md`](06-entities.md).
