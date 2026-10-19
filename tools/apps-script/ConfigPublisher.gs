const BREAK = String.fromCharCode(10);

const LEWDVENTURE_ADMIN_URL_PROPERTY = 'LEWDVENTURE_ADMIN_URL';

const LEWDVENTURE_ENVIRONMENTS = {
  dev: { title: 'dev', urlProperty: 'LEWDVENTURE_DEV_URL', keyProperty: 'LEWDVENTURE_DEV_KEY' },
  stage: { title: 'stage', urlProperty: 'LEWDVENTURE_STAGE_URL', keyProperty: 'LEWDVENTURE_STAGE_KEY' },
};

function onOpen() {
  SpreadsheetApp.getUi()
    .createMenu('Lewdventure')
    .addItem('Опубликовать конфиги на dev', 'publishToDev')
    .addItem('Опубликовать конфиги на stage', 'publishToStage')
    .addSeparator()
    .addItem('Проверить баланс на dev', 'checkBalanceDev')
    .addItem('Проверить баланс на stage', 'checkBalanceStage')
    .addSeparator()
    .addItem('Статус dev', 'statusDev')
    .addItem('Статус stage', 'statusStage')
    .addToUi();
}

function publishToDev() {
  publishConfigs_('dev');
}

function publishToStage() {
  publishConfigs_('stage');
}

function checkBalanceDev() {
  checkBalance_('dev');
}

function checkBalanceStage() {
  checkBalance_('stage');
}

function statusDev() {
  showStatus_('dev');
}

function statusStage() {
  showStatus_('stage');
}

function publishConfigs_(environmentName) {
  const ui = SpreadsheetApp.getUi();
  const environment = LEWDVENTURE_ENVIRONMENTS[environmentName];
  const confirmation = ui.prompt(
    'Публикация на ' + environment.title,
    'Коротко опишите изменение (попадёт в историю активаций):',
    ui.ButtonSet.OK_CANCEL);

  if (confirmation.getSelectedButton() !== ui.Button.OK) {
    return;
  }

  const sheetsResponse = request_(environment, 'get', '/api/config/sheets', null);

  if (sheetsResponse.code !== 200 || sheetsResponse.body === null) {
    ui.alert('Публикация на ' + environment.title, 'Не удалось получить список листов' + BREAK + 'HTTP ' + sheetsResponse.code + BREAK + sheetsResponse.text, ui.ButtonSet.OK);
    return;
  }

  let payload;

  try {
    payload = collectSheets_(sheetsResponse.body.sheets, confirmation.getResponseText());
  } catch (error) {
    ui.alert('Публикация на ' + environment.title, String(error), ui.ButtonSet.OK);
    return;
  }

  const readSheets = payload.readSheets;

  delete payload.readSheets;

  const response = request_(environment, 'post', '/api/config/upload', payload);
  ui.alert(
    'Публикация на ' + environment.title,
    formatPublishResult_(response) + BREAK + BREAK + 'Прочитанные вкладки:' + BREAK + readSheets.join(BREAK),
    ui.ButtonSet.OK);
}

function checkBalance_(environmentName) {
  const ui = SpreadsheetApp.getUi();
  const environment = LEWDVENTURE_ENVIRONMENTS[environmentName];
  const title = 'Проверка баланса на ' + environment.title;
  const sheetsResponse = request_(environment, 'get', '/api/config/sheets', null);

  if (sheetsResponse.code !== 200 || sheetsResponse.body === null) {
    ui.alert(title, 'Не удалось получить список листов' + BREAK + 'HTTP ' + sheetsResponse.code + BREAK + sheetsResponse.text, ui.ButtonSet.OK);
    return;
  }

  let payload;

  try {
    payload = collectSheets_(sheetsResponse.body.sheets, 'проверка баланса');
  } catch (error) {
    ui.alert(title, String(error), ui.ButtonSet.OK);
    return;
  }

  delete payload.readSheets;

  const response = request_(environment, 'post', '/api/config/check', payload);

  if (response.body === null || !response.body.succeeded) {
    ui.alert(title, formatPublishResult_(response), ui.ButtonSet.OK);
    return;
  }

  const result = response.body;
  const adminUrl = PropertiesService.getScriptProperties().getProperty(LEWDVENTURE_ADMIN_URL_PROPERTY);
  const lines = [
    'Версия ' + result.shortVersion + ' собрана и сохранена, мастер не менялся.',
    'Изменений к мастеру: ' + countChanges_(result.changes) + '.',
  ];
  let html = '<p>' + lines.map(escapeHtml_).join('<br>') + '</p>';

  if (adminUrl) {
    const link = adminUrl.replace(/\/$/, '') + '/Balance?env=' + encodeURIComponent(environment.title) + '&Version=' + encodeURIComponent(result.version);

    html += '<p><a href="' + escapeHtml_(link) + '" target="_blank">Открыть песочницу баланса с этой версией</a></p>';
  } else {
    html += '<p>Откройте админку → «Баланс» и выберите версию ' + escapeHtml_(result.shortVersion) + '. Чтобы здесь была ссылка, задайте Script Property ' + LEWDVENTURE_ADMIN_URL_PROPERTY + '.</p>';
  }

  html += '<p style="color:#666">Если версия понравится — опубликуйте её обычной кнопкой или сделайте мастером в админке → «Конфиги».</p>';

  ui.showModalDialog(HtmlService.createHtmlOutput('<div style="font-family:sans-serif;font-size:14px">' + html + '</div>').setWidth(460).setHeight(220), title);
}

