(function () {
    'use strict';

    const STORAGE_KEY = 'poproshaykabot.demo.chatSettings.v2';

    const colorFields = [
        { key: 'backgroundColor', label: 'Фон сообщения' },
        { key: 'textColor', label: 'Основной текст' },
        { key: 'usernameColor', label: 'Имя пользователя' },
        { key: 'systemMessageColor', label: 'Системные' },
        { key: 'timestampColor', label: 'Время' },
    ];

    const cssVarMap = {
        backgroundColor: ['--chat-bg-color', colorToCss],
        textColor: ['--chat-text-color', colorToCss],
        usernameColor: ['--chat-username-color', colorToCss],
        systemMessageColor: ['--chat-system-color', colorToCss],
        timestampColor: ['--chat-timestamp-color', colorToCss],
        fontFamily: ['--chat-font-family', v => v],
        fontSize: ['--chat-font-size', v => v + 'px'],
        padding: ['--chat-padding', v => v + 'px'],
        margin: ['--chat-margin', v => v + 'px 0'],
        borderRadius: ['--chat-border-radius', v => v + 'px'],
        animationDuration: ['--chat-animation-duration', v => v + 'ms'],
        emoteSizePixels: ['--emote-size', v => v + 'px'],
        badgeSizePixels: ['--badge-size', v => v + 'px'],
        userAvatarSizePixels: ['--avatar-size', v => v + 'px'],
        fadeOutAnimationDurationMs: ['--fade-out-duration', v => v + 'ms'],
    };

    const classToggles = [
        ['showUserTypeBorders', '.message', 'no-borders'],
        ['highlightFirstTimeUsers', '.message.first-time', 'no-first-time-effects'],
        ['highlightMentions', '.message', 'no-mentions'],
        ['enableMessageShadows', '.message', 'no-shadows'],
        ['enableSpecialEffects', '.message', 'no-special-effects'],
        ['showTimestamp', '#chat .message, .demo-stage .message', 'no-timestamp'],
        ['showUserAvatars', '.message', 'no-avatars'],
    ];

    const fontWeightVar = '--chat-font-weight';

    const rangeFields = [
        ['cfg-fontSize', 'fontSize'],
        ['cfg-padding', 'padding'],
        ['cfg-margin', 'margin'],
        ['cfg-borderRadius', 'borderRadius'],
        ['cfg-maxMessages', 'maxMessages'],
        ['cfg-emoteSizePixels', 'emoteSizePixels'],
        ['cfg-badgeSizePixels', 'badgeSizePixels'],
        ['cfg-userAvatarSizePixels', 'userAvatarSizePixels'],
        ['cfg-messageImageMaxHeightPixels', 'messageImageMaxHeightPixels'],
        ['cfg-scrollAnimationDuration', 'scrollAnimationDuration'],
        ['cfg-scrollToBottomThreshold', 'scrollToBottomThreshold'],
        ['cfg-scrollPauseAfterUserMs', 'scrollPauseAfterUserMs'],
        ['cfg-animationDuration', 'animationDuration'],
        ['cfg-messageLifetimeSeconds', 'messageLifetimeSeconds'],
        ['cfg-fadeOutAnimationDurationMs', 'fadeOutAnimationDurationMs'],
    ];

    let settings = null;
    let serverSnapshot = null;
    let schema = null;
    let animations = null;
    let isDirty = false;
    let bootstrapFailed = false;

    const drawer = document.getElementById('config-drawer');
    const toggleButton = document.getElementById('config-toggle');
    const closeButton = document.getElementById('config-close');
    const revertButton = document.getElementById('config-revert');
    const pushButton = document.getElementById('config-push');
    const colorsContainer = document.getElementById('config-colors');
    const statusEl = document.getElementById('config-status');
    const dirtyEl = document.getElementById('config-dirty');
    const errorEl = document.getElementById('config-error');

    function deepClone(value) {
        return structuredClone(value);
    }

    function colorToCss(color) {
        const a = String(Number((color.a / 255).toFixed(3)));
        return 'rgba(' + color.r + ', ' + color.g + ', ' + color.b + ', ' + a + ')';
    }

    function colorToHex(color) {
        const hex = n => n.toString(16).padStart(2, '0');
        return '#' + hex(color.r) + hex(color.g) + hex(color.b);
    }

    function arraysEqual(a, b) {
        if (a.length !== b.length) return false;
        for (let i = 0; i < a.length; i++) {
            if (!settingsEqual(a[i], b[i])) return false;
        }
        return true;
    }

    function objectsEqual(a, b) {
        const keysA = Object.keys(a);
        const keysB = Object.keys(b);
        if (keysA.length !== keysB.length) return false;
        for (const key of keysA) {
            if (!Object.hasOwn(b, key)) return false;
            if (!settingsEqual(a[key], b[key])) return false;
        }
        return true;
    }

    function settingsEqual(a, b) {
        if (a === b) return true;
        if (a === null || b === null || typeof a !== 'object' || typeof b !== 'object') {
            return a === b;
        }
        const aArray = Array.isArray(a);
        const bArray = Array.isArray(b);
        if (aArray !== bArray) return false;
        return aArray ? arraysEqual(a, b) : objectsEqual(a, b);
    }

    function readDraftFromStorage() {
        try {
            const raw = globalThis.localStorage.getItem(STORAGE_KEY);
            if (!raw) return null;
            const parsed = JSON.parse(raw);
            if (!parsed || typeof parsed !== 'object' || !parsed.draft) return null;
            return parsed.draft;
        } catch (e) {
            console.warn('Не удалось прочитать черновик из localStorage:', e);
            return null;
        }
    }

    function writeDraftToStorage() {
        try {
            globalThis.localStorage.setItem(STORAGE_KEY, JSON.stringify({ draft: settings }));
        } catch (e) {
            console.warn('Не удалось сохранить черновик в localStorage:', e);
        }
    }

    function clearDraftStorage() {
        try {
            globalThis.localStorage.removeItem(STORAGE_KEY);
        } catch (e) {
            console.warn('Не удалось очистить черновик в localStorage:', e);
        }
    }

    function markDirty() {
        isDirty = true;
        writeDraftToStorage();
        renderDirtyBanner();
    }

    function markClean(newServerSnapshot) {
        isDirty = false;
        serverSnapshot = deepClone(newServerSnapshot);
        clearDraftStorage();
        renderDirtyBanner();
    }

    function renderDirtyBanner() {
        if (!dirtyEl) return;
        if (isDirty) {
            dirtyEl.hidden = false;
            dirtyEl.textContent = '● Черновик не отправлен в OBS. Нажмите «Применить к OBS» или «С сервера», чтобы отбросить.';
        } else {
            dirtyEl.hidden = true;
            dirtyEl.textContent = '';
        }
    }

    function showError(message) {
        if (!errorEl) return;
        errorEl.hidden = false;
        errorEl.textContent = message;
    }

    function applySettings() {
        if (!settings) return;

        const root = document.documentElement;
        Object.keys(cssVarMap).forEach(key => {
            if (settings[key] === undefined || settings[key] === null) return;
            const entry = cssVarMap[key];
            root.style.setProperty(entry[0], entry[1](settings[key]));
        });
        root.style.setProperty(fontWeightVar, settings.fontBold ? 'bold' : 'normal');

        classToggles.forEach(toggle => {
            const key = toggle[0];
            const selector = toggle[1];
            const className = toggle[2];
            const enabled = settings[key];
            document.querySelectorAll(selector).forEach(el => {
                if (enabled) el.classList.remove(className);
                else el.classList.add(className);
            });
        });
    }

    function buildColorControls() {
        colorsContainer.replaceChildren();
        colorFields.forEach(field => {
            const key = field.key;
            const row = document.createElement('div');
            row.className = 'demo-color-row';

            const labelEl = document.createElement('span');
            labelEl.textContent = field.label;
            row.appendChild(labelEl);

            const swatch = document.createElement('span');
            swatch.className = 'demo-color-swatch';
            swatch.id = 'cfg-' + key + '-swatch';
            row.appendChild(swatch);

            const colorInput = document.createElement('input');
            colorInput.type = 'color';
            colorInput.id = 'cfg-' + key;
            colorInput.title = 'Цвет (RGB)';
            row.appendChild(colorInput);

            const alphaInput = document.createElement('input');
            alphaInput.type = 'range';
            alphaInput.id = 'cfg-' + key + '-alpha';
            alphaInput.min = '0';
            alphaInput.max = '255';
            alphaInput.title = 'Прозрачность (alpha)';
            row.appendChild(alphaInput);

            colorsContainer.appendChild(row);

            const sync = () => {
                const current = settings[key];
                if (!current) return;
                const hex = colorToHex(current);
                if (colorInput.value.toLowerCase() !== hex) colorInput.value = hex;
                if (alphaInput.value !== String(current.a)) alphaInput.value = String(current.a);
                swatch.style.setProperty('--swatch-color', colorToCss(current));
            };

            colorInput.addEventListener('input', () => {
                const hex = colorInput.value.slice(1);
                settings[key] = {
                    a: settings[key].a,
                    r: Number.parseInt(hex.slice(0, 2), 16),
                    g: Number.parseInt(hex.slice(2, 4), 16),
                    b: Number.parseInt(hex.slice(4, 6), 16),
                };
                onChange();
                sync();
            });

            alphaInput.addEventListener('input', () => {
                settings[key] = { ...settings[key], a: Number.parseInt(alphaInput.value, 10) };
                onChange();
                sync();
            });

            sync();
        });
    }

    function populateAnimationSelects() {
        document.querySelectorAll('select[data-kind]').forEach(sel => {
            const list = sel.dataset.kind === 'exit' ? animations.exit : animations.entry;
            sel.replaceChildren();
            list.forEach(opt => {
                const node = document.createElement('option');
                node.value = opt.value;
                node.textContent = opt.label;
                sel.appendChild(node);
            });
        });
    }

    function applySchemaToSliders() {
        rangeFields.forEach(pair => {
            const id = pair[0];
            const settingKey = pair[1];
            const el = document.getElementById(id);
            if (!el) return;
            const range = schema[settingKey];
            if (!range) {
                console.warn('Схема не содержит диапазона для', settingKey);
                return;
            }
            el.min = String(range.min);
            el.max = String(range.max);
            el.step = String(range.step);
        });
    }

    function bindControl(id, settingKey, kind) {
        const el = document.getElementById(id);
        if (!el) return;

        if (kind === 'range' || kind === 'number') {
            el.value = String(settings[settingKey]);
            const valueLabel = document.getElementById(id + '-value');
            const suffix = valueLabel ? valueLabel.dataset.suffix || '' : '';
            const sync = () => {
                if (valueLabel) valueLabel.textContent = settings[settingKey] + suffix;
            };
            sync();
            el.addEventListener('input', () => {
                const n = Number(el.value);
                if (Number.isFinite(n)) {
                    settings[settingKey] = Math.round(n);
                    sync();
                    onChange();
                }
            });
        } else if (kind === 'checkbox') {
            el.checked = Boolean(settings[settingKey]);
            el.addEventListener('change', () => {
                settings[settingKey] = el.checked;
                onChange();
            });
        } else if (kind === 'text') {
            el.value = String(settings[settingKey]);
            el.addEventListener('input', () => {
                settings[settingKey] = el.value;
                onChange();
            });
        } else if (kind === 'select') {
            el.value = String(settings[settingKey]);
            el.addEventListener('change', () => {
                settings[settingKey] = el.value;
                onChange();
            });
        } else if (kind === 'flags') {
            const selected = String(settings[settingKey] || '')
                .split(/[,\s]+/)
                .filter(name => name.length > 0);
            Array.from(el.options).forEach(option => {
                option.selected = selected.indexOf(option.value) !== -1;
            });
            el.addEventListener('change', () => {
                const names = Array.from(el.selectedOptions).map(option => option.value);
                settings[settingKey] = names.length > 0 ? names.join(', ') : 'None';
                onChange();
            });
        } else if (kind === 'lines') {
            const current = Array.isArray(settings[settingKey]) ? settings[settingKey] : [];
            el.value = current.join('\n');
            el.addEventListener('input', () => {
                settings[settingKey] = el.value
                    .split(/[\r\n,;\s]+/)
                    .map(line => line.trim())
                    .filter(line => line.length > 0);
                onChange();
            });
        }
    }

    function bindAll() {
        bindControl('cfg-fontFamily', 'fontFamily', 'text');
        bindControl('cfg-fontSize', 'fontSize', 'range');
        bindControl('cfg-fontBold', 'fontBold', 'checkbox');
        bindControl('cfg-padding', 'padding', 'range');
        bindControl('cfg-margin', 'margin', 'range');
        bindControl('cfg-borderRadius', 'borderRadius', 'range');
        bindControl('cfg-maxMessages', 'maxMessages', 'range');
        bindControl('cfg-showTimestamp', 'showTimestamp', 'checkbox');
        bindControl('cfg-emoteSizePixels', 'emoteSizePixels', 'range');
        bindControl('cfg-badgeSizePixels', 'badgeSizePixels', 'range');
        bindControl('cfg-showUserAvatars', 'showUserAvatars', 'checkbox');
        bindControl('cfg-userAvatarSizePixels', 'userAvatarSizePixels', 'range');
        bindControl('cfg-showMessageImages', 'showMessageImages', 'checkbox');
        bindControl('cfg-messageImageRoles', 'messageImageRoles', 'flags');
        bindControl('cfg-messageImageAllowedHosts', 'messageImageAllowedHosts', 'lines');
        bindControl('cfg-messageImageMaxHeightPixels', 'messageImageMaxHeightPixels', 'range');
        bindControl('cfg-showUserTypeBorders', 'showUserTypeBorders', 'checkbox');
        bindControl('cfg-highlightFirstTimeUsers', 'highlightFirstTimeUsers', 'checkbox');
        bindControl('cfg-highlightMentions', 'highlightMentions', 'checkbox');
        bindControl('cfg-enableMessageShadows', 'enableMessageShadows', 'checkbox');
        bindControl('cfg-enableSpecialEffects', 'enableSpecialEffects', 'checkbox');
        bindControl('cfg-enableSmoothScroll', 'enableSmoothScroll', 'checkbox');
        bindControl('cfg-scrollAnimationDuration', 'scrollAnimationDuration', 'range');
        bindControl('cfg-autoScrollEnabled', 'autoScrollEnabled', 'checkbox');
        bindControl('cfg-scrollToBottomThreshold', 'scrollToBottomThreshold', 'range');
        bindControl('cfg-scrollPauseAfterUserMs', 'scrollPauseAfterUserMs', 'range');
        bindControl('cfg-enableAnimations', 'enableAnimations', 'checkbox');
        bindControl('cfg-animationDuration', 'animationDuration', 'range');
        bindControl('cfg-userMessageAnimation', 'userMessageAnimation', 'select');
        bindControl('cfg-botMessageAnimation', 'botMessageAnimation', 'select');
        bindControl('cfg-systemMessageAnimation', 'systemMessageAnimation', 'select');
        bindControl('cfg-broadcasterMessageAnimation', 'broadcasterMessageAnimation', 'select');
        bindControl('cfg-firstTimeUserMessageAnimation', 'firstTimeUserMessageAnimation', 'select');
        bindControl('cfg-enableMessageFadeOut', 'enableMessageFadeOut', 'checkbox');
        bindControl('cfg-messageLifetimeSeconds', 'messageLifetimeSeconds', 'range');
        bindControl('cfg-fadeOutAnimationType', 'fadeOutAnimationType', 'select');
        bindControl('cfg-fadeOutAnimationDurationMs', 'fadeOutAnimationDurationMs', 'range');
    }

    function refreshControlsFromSettings() {
        Object.keys(settings).forEach(key => {
            const value = settings[key];
            const isColor = value && typeof value === 'object' && 'a' in value;
            if (isColor) {
                const colorInput = document.getElementById('cfg-' + key);
                const alphaInput = document.getElementById('cfg-' + key + '-alpha');
                const swatch = document.getElementById('cfg-' + key + '-swatch');
                if (colorInput) colorInput.value = colorToHex(value);
                if (alphaInput) alphaInput.value = String(value.a);
                if (swatch) swatch.style.setProperty('--swatch-color', colorToCss(value));
                return;
            }
            const el = document.getElementById('cfg-' + key);
            if (!el) return;
            if (el.type === 'checkbox') {
                el.checked = Boolean(value);
            } else if (el.type === 'range' || el.type === 'number') {
                el.value = String(value);
                const lbl = document.getElementById(el.id + '-value');
                if (lbl) lbl.textContent = value + (lbl.dataset.suffix || '');
            } else {
                el.value = String(value);
            }
        });
    }

    function onChange() {
        applySettings();
        if (settingsEqual(settings, serverSnapshot)) {
            isDirty = false;
            clearDraftStorage();
            renderDirtyBanner();
        } else {
            markDirty();
        }
    }

    function setDrawerOpen(open) {
        if (open) {
            drawer.classList.add('open');
            drawer.setAttribute('aria-hidden', 'false');
            toggleButton.classList.add('active');
            toggleButton.setAttribute('aria-pressed', 'true');
            document.body.classList.add('drawer-open');
        } else {
            drawer.classList.remove('open');
            drawer.setAttribute('aria-hidden', 'true');
            toggleButton.classList.remove('active');
            toggleButton.setAttribute('aria-pressed', 'false');
            document.body.classList.remove('drawer-open');
        }
    }

    function setStatus(text, kind) {
        statusEl.textContent = text;
        statusEl.classList.remove('ok', 'error');
        if (kind) statusEl.classList.add(kind);
    }

    async function loadFromServer() {
        const response = await fetch('/api/chat-settings/raw');
        if (!response.ok) {
            throw new Error('HTTP ' + response.status + ' для /api/chat-settings/raw');
        }
        return response.json();
    }

    async function loadSchema() {
        const response = await fetch('/api/chat-settings/schema');
        if (!response.ok) {
            throw new Error('HTTP ' + response.status + ' для /api/chat-settings/schema');
        }
        return response.json();
    }

    async function loadAnimations() {
        const response = await fetch('/api/animations');
        if (!response.ok) {
            throw new Error('HTTP ' + response.status + ' для /api/animations');
        }
        return response.json();
    }

    function disableDrawer() {
        if (pushButton) pushButton.disabled = true;
        if (revertButton) revertButton.disabled = true;
    }

    async function bootstrap() {
        try {
            const [rawValues, rangesSchema, animationsList] = await Promise.all([
                loadFromServer(),
                loadSchema(),
                loadAnimations(),
            ]);
            serverSnapshot = rawValues;
            schema = rangesSchema;
            animations = animationsList;
        } catch (e) {
            console.error('Bootstrap демки провалился:', e);
            showError('Не удалось загрузить настройки с сервера: ' + (e?.message ? e.message : e)
                + '. Контролы заблокированы — запустите бота или проверьте порт HTTP-сервера.');
            bootstrapFailed = true;
            disableDrawer();
            return;
        }

        const draft = readDraftFromStorage();
        if (draft && typeof draft === 'object') {
            settings = { ...serverSnapshot, ...draft };
            if (!settingsEqual(settings, serverSnapshot)) {
                isDirty = true;
            }
        } else {
            settings = deepClone(serverSnapshot);
        }

        populateAnimationSelects();
        applySchemaToSliders();
        buildColorControls();
        bindAll();
        refreshControlsFromSettings();
        applySettings();
        observeStageRebuilds();
        renderDirtyBanner();
        subscribeToSse();
    }

    function rebuildAfterServerUpdate(newSnapshot) {
        serverSnapshot = deepClone(newSnapshot);
        if (isDirty) {
            setStatus('Сервер прислал новые настройки — ваш черновик сохранён. Кнопка ↺ загрузит обновлённую версию.', 'ok');
            return;
        }
        settings = deepClone(newSnapshot);
        refreshControlsFromSettings();
        applySettings();
        setStatus('Настройки обновлены сервером.', 'ok');
    }

    function observeStageRebuilds() {
        if (typeof MutationObserver === 'undefined') return;
        const grids = ['entry-grid', 'exit-grid']
            .map(id => document.getElementById(id))
            .filter(grid => grid !== null);
        if (grids.length === 0) return;
        const observer = new MutationObserver(() => applySettings());
        grids.forEach(grid => observer.observe(grid, { childList: true, subtree: true }));
    }

    function subscribeToSse() {
        if (typeof EventSource === 'undefined') return;
        try {
            const source = new EventSource('/events');
            source.addEventListener('chat_settings_changed_raw', function (event) {
                try {
                    const payload = JSON.parse(event.data);
                    rebuildAfterServerUpdate(payload);
                } catch (e) {
                    console.warn('Не удалось разобрать chat_settings_changed_raw:', e);
                }
            });
            source.onerror = function () {
            };
        } catch (e) {
            console.warn('SSE недоступен:', e);
        }
    }

    toggleButton.addEventListener('click', () => setDrawerOpen(!drawer.classList.contains('open')));
    closeButton.addEventListener('click', () => setDrawerOpen(false));

    revertButton.addEventListener('click', async () => {
        if (bootstrapFailed) return;
        const previousLabel = revertButton.textContent;
        revertButton.disabled = true;
        revertButton.textContent = '… загрузка';
        setStatus('Загрузка с сервера…', '');
        try {
            const fresh = await loadFromServer();
            settings = deepClone(fresh);
            markClean(fresh);
            refreshControlsFromSettings();
            applySettings();
            setStatus('Загружено с сервера.', 'ok');
        } catch (e) {
            setStatus('Не удалось загрузить с сервера: ' + (e?.message ? e.message : e), 'error');
        } finally {
            revertButton.disabled = false;
            revertButton.textContent = previousLabel;
        }
    });

    pushButton.addEventListener('click', async () => {
        if (bootstrapFailed) return;
        const previousLabel = pushButton.textContent;
        pushButton.disabled = true;
        pushButton.textContent = '… отправка';
        setStatus('Отправка настроек в OBS…', '');
        try {
            const response = await fetch('/api/chat-settings', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(settings),
            });
            if (!response.ok) {
                let detail = '';
                try {
                    const err = await response.json();
                    detail = err && (err.error || err.details) ? ': ' + (err.error || err.details) : '';
                } catch (jsonError) {
                    console.debug('Не удалось разобрать тело ошибки ответа:', jsonError);
                }
                setStatus('Ошибка ' + response.status + detail, 'error');
                return;
            }
            markClean(settings);
            setStatus('Применено к OBS — оверлей чата обновлён через SSE.', 'ok');
        } catch (e) {
            setStatus('Сеть недоступна или бот не запущен: ' + e.message, 'error');
        } finally {
            pushButton.disabled = false;
            pushButton.textContent = previousLabel;
        }
    });

    bootstrap();
}());
