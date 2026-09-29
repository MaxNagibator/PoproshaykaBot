(function () {
    'use strict';

    const chatContainer = document.getElementById('chat');
    let maxMessages = 50;
    let showTimestamp = true;
    let enableAnimations = true;

    const urlParams = new URLSearchParams(globalThis.location.search);
    const isPreview = urlParams.get('preview') === 'true';

    let showUserTypeBorders = true;
    let highlightFirstTimeUsers = true;
    let isHighlightMentions = true;
    let enableMessageShadows = true;
    let enableSpecialEffects = true;
    let showUserAvatars = false;
    let showMessageImages = false;

    let enableSmoothScroll = true;
    let scrollAnimationDuration = 300;
    let autoScrollEnabled = true;
    let scrollPaused = false;
    let scrollToBottomThreshold = 100;
    let scrollPauseAfterUserMs = 3000;
    let lastScrollTime = 0;
    let scrollAnimationId = null;

    let userMessageAnimation = 'slide-in-right';
    let botMessageAnimation = 'fade-in-up';
    let systemMessageAnimation = 'fade-in-up';
    let broadcasterMessageAnimation = 'slide-in-left';
    let firstTimeUserMessageAnimation = 'bounce-in';

    let enableMessageFadeOut = !isPreview; // Отключаем затухание сразу, если это превью
    let messageLifetime = 30000;
    let fadeOutAnimationType = 'fade-out';
    let fadeOutDuration = 1000;

    const seenMessageIds = new Set();
    const seenMessageIdsOrder = [];
    const seenMessageIdsLimit = 1000;

    let eventSource = null;
    let lastEventTime = Date.now();
    let staleThresholdMs = 70000;
    let reconnecting = false;
    let watchdogIntervalId = null;
    let reconnectTimeoutId = null;
    let reconnectDelayMs = 3000;
    const reconnectMinDelayMs = 3000;
    const reconnectMaxDelayMs = 30000;
    const watchdogTickMs = 10000;

    function markAlive() {
        lastEventTime = Date.now();
    }

    const avatarMemoryCache = new Map();
    const avatarStorageKeyPrefix = 'poproshaykabot.avatar.';
    const avatarStorageTtlMs = 24 * 60 * 60 * 1000;

    function readAvatarFromStorage(userId) {
        try {
            const raw = globalThis.localStorage.getItem(avatarStorageKeyPrefix + userId);
            if (!raw) return null;
            const parsed = JSON.parse(raw);
            if (!parsed || typeof parsed.exp !== 'number' || Date.now() > parsed.exp) {
                globalThis.localStorage.removeItem(avatarStorageKeyPrefix + userId);
                return null;
            }
            return parsed.url || null;
        } catch (e) {
            console.debug('Не удалось прочитать аватар из localStorage', e);
            return null;
        }
    }

    function writeAvatarToStorage(userId, url) {
        try {
            globalThis.localStorage.setItem(
                avatarStorageKeyPrefix + userId,
                JSON.stringify({ url: url || '', exp: Date.now() + avatarStorageTtlMs })
            );
        } catch (e) {
            console.debug('localStorage недоступен или переполнен', e);
        }
    }

    function resolveAvatar(userId) {
        if (!userId) return Promise.resolve(null);

        if (avatarMemoryCache.has(userId)) {
            return avatarMemoryCache.get(userId);
        }

        const stored = readAvatarFromStorage(userId);
        if (stored !== null) {
            const resolved = Promise.resolve(stored || null);
            avatarMemoryCache.set(userId, resolved);
            return resolved;
        }

        const promise = fetch('/api/user-avatar?id=' + encodeURIComponent(userId))
            .then(response => {
                if (response.status === 404) {
                    writeAvatarToStorage(userId, '');
                    return null;
                }
                if (!response.ok) {
                    return null;
                }
                return response.json().then(payload => {
                    const url = payload?.url ?? null;
                    if (url) writeAvatarToStorage(userId, url);
                    return url;
                });
            })
            .catch(() => null);

        avatarMemoryCache.set(userId, promise);
        return promise;
    }

    function attachAvatar(messageDiv, userId) {
        const placeholder = messageDiv.querySelector('.avatar[data-user-id="' + escapeAttr(userId) + '"]');
        if (!placeholder) return;

        resolveAvatar(userId).then(url => {
            if (!url) {
                placeholder.classList.add('avatar-missing');
                return;
            }
            placeholder.src = url;
            placeholder.classList.add('avatar-loaded');
        });
    }

    function renderAvatarPlaceholder(userId) {
        if (!showUserAvatars || !userId) return '';
        const safeId = escapeAttr(userId);
        return `<img class="avatar" data-user-id="${safeId}" alt="" src="" loading="lazy">`;
    }

    const fadeTimers = new WeakMap();

    const newMessagesIndicator = document.getElementById('new-messages-indicator');
    const newMessagesIndicatorCount = document.getElementById('new-messages-indicator-count');
    let pendingNewMessages = 0;

    function rememberMessageId(id) {
        if (!id) return false;
        if (seenMessageIds.has(id)) return true;
        seenMessageIds.add(id);
        seenMessageIdsOrder.push(id);
        if (seenMessageIdsOrder.length > seenMessageIdsLimit) {
            const evicted = seenMessageIdsOrder.shift();
            seenMessageIds.delete(evicted);
        }
        return false;
    }

    function smoothScrollToBottom(force = false) {
        if (!force && (!autoScrollEnabled || scrollPaused)) return;

        const currentTime = Date.now();
        if (currentTime - lastScrollTime < 16) return; // Throttle to ~60fps
        lastScrollTime = currentTime;

        if (scrollAnimationId) {
            cancelAnimationFrame(scrollAnimationId);
        }

        if (enableSmoothScroll && 'scrollBehavior' in document.documentElement.style) {
            chatContainer.scrollTo({
                top: chatContainer.scrollHeight,
                behavior: 'smooth'
            });
        } else {
            const startTop = chatContainer.scrollTop;
            const targetTop = chatContainer.scrollHeight - chatContainer.clientHeight;
            const distance = targetTop - startTop;

            if (Math.abs(distance) < 1) {
                chatContainer.scrollTop = targetTop;
                return;
            }

            const startTime = performance.now();

            function animateScroll(currentTime) {
                const elapsed = currentTime - startTime;
                const progress = Math.min(elapsed / scrollAnimationDuration, 1);

                const easeOut = 1 - Math.pow(1 - progress, 3);

                chatContainer.scrollTop = startTop + distance * easeOut;

                if (progress < 1) {
                    scrollAnimationId = requestAnimationFrame(animateScroll);
                } else {
                    scrollAnimationId = null;
                }
            }

            scrollAnimationId = requestAnimationFrame(animateScroll);
        }
    }

    function isNearBottom() {
        return chatContainer.scrollHeight - chatContainer.scrollTop - chatContainer.clientHeight <= scrollToBottomThreshold;
    }

    function handleUserScroll() {
        if (!isNearBottom()) {
            scrollPaused = true;
            if (scrollPauseAfterUserMs > 0) {
                setTimeout(() => {
                    scrollPaused = false;
                }, scrollPauseAfterUserMs);
            }
            return;
        }

        scrollPaused = false;
        hideNewMessagesIndicator();
    }

    function showNewMessagesIndicator() {
        if (!newMessagesIndicator) return;
        newMessagesIndicator.hidden = false;
        newMessagesIndicator.classList.add('visible');
    }

    function hideNewMessagesIndicator() {
        if (!newMessagesIndicator) return;
        newMessagesIndicator.classList.remove('visible');
        pendingNewMessages = 0;
        if (newMessagesIndicatorCount) {
            newMessagesIndicatorCount.textContent = '0';
        }
    }

    function bumpNewMessagesIndicator() {
        if (!newMessagesIndicator) return;
        pendingNewMessages += 1;
        if (newMessagesIndicatorCount) {
            newMessagesIndicatorCount.textContent = String(pendingNewMessages);
        }
        showNewMessagesIndicator();
    }

    if (chatContainer) {
        let scrollTimeout;
        chatContainer.addEventListener('scroll', () => {
            clearTimeout(scrollTimeout);
            scrollTimeout = setTimeout(handleUserScroll, 50);
        }, { passive: true });
    }

    if (newMessagesIndicator) {
        newMessagesIndicator.addEventListener('click', () => {
            hideNewMessagesIndicator();
            scrollPaused = false;
            smoothScrollToBottom(true);
        });
    }

    function getUserTypeClasses(userStatus) {
        const classes = [];

        if (userStatus & 1) classes.push('broadcaster');
        if (userStatus & 2) classes.push('moderator');
        if (userStatus & 4) classes.push('vip');
        if (userStatus & 8) classes.push('subscriber');

        return classes;
    }

    const validEntryAnimations = new Set([
        'no-animation',
        'slide-in-right',
        'slide-in-left',
        'fade-in-up',
        'bounce-in',
        'pop-in',
        'rubber-band',
        'tada',
        'zoom-blur-in',
        'neon-pulse-in',
        'materialize',
        'glitch-in',
        'power-up',
        'slam-in',
        'notification-bell',
        'hologram',
        'flip-in-x',
        'roll-in',
        'coin-flip',
        'swoop-in',
    ]);

    function sanitizeEntryAnimation(value, fallback) {
        return validEntryAnimations.has(value) ? value : fallback;
    }

    function getAnimationType(userStatus, messageType, isFirstTime = false) {
        let raw;
        if (isFirstTime) {
            raw = firstTimeUserMessageAnimation;
        } else {
            switch (messageType) {
                case 'BotResponse':
                    raw = botMessageAnimation;
                    break;
                case 'SystemNotification':
                    raw = systemMessageAnimation;
                    break;
                case 'UserMessage':
                    raw = userStatus & 1 ? broadcasterMessageAnimation : userMessageAnimation;
                    break;
                default:
                    raw = userMessageAnimation;
            }
        }
        return sanitizeEntryAnimation(raw, 'slide-in-right');
    }

    function initEventSource() {
        if (eventSource) {
            try {
                eventSource.close();
            } catch (e) { /* noop */
            }
        }

        const es = new EventSource('/events');
        eventSource = es;

        es.onopen = function () {
            reconnectDelayMs = reconnectMinDelayMs;
            reconnecting = false;
            markAlive();
            showReloadBanner();
        };

        es.addEventListener('message', function (event) {
            markAlive();
            try {
                addMessage(JSON.parse(event.data));
            } catch (e) {
                console.error('Ошибка парсинга SSE message:', e);
            }
        });

        es.addEventListener('clear', function () {
            markAlive();
            clearChat();
        });

        es.addEventListener('chat_settings_changed', function (event) {
            markAlive();
            try {
                updateChatSettings(JSON.parse(event.data));
            } catch (e) {
                console.error('Ошибка парсинга SSE chat_settings_changed:', e);
            }
        });

        es.addEventListener('ping', function (event) {
            markAlive();
            try {
                const payload = JSON.parse(event.data);
                if (typeof payload.intervalSeconds === 'number' && payload.intervalSeconds > 0) {
                    staleThresholdMs = Math.max(70000, payload.intervalSeconds * 1000 * 2 + 10000);
                }
            } catch (e) {
                console.debug('Не удалось разобрать ping SSE:', e);
            }
        });

        es.addEventListener('dropped', function (event) {
            markAlive();
            try {
                const payload = JSON.parse(event.data);
                console.warn(
                    'Сервер сбросил сообщения SSE. Глобальная очередь:',
                    payload.count,
                    '| По клиентам:',
                    payload.clientCount);
            } catch (e) {
                console.warn('Сервер сбросил сообщения SSE (детали недоступны).', e);
            }
        });

        es.onerror = function (event) {
            console.error('SSE ошибка:', event);
            if (es === eventSource && es.readyState === EventSource.CLOSED) {
                scheduleReconnect();
            }
        };
    }

    const validExitAnimations = new Set([
        'fade-out',
        'slide-out-left',
        'slide-out-right',
        'scale-down',
        'shrink-up',
        'dissolve',
        'slide-out-up',
        'slide-out-down',
        'rotate-out',
        'pixelate-out',
    ]);

    function fadeOutMessage(messageDiv) {
        if (!enableMessageFadeOut) return;
        if (messageDiv.dataset.fading === 'true') return;
        messageDiv.dataset.fading = 'true';

        if (fadeOutAnimationType === 'no-animation' || !validExitAnimations.has(fadeOutAnimationType)) {
            setTimeout(() => messageDiv.remove(), 100);
            return;
        }

        messageDiv.dataset.exitAnim = fadeOutAnimationType;

        setTimeout(() => messageDiv.remove(), fadeOutDuration);
    }

    function cancelFadeOut(messageDiv) {
        const id = fadeTimers.get(messageDiv);
        if (id !== undefined) {
            clearTimeout(id);
            fadeTimers.delete(messageDiv);
        }
    }

    function scheduleFadeOut(messageDiv) {
        cancelFadeOut(messageDiv);
        if (!enableMessageFadeOut) return;
        if (messageDiv.dataset.fading === 'true') return;

        const createdAt = Number.parseInt(messageDiv.dataset.createdAt, 10) || Date.now();
        const remaining = Math.max(0, messageLifetime - (Date.now() - createdAt));

        const id = setTimeout(() => {
            fadeTimers.delete(messageDiv);
            fadeOutMessage(messageDiv);
        }, remaining);
        fadeTimers.set(messageDiv, id);
    }

    function rescheduleAllFades() {
        document.querySelectorAll('#chat .message').forEach(messageDiv => {
            if (enableMessageFadeOut) {
                scheduleFadeOut(messageDiv);
            } else {
                cancelFadeOut(messageDiv);
            }
        });
    }

    function applyEntryAnimation(messageDiv, message, isHistoryMessage) {
        if (isHistoryMessage || !enableAnimations) {
            messageDiv.classList.add('no-animation', 'entry-done');
            return;
        }

        const animationType = getAnimationType(message.status || 0, message.messageType, message.isFirstTime);
        messageDiv.classList.add(animationType);

        const onEntryEnd = event => {
            if (event.target !== messageDiv) return;
            messageDiv.removeEventListener('animationend', onEntryEnd);
            messageDiv.classList.add('entry-done');
        };
        messageDiv.addEventListener('animationend', onEntryEnd);
    }

    function applySpecialEffects(messageDiv, message) {
        if (!enableSpecialEffects) {
            messageDiv.classList.add('no-special-effects');
            return;
        }

        const isBroadcasterOrVip = message.status & 1 || message.status & 4;
        if (isBroadcasterOrVip) {
            messageDiv.classList.add('special-effect');
        }
    }

    function applyFirstTimeMarker(messageDiv, message) {
        if (!message.isFirstTime) {
            return;
        }

        if (highlightFirstTimeUsers) {
            messageDiv.classList.add('first-time');
        } else {
            messageDiv.classList.add('first-time', 'no-first-time-effects');
        }
    }

    function applyToggleClasses(messageDiv) {
        const toggles = [
            [showUserTypeBorders, 'no-borders'],
            [enableMessageShadows, 'no-shadows'],
            [isHighlightMentions, 'no-mentions'],
            [showTimestamp, 'no-timestamp'],
            [showUserAvatars, 'no-avatars'],
            [showMessageImages, 'no-message-images'],
        ];
        toggles.forEach(([enabled, className]) => {
            if (!enabled) messageDiv.classList.add(className);
        });
    }

    function addMessage(message, isHistoryMessage = false) {
        if (rememberMessageId(message.messageId)) {
            return;
        }

        const messageDiv = document.createElement('div');
        messageDiv.className = 'message';

        const createdAtMs = isHistoryMessage
            ? new Date(message.timestamp).getTime()
            : Date.now();
        messageDiv.dataset.createdAt = String(createdAtMs);

        applyEntryAnimation(messageDiv, message, isHistoryMessage);

        const userTypeClasses = getUserTypeClasses(message.status || 0);
        userTypeClasses.forEach(cls => messageDiv.classList.add(cls));

        applyToggleClasses(messageDiv);
        applySpecialEffects(messageDiv, message);
        applyFirstTimeMarker(messageDiv, message);

        const timestampDate = new Date(message.timestamp);
        const timestampLabel = timestampDate.toLocaleTimeString();
        const timestampIso = Number.isNaN(timestampDate.getTime()) ? '' : timestampDate.toISOString();
        const isSystemMessage = message.messageType !== 'UserMessage';

        const timestampHtml = `<time class="timestamp" datetime="${escapeAttr(timestampIso)}">${escapeHtml(timestampLabel)}</time>`;

        if (isSystemMessage) {
            messageDiv.innerHTML = `
            ${timestampHtml}
            <span class="system-message">${escapeHtml(message.message)}</span>
        `;
        } else {
            const badgesHtml = renderBadges(message.badges || []);
            const messageText = String(message.message ?? '');
            const image = pickMessageImage(message.images, message.imageLinks, messageText);
            const messageWithEmotes = renderMessageWithEmotes(messageText, message.emotes || [], image);
            const avatarHtml = renderAvatarPlaceholder(message.userId);

            const usernameClasses = ['username', ...userTypeClasses].join(' ');
            const usernameColor = sanitizeUsernameColor(message.color);
            const usernameStyle = usernameColor ? ` style="color: ${usernameColor}"` : '';
            const usernameHtml = `<span class="${usernameClasses}"${usernameStyle}>${escapeHtml(message.displayName || message.username || '')}</span>`;

            const headerHtml = `
            <div class="message-header">
                ${timestampHtml}
                ${badgesHtml}
                ${usernameHtml}
            </div>`;

            const textClasses = image && image.linkOnly ? 'message-text message-text-link-only' : 'message-text';
            const bodyHtml = `<div class="${textClasses}">${messageWithEmotes}</div>`;
            const imagesHtml = renderMessageImage(image);

            const contentHtml = `
            <div class="message-content">
                ${headerHtml}
                ${bodyHtml}
                ${imagesHtml}
            </div>`;

            if (avatarHtml) {
                messageDiv.innerHTML = `
            ${avatarHtml}
            ${contentHtml}
        `;

                attachAvatar(messageDiv, message.userId);
            } else {
                messageDiv.innerHTML = contentHtml;
            }

            attachMessageImage(messageDiv);
        }

        chatContainer.appendChild(messageDiv);

        scheduleFadeOut(messageDiv);

        while (chatContainer.children.length > maxMessages) {
            const removed = chatContainer.firstChild;
            if (removed) {
                cancelFadeOut(removed);
                removed.remove();
            }
        }

        if (isHistoryMessage) {
            chatContainer.scrollTop = chatContainer.scrollHeight;
            return;
        }

        if (scrollPaused && autoScrollEnabled) {
            bumpNewMessagesIndicator();
            return;
        }

        requestAnimationFrame(() => smoothScrollToBottom());
    }

    function clearChat() {
        chatContainer.replaceChildren();
        seenMessageIds.clear();
        seenMessageIdsOrder.length = 0;
        scrollPaused = false;
        hideNewMessagesIndicator();
    }

    function renderBadges(badges) {
        if (!badges || badges.length === 0) return '';

        return badges.map(badge => {
            if (!badge.imageUrl) return '';
            const src = escapeAttr(badge.imageUrl);
            const type = escapeAttr(badge.type);
            const version = escapeAttr(badge.version);
            return `<img src="${src}" alt="${type}" title="${type} ${version}" class="badge">`;
        }).join('');
    }

    const messageImageProxyPrefix = '/api/image?';

    function pickMessageImage(images, imageLinks, text) {
        if (!showMessageImages || !Array.isArray(images) || images.length === 0) {
            return null;
        }

        const url = images[0];

        if (typeof url !== 'string' || !url.startsWith(messageImageProxyPrefix)) {
            return null;
        }

        const range = Array.isArray(imageLinks) ? imageLinks[0] : null;

        if (!range || !isValidRange(range.startIndex, range.endIndex, text.length)) {
            return { url, link: null, linkOnly: false };
        }

        const link = { startIndex: range.startIndex, endIndex: range.endIndex };
        const linkOnly = text.substring(0, link.startIndex).trim() === ''
            && text.substring(link.endIndex + 1).trim() === '';

        return { url, link, linkOnly };
    }

    function isValidRange(startIndex, endIndex, length) {
        return Number.isInteger(startIndex) && Number.isInteger(endIndex)
            && startIndex >= 0 && endIndex >= startIndex && endIndex < length;
    }

    function renderMessageImage(image) {
        if (!image) {
            return '';
        }

        return `<div class="message-images"><img class="message-image" src="${escapeAttr(image.url)}" alt="" loading="lazy"></div>`;
    }

    function attachMessageImage(messageDiv) {
        const image = messageDiv.querySelector('.message-image');

        if (!image) {
            return;
        }

        image.addEventListener('error', () => {
            const container = image.closest('.message-images');
            if (container) container.remove();
        }, { once: true });

        image.addEventListener('load', () => {
            image.classList.add('message-image-loaded');
            messageDiv.classList.add('has-message-image');
        }, { once: true });
    }

    function renderMessageWithEmotes(message, emotes, image) {
        const fragments = [];

        for (const emote of emotes) {
            if (!emote.imageUrl) {
                continue;
            }

            if (!isValidRange(emote.startIndex, emote.endIndex, message.length)) {
                console.warn('Пропущен эмоут с некорректными индексами:', emote);
                continue;
            }

            const animatedClass = enableSpecialEffects && isAnimatedEmote(emote.name) ? ' animated' : '';
            const src = escapeAttr(emote.imageUrl);
            const name = escapeAttr(emote.name);

            fragments.push({
                start: emote.startIndex,
                end: emote.endIndex,
                html: `<img src="${src}" alt="${name}" title="${name}" class="emote${animatedClass}">`,
            });
        }

        const link = image && image.link;

        if (link) {
            const linkText = escapeHtml(message.substring(link.startIndex, link.endIndex + 1));

            fragments.push({
                start: link.startIndex,
                end: link.endIndex,
                html: `<span class="message-image-link">${linkText}</span>`,
            });
        }

        fragments.sort((a, b) => a.start - b.start);

        let html = '';
        let cursor = 0;

        for (const fragment of fragments) {
            if (fragment.start < cursor) {
                continue;
            }

            html += highlightMentions(escapeHtml(message.substring(cursor, fragment.start))) + fragment.html;
            cursor = fragment.end + 1;
        }

        return html + highlightMentions(escapeHtml(message.substring(cursor)));
    }

    function isAnimatedEmote(emoteName) {
        const animatedEmotes = ['Kappa', 'PogChamp', 'KEKW', 'EZ', 'PauseChamp'];
        return animatedEmotes.includes(emoteName);
    }

    function highlightMentions(text) {
        if (!isHighlightMentions) return text;

        return text.replace(/@(\w+)/g, '<span class="mention">@$1</span>');
    }

    const usernameColorPattern = /^#[0-9a-fA-F]{6}$/;

    function sanitizeUsernameColor(value) {
        if (typeof value !== 'string') return '';
        const trimmed = value.trim();
        return usernameColorPattern.test(trimmed) ? trimmed : '';
    }

    function escapeAttr(value) {
        return String(value ?? '')
            .replaceAll('&', '&amp;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;');
    }

    function escapeHtml(text) {
        return text.replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    const cssVariableMap = [
        ['backgroundColor', '--chat-bg-color'],
        ['textColor', '--chat-text-color'],
        ['usernameColor', '--chat-username-color'],
        ['systemMessageColor', '--chat-system-color'],
        ['timestampColor', '--chat-timestamp-color'],
        ['fontFamily', '--chat-font-family'],
        ['fontSize', '--chat-font-size'],
        ['fontWeight', '--chat-font-weight'],
        ['padding', '--chat-padding'],
        ['margin', '--chat-margin'],
        ['borderRadius', '--chat-border-radius'],
        ['animationDuration', '--chat-animation-duration'],
        ['emoteSize', '--emote-size'],
        ['badgeSize', '--badge-size'],
        ['userAvatarSize', '--avatar-size'],
        ['messageImageMaxHeight', '--message-image-max-height'],
    ];

    const animationFieldMap = {
        userMessageAnimation: v => userMessageAnimation = v,
        botMessageAnimation: v => botMessageAnimation = v,
        systemMessageAnimation: v => systemMessageAnimation = v,
        broadcasterMessageAnimation: v => broadcasterMessageAnimation = v,
        firstTimeUserMessageAnimation: v => firstTimeUserMessageAnimation = v,
    };

    function toggleNegationClass(selector, className, enabled) {
        document.querySelectorAll(selector).forEach(message => {
            if (enabled) {
                message.classList.remove(className);
            } else {
                message.classList.add(className);
            }
        });
    }

    function parseSettings(settings) {
        if (typeof settings !== 'string') {
            return settings;
        }
        try {
            return JSON.parse(settings);
        } catch (e) {
            console.error('Некорректные настройки чата (ожидался объект):', e);
            return null;
        }
    }

    function applyCssVariables(settings, root) {
        cssVariableMap.forEach(([key, cssVar]) => {
            if (settings[key]) {
                root.style.setProperty(cssVar, settings[key]);
            }
        });
    }

    function applyClassToggleSettings(settings) {
        const toggles = [
            ['showUserTypeBorders', '.message', 'no-borders', v => showUserTypeBorders = v, () => showUserTypeBorders],
            ['highlightFirstTimeUsers', '.message.first-time', 'no-first-time-effects', v => highlightFirstTimeUsers = v, () => highlightFirstTimeUsers],
            ['highlightMentions', '.message', 'no-mentions', v => isHighlightMentions = v, () => isHighlightMentions],
            ['enableMessageShadows', '.message', 'no-shadows', v => enableMessageShadows = v, () => enableMessageShadows],
            ['enableSpecialEffects', '.message', 'no-special-effects', v => enableSpecialEffects = v, () => enableSpecialEffects],
            ['showTimestamp', '#chat .message', 'no-timestamp', v => showTimestamp = v, () => showTimestamp],
            ['showUserAvatars', '.message', 'no-avatars', v => showUserAvatars = v, () => showUserAvatars],
            ['showMessageImages', '.message', 'no-message-images', v => showMessageImages = v, () => showMessageImages],
        ];

        toggles.forEach(([key, selector, className, setter, getter]) => {
            if (settings[key] !== undefined) {
                setter(settings[key]);
                toggleNegationClass(selector, className, getter());
            }
        });
    }

    function applyAnimationSettings(settings) {
        if (settings.enableAnimations !== undefined) enableAnimations = settings.enableAnimations;

        Object.keys(animationFieldMap).forEach(key => {
            if (settings[key]) animationFieldMap[key](settings[key]);
        });
    }

    function applyScrollSettings(settings) {
        if (settings.enableSmoothScroll !== undefined) enableSmoothScroll = settings.enableSmoothScroll;
        if (settings.scrollAnimationDuration !== undefined) scrollAnimationDuration = settings.scrollAnimationDuration;
        if (settings.autoScrollEnabled !== undefined) autoScrollEnabled = settings.autoScrollEnabled;
        if (settings.scrollToBottomThreshold !== undefined) scrollToBottomThreshold = settings.scrollToBottomThreshold;
        if (settings.scrollPauseAfterUserMs !== undefined) scrollPauseAfterUserMs = settings.scrollPauseAfterUserMs;
    }

    function applyFadeSettings(settings, root) {
        let fadeRescheduleNeeded = false;

        if (settings.enableMessageFadeOut !== undefined) {
            const next = isPreview ? false : settings.enableMessageFadeOut;
            if (next !== enableMessageFadeOut) fadeRescheduleNeeded = true;
            enableMessageFadeOut = next;
        }
        if (settings.messageLifetimeSeconds !== undefined) {
            const next = settings.messageLifetimeSeconds * 1000;
            if (next !== messageLifetime) fadeRescheduleNeeded = true;
            messageLifetime = next;
        }
        if (settings.fadeOutAnimationType !== undefined) fadeOutAnimationType = settings.fadeOutAnimationType;
        if (settings.fadeOutAnimationDurationMs !== undefined) {
            fadeOutDuration = settings.fadeOutAnimationDurationMs;
            root.style.setProperty('--fade-out-duration', fadeOutDuration + 'ms');
        }

        return fadeRescheduleNeeded;
    }

    function updateChatSettings(settings) {
        settings = parseSettings(settings);
        if (settings === null) {
            return;
        }
        const root = document.documentElement;

        applyCssVariables(settings, root);
        applyClassToggleSettings(settings);

        if (settings.maxMessages !== undefined) maxMessages = settings.maxMessages;

        applyAnimationSettings(settings);
        applyScrollSettings(settings);

        if (applyFadeSettings(settings, root)) {
            rescheduleAllFades();
        }

        console.log('Настройки чата обновлены:', settings);
    }

    const reloadBannerLifetimeMs = 5000;
    const reloadBannerFadeMs = 600;

    function showReloadBanner() {
        const now = new Date();

        const banner = document.createElement('div');
        banner.classList.add('message', 'reload-banner', 'no-animation', 'entry-done');
        banner.style.transition = 'opacity ' + reloadBannerFadeMs + 'ms ease';

        const timeElement = document.createElement('time');
        timeElement.className = 'timestamp';
        timeElement.dateTime = now.toISOString();
        timeElement.textContent = now.toLocaleTimeString();

        const text = document.createElement('span');
        text.className = 'system-message';
        text.textContent = '🔁 Чат-источник перезагружен';

        banner.appendChild(timeElement);
        banner.appendChild(text);
        chatContainer.appendChild(banner);

        requestAnimationFrame(() => smoothScrollToBottom(true));

        setTimeout(() => {
            banner.style.opacity = '0';
            setTimeout(() => banner.remove(), reloadBannerFadeMs);
        }, reloadBannerLifetimeMs);
    }

    function loadHistoryAndBackfill() {
        return fetch('/api/history')
            .then(response => {
                if (!response.ok) {
                    throw new Error('HTTP ' + response.status);
                }
                return response.json();
            })
            .then(messages => messages.forEach(message => addMessage(message, true)));
    }

    function scheduleReconnect() {
        if (reconnecting) {
            return;
        }
        reconnecting = true;

        if (reconnectTimeoutId !== null) {
            clearTimeout(reconnectTimeoutId);
        }

        console.warn('SSE: переподключение оверлея через', reconnectDelayMs, 'мс');
        reconnectTimeoutId = setTimeout(() => {
            reconnectTimeoutId = null;
            reconnecting = false;
            reconnectDelayMs = Math.min(reconnectMaxDelayMs, reconnectDelayMs * 2);
            loadHistoryAndBackfill()
                .catch(error => console.error('Ошибка догрузки истории при реконнекте:', error))
                .finally(() => {
                    initEventSource();
                    markAlive();
                });
        }, reconnectDelayMs);
    }

    function startWatchdog() {
        if (watchdogIntervalId !== null) {
            return;
        }
        watchdogIntervalId = setInterval(() => {
            if (reconnecting) {
                return;
            }
            const stale = Date.now() - lastEventTime > staleThresholdMs;
            const closed = eventSource !== null && eventSource.readyState === EventSource.CLOSED;
            if (stale || closed) {
                scheduleReconnect();
            }
        }, watchdogTickMs);
    }

    function initOverlay() {
        fetch('/api/chat-settings')
            .then(response => response.json())
            .then(settings => updateChatSettings(settings))
            .then(() => loadHistoryAndBackfill())
            .catch(error => console.error('Ошибка инициализации оверлея:', error))
            .finally(() => {
                initEventSource();
                markAlive();
                startWatchdog();
            });
    }

    initOverlay();

}());