function countChanges_(changes) {
  let total = 0;

  (changes || []).forEach(function (change) {
    total += change.added.length + change.removed.length + change.changed.length;
  });

  return total;
}

function escapeHtml_(text) {
  return String(text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function collectSheets_(sheets, reason) {
  const collected = [];
  const readSheets = [];

  for (let i = 0; i < sheets.length; i++) {
    const definition = sheets[i];
    const spreadsheet = SpreadsheetApp.openById(definition.spreadsheetId);
    const sheet = findFirstVisibleSheet_(spreadsheet, definition.domain);
    const dataRange = sheet.getDataRange();

    collected.push({
      domain: definition.domain,
      spreadsheetId: definition.spreadsheetId,
      range: dataRange.getA1Notation(),
      values: trimTrailingEmptyRows_(dataRange.getDisplayValues()),
    });

    readSheets.push(definition.domain + ' <- ' + sheet.getName() + ' ' + dataRange.getA1Notation());
  }

  return { reason: reason, sheets: collected, readSheets: readSheets };
}

function findFirstVisibleSheet_(spreadsheet, domain) {
  const all = spreadsheet.getSheets();

  for (let i = 0; i < all.length; i++) {
    if (all[i].isSheetHidden() === false) {
      return all[i];
    }
  }

  throw 'В таблице домена ' + domain + ' нет ни одной видимой вкладки';
}

function trimTrailingEmptyRows_(values) {
  let last = values.length;

  while (0 < last && isEmptyRow_(values[last - 1])) {
    last--;
  }

  return values.slice(0, last);
}

function isEmptyRow_(row) {
  for (let i = 0; i < row.length; i++) {
    if (String(row[i]).trim() !== '') {
      return false;
    }
  }

  return true;
}

function showStatus_(environmentName) {
  const ui = SpreadsheetApp.getUi();
  const environment = LEWDVENTURE_ENVIRONMENTS[environmentName];
  const response = request_(environment, 'get', '/api/config/status', null);

  if (response.code !== 200) {
    ui.alert('Статус ' + environment.title, 'HTTP ' + response.code + '\n' + response.text, ui.ButtonSet.OK);
    return;
  }

  const status = response.body;
  const lines = [
    'Загружено: ' + status.loadedShortVersion,
    'Источник: ' + status.source,
    'Загружено в: ' + status.loadedAt,
    'Активная версия: ' + (status.activeVersion || '—'),
    'Синхронно: ' + (status.inSync ? 'да' : 'нет'),
  ];
  ui.alert('Статус ' + environment.title, lines.join('\n'), ui.ButtonSet.OK);
}

function request_(environment, method, path, payload) {
  const properties = PropertiesService.getScriptProperties();
  const baseUrl = properties.getProperty(environment.urlProperty);
  const key = properties.getProperty(environment.keyProperty);

  if (!baseUrl || !key) {
    throw new Error('Script Properties ' + environment.urlProperty + ' и ' + environment.keyProperty + ' не заданы');
  }

  const options = {
    method: method,
    muteHttpExceptions: true,
    headers: { 'X-Config-Key': key },
  };

  if (payload !== null) {
    options.contentType = 'application/json';
    options.payload = JSON.stringify(payload);
  }

  const response = UrlFetchApp.fetch(baseUrl.replace(/\/$/, '') + path, options);
  const text = response.getContentText();
  let body = null;

  try {
    body = JSON.parse(text);
  } catch (error) {
    body = null;
  }

  return { code: response.getResponseCode(), text: text, body: body };
}

function formatPublishResult_(response) {
  if (response.body === null) {
    return 'HTTP ' + response.code + '\n' + response.text;
  }

  const result = response.body;
  const lines = [];

  lines.push(result.succeeded ? 'Успешно' : 'Ошибка');
  lines.push('Версия: ' + result.shortVersion);
  lines.push(result.activated ? 'Активирована новая версия' : 'Версия не изменилась');

  (result.errors || []).forEach(function (error) {
    lines.push('Ошибка: ' + error);
  });

  (result.changes || []).forEach(function (change) {
    lines.push(change.domain + ': строк ' + change.rowsBefore + ' → ' + change.rowsAfter
      + ', добавлено ' + change.added.length
      + ', удалено ' + change.removed.length
      + ', изменено ' + change.changed.length);
  });

  if ((result.warnings || []).length > 0) {
    lines.push('Предупреждений: ' + result.warnings.length);
  }

  return lines.join('\n');
}
