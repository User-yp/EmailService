/*
 * 后端接口封装 + 轻量全局状态。
 * 所有请求都走同源的 /api/*，因此不需要额外配置代理。
 */
(function () {
    'use strict';

    async function request(url, options) {
        const response = await fetch(url, options);

        if (!response.ok) {
            let message = response.status + ' ' + response.statusText;
            try {
                const body = await response.json();
                message = body.error || body.title || message;
                if (body.hint) {
                    message += '（' + body.hint + '）';
                }
            } catch (ignored) {
                // 非 JSON 响应（例如 500 页面），保留状态码信息即可
            }
            throw new Error(message);
        }

        if (response.status === 204) {
            return null;
        }

        const contentType = response.headers.get('content-type') || '';
        return contentType.indexOf('application/json') >= 0 ? response.json() : response.text();
    }

    function toQuery(params) {
        const search = new URLSearchParams();
        Object.keys(params || {}).forEach(function (key) {
            const value = params[key];
            if (value !== null && value !== undefined && value !== '') {
                search.append(key, value);
            }
        });
        const text = search.toString();
        return text ? '?' + text : '';
    }

    window.EmailApi = {
        dashboard: function () {
            return request('/api/dashboard');
        },
        emails: function (filter) {
            return request('/api/emails' + toQuery(filter));
        },
        email: function (id) {
            return request('/api/emails/' + encodeURIComponent(id));
        },
        send: function (formData) {
            return request('/api/emails', { method: 'POST', body: formData });
        },
        retry: function (id) {
            return request('/api/emails/' + encodeURIComponent(id) + '/retry', { method: 'POST' });
        },
        remove: function (id) {
            return request('/api/emails/' + encodeURIComponent(id), { method: 'DELETE' });
        },
        seed: function (count, append) {
            return request('/api/seed' + toQuery({ count: count, append: append }), { method: 'POST' });
        },
        diagnostics: function () {
            return request('/api/diagnostics');
        },
        attachmentUrl: function (id) {
            return '/api/attachments/' + encodeURIComponent(id);
        }
    };

    // 全局提示：各页面操作后调用 flashXxx，由根组件统一渲染
    const store = Vue.reactive({ flash: null });

    store.flashSuccess = function (text) {
        store.flash = { type: 'success', text: text };
    };
    store.flashWarning = function (text) {
        store.flash = { type: 'warning', text: text };
    };
    store.flashError = function (text) {
        store.flash = { type: 'error', text: text };
    };
    store.clearFlash = function () {
        store.flash = null;
    };

    window.EmailStore = store;
})();
