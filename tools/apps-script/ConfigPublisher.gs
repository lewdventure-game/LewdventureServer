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

  const response = request_(environment, 'post', '/api/config/publish', { reason: confirmation.getResponseText() });
  ui.alert('Публикация на ' + environment.title, formatPublishResult_(response), ui.ButtonSet.OK);
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
