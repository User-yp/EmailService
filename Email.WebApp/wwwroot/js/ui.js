/*
 * 通用格式化函数与共享组件。新增页面时直接复用这些组件，保持展示风格一致。
 */
(function () {
    'use strict';

    const STATUS_LABELS = {
        Draft: '草稿',
        Init: '待发送',
        Sent: '已发送',
        Failed: '失败',
        Retry: '重试中'
    };

    window.EmailFormat = {
        statusLabel: function (status) {
            return STATUS_LABELS[status] || status || '-';
        },
        statusClass: function (status) {
            return 'badge badge-' + String(status || 'init').toLowerCase();
        },
        dateTime: function (value) {
            return value ? new Date(value).toLocaleString('zh-CN', { hour12: false }) : '-';
        },
        date: function (value) {
            if (!value) {
                return '-';
            }
            const date = new Date(value);
            return ('0' + (date.getMonth() + 1)).slice(-2) + '-' + ('0' + date.getDate()).slice(-2)
                + ' ' + ('0' + date.getHours()).slice(-2) + ':' + ('0' + date.getMinutes()).slice(-2);
        },
        size: function (bytes) {
            const value = Number(bytes) || 0;
            if (value < 1024) {
                return value + ' B';
            }
            if (value < 1024 * 1024) {
                return (value / 1024).toFixed(1) + ' KB';
            }
            return (value / 1024 / 1024).toFixed(2) + ' MB';
        },
        recipients: function (list) {
            return list && list.length ? list.join(', ') : '-';
        }
    };

    const StatusBadge = {
        props: { status: { type: String, default: 'Init' } },
        template: '<span :class="cls">{{ label }}</span>',
        computed: {
            cls: function () {
                return window.EmailFormat.statusClass(this.status);
            },
            label: function () {
                return window.EmailFormat.statusLabel(this.status);
            }
        }
    };

    const Pager = {
        props: {
            page: { type: Object, required: true }
        },
        emits: ['go'],
        template: [
            '<div class="pager">',
            '  <span class="muted">共 {{ page.total }} 条 · 第 {{ page.page }} / {{ page.totalPages }} 页</span>',
            '  <span class="pager-actions">',
            '    <button class="btn btn-ghost" :disabled="!page.hasPrevious" @click="$emit(\'go\', page.page - 1)">上一页</button>',
            '    <button class="btn btn-ghost" :disabled="!page.hasNext" @click="$emit(\'go\', page.page + 1)">下一页</button>',
            '  </span>',
            '</div>'
        ].join('')
    };

    const FlashBanner = {
        setup: function () {
            return { store: window.EmailStore };
        },
        template: [
            '<div v-if="store.flash" :class="[\'alert\', \'alert-\' + store.flash.type]\">',
            '  <span>{{ store.flash.text }}</span>',
            '  <button class="alert-close" @click="store.clearFlash()" title="关闭">×</button>',
            '</div>'
        ].join('')
    };

    window.EmailComponents = {
        StatusBadge: StatusBadge,
        Pager: Pager,
        FlashBanner: FlashBanner
    };
})();
