// ==UserScript==
// @name         Discord Widget Config Editor Experiment Override
// @namespace    https://discord.com/developers/
// @version      1.0.0
// @downloadURL  https://raw.githubusercontent.com/konnokai/DiscordWidgetsV2Bot/master/discord-widget-config-editor.user.js
// @updateURL    https://raw.githubusercontent.com/konnokai/DiscordWidgetsV2Bot/master/discord-widget-config-editor.user.js
// @homepageURL  https://github.com/konnokai/DiscordWidgetsV2Bot
// @supportURL   https://github.com/konnokai/DiscordWidgetsV2Bot/issues
// @description  進入 Discord 開發者應用頁面時，自動啟用 2026-03-widget-config-editor 實驗覆寫（每次載入只執行一次）
// @author       Konnokai
// @match        https://discord.com/developers/applications/*
// @run-at       document-idle
// @grant        none
// ==/UserScript==

(function () {
    'use strict';

    const OVERRIDE_KEY = '2026-03-widget-config-editor';
    const OVERRIDE_VALUE = 1;
    const TAG = '[WidgetConfigEditor]';

    let done = false;

    function findByProps(mods, ...props) {
        for (const m of Object.values(mods)) {
            try {
                if (!m.exports || m.exports === window) continue;
                if (props.every((x) => m.exports?.[x])) return m.exports;

                for (const ex in m.exports) {
                    if (
                        props.every((x) => m.exports?.[ex]?.[x]) &&
                        m.exports[ex][Symbol.toStringTag] !== 'IntlMessagesProxy'
                    ) {
                        return m.exports[ex];
                    }
                }
            } catch {}
        }
    }

    function tryApply() {
        if (done) return true;

        const chunk = window.webpackChunkdiscord_developers;
        if (!chunk) return false;

        try {
            const _mods = chunk.push([[Symbol()], {}, (r) => r.c]);
            chunk.pop();

            const store = findByProps(_mods, 'getAll');
            if (!store || typeof store.getAll !== 'function') return false;

            const experimentStore = store
                .getAll()
                .find((e) => e.getName?.() === 'ApexExperimentStore');
            if (!experimentStore) return false;

            experimentStore.createOverride(OVERRIDE_KEY, OVERRIDE_VALUE);
            done = true;
            console.log(`${TAG} override 已套用：${OVERRIDE_KEY} = ${OVERRIDE_VALUE}`);
            return true;
        } catch (e) {
            console.warn(`${TAG} 尚未就緒，稍後重試…`, e);
            return false;
        }
    }

    // 輪詢直到 webpack / ApexExperimentStore 準備好，成功後即停止（因此每次頁面載入只會實際執行一次）
    if (tryApply()) return;

    const interval = setInterval(() => {
        if (tryApply()) clearInterval(interval);
    }, 500);

    // 安全上限：30 秒後仍未成功就放棄，避免無限輪詢
    setTimeout(() => clearInterval(interval), 30000);
})();