/*
 * 页面组件。每个页面只负责：调用 /api/* → 渲染；业务规则全部在后端领域/应用层。
 * 新增页面：在这里加一个组件，再到 app.js 的 routes 里注册即可。
 */
(function () {
    'use strict';

    const ref = Vue.ref;
    const computed = Vue.computed;
    const onMounted = Vue.onMounted;
    const watch = Vue.watch;

    /* ------------------------------------------------------------ 概览 */

    const DashboardPage = {
        setup: function () {
            const store = window.EmailStore;
            const format = window.EmailFormat;
            const data = ref(null);
            const loading = ref(true);
            const error = ref('');
            const seedCount = ref(24);
            const seedAppend = ref(false);
            const seeding = ref(false);

            const bars = computed(function () {
                const statistics = (data.value && data.value.statistics) || {};
                const total = statistics.total || 0;
                const percent = function (value) {
                    return total === 0 ? 0 : Math.round((value || 0) * 100 / total);
                };
                const pending = (statistics.init || 0) + (statistics.draft || 0);
                return [
                    { label: '已发送', cls: 'fill-sent', count: statistics.sent || 0, percent: percent(statistics.sent) },
                    { label: '失败', cls: 'fill-failed', count: statistics.failed || 0, percent: percent(statistics.failed) },
                    { label: '重试中', cls: 'fill-retry', count: statistics.retry || 0, percent: percent(statistics.retry) },
                    { label: '待发送', cls: 'fill-init', count: pending, percent: percent(pending) }
                ];
            });

            async function load() {
                loading.value = true;
                error.value = '';
                try {
                    data.value = await window.EmailApi.dashboard();
                } catch (e) {
                    error.value = e.message;
                } finally {
                    loading.value = false;
                }
            }

            async function seed() {
                seeding.value = true;
                try {
                    const result = await window.EmailApi.seed(seedCount.value, seedAppend.value);
                    if (result.skipped) {
                        store.flashWarning(result.message);
                    } else {
                        store.flashSuccess(result.message);
                    }
                    await load();
                } catch (e) {
                    store.flashError(e.message);
                } finally {
                    seeding.value = false;
                }
            }

            onMounted(load);
            return { data, loading, error, bars, seedCount, seedAppend, seeding, load, seed, format };
        },
        template: `
<div>
  <div class="page-header">
    <h1>概览</h1>
    <div class="actions">
      <button class="btn" :disabled="loading" @click="load">刷新</button>
      <router-link class="btn" to="/emails">查看全部邮件</router-link>
      <router-link class="btn btn-primary" to="/compose">发送测试邮件</router-link>
    </div>
  </div>

  <div v-if="error" class="alert alert-error">{{ error }}</div>

  <div v-if="data" class="cards">
    <div class="card stat"><div class="stat-label">邮件总数</div><div class="stat-value">{{ data.statistics.total }}</div></div>
    <div class="card stat"><div class="stat-label">已发送</div><div class="stat-value ok">{{ data.statistics.sent }}</div></div>
    <div class="card stat"><div class="stat-label">发送失败</div><div class="stat-value bad">{{ data.statistics.failed }}</div></div>
    <div class="card stat"><div class="stat-label">重试中</div><div class="stat-value warn">{{ data.statistics.retry }}</div></div>
    <div class="card stat"><div class="stat-label">待发送</div><div class="stat-value">{{ data.statistics.init + data.statistics.draft }}</div></div>
    <div class="card stat"><div class="stat-label">附件总数</div><div class="stat-value">{{ data.statistics.attachmentCount }}</div></div>
    <div class="card stat"><div class="stat-label">发送成功率</div><div class="stat-value">{{ data.statistics.successRate }}%</div></div>
  </div>

  <section class="panel">
    <h2>状态分布</h2>
    <div class="bars">
      <div v-for="bar in bars" :key="bar.label" class="bar-row">
        <span class="bar-label">{{ bar.label }}</span>
        <div class="bar"><div class="bar-fill" :class="bar.cls" :style="{ width: bar.percent + '%' }"></div></div>
        <span class="bar-value">{{ bar.count }}（{{ bar.percent }}%）</span>
      </div>
    </div>
    <p v-if="data && data.statistics.lastSentTime" class="muted">
      最近一次成功发送：{{ format.dateTime(data.statistics.lastSentTime) }}
    </p>
  </section>

  <section class="panel">
    <h2>最近邮件</h2>
    <table class="table">
      <thead>
        <tr><th>主题</th><th>收件人</th><th>状态</th><th>创建时间</th><th></th></tr>
      </thead>
      <tbody>
        <tr v-if="!data || data.recentEmails.length === 0">
          <td colspan="5" class="empty">还没有邮件，可在下方生成测试数据，或直接发送一封测试邮件。</td>
        </tr>
        <tr v-for="item in (data ? data.recentEmails : [])" :key="item.id">
          <td><router-link :to="'/emails/' + item.id">{{ item.subject }}</router-link></td>
          <td class="muted">{{ item.recipients }}</td>
          <td><status-badge :status="item.status"></status-badge></td>
          <td class="muted">{{ format.dateTime(item.createdAt) }}</td>
          <td class="row-actions"><router-link class="btn btn-sm" :to="'/emails/' + item.id">详情</router-link></td>
        </tr>
      </tbody>
    </table>
  </section>

  <section class="panel">
    <h2>测试数据</h2>
    <p class="muted">
      生成包含各种状态（已发送 / 失败 / 重试中 / 待发送）与附件的演示数据，直接写库、<strong>不会真的发信</strong>。
      收件人统一使用 @example.com 保留域名，便于用 SQL 一次性清理。
    </p>
    <div class="inline-form">
      <label>数量 <input type="number" min="1" max="200" v-model.number="seedCount" style="width:90px" /></label>
      <label class="checkbox"><input type="checkbox" v-model="seedAppend" /> 追加生成（忽略「已有数据则跳过」检查）</label>
      <button class="btn btn-primary" :disabled="seeding" @click="seed">{{ seeding ? '生成中…' : '生成测试数据' }}</button>
    </div>
  </section>
</div>`
    };

    /* ------------------------------------------------------------ 邮件列表 */

    const EmailListPage = {
        setup: function () {
            const store = window.EmailStore;
            const format = window.EmailFormat;
            const route = VueRouter.useRoute();
            const router = VueRouter.useRouter();

            const status = ref(route.query.status || '');
            const keyword = ref(route.query.keyword || '');
            const page = ref(Number(route.query.page) || 1);
            const pageSize = ref(Number(route.query.pageSize) || 20);

            const result = ref({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 1, hasNext: false, hasPrevious: false });
            const loading = ref(true);
            const error = ref('');
            const statuses = ['Draft', 'Init', 'Sent', 'Failed', 'Retry'];

            let ignoreNextQueryChange = false;

            function syncUrl() {
                ignoreNextQueryChange = true;
                router.replace({
                    path: '/emails',
                    query: {
                        status: status.value || undefined,
                        keyword: keyword.value || undefined,
                        page: page.value > 1 ? page.value : undefined,
                        pageSize: pageSize.value !== 20 ? pageSize.value : undefined
                    }
                }).finally(function () {
                    ignoreNextQueryChange = false;
                });
            }

            async function load() {
                loading.value = true;
                error.value = '';
                try {
                    result.value = await window.EmailApi.emails({
                        status: status.value,
                        keyword: keyword.value,
                        page: page.value,
                        pageSize: pageSize.value
                    });
                } catch (e) {
                    error.value = e.message;
                } finally {
                    loading.value = false;
                }
            }

            function search() {
                page.value = 1;
                syncUrl();
                load();
            }

            function go(target) {
                page.value = target;
                syncUrl();
                load();
            }

            function reset() {
                status.value = '';
                keyword.value = '';
                pageSize.value = 20;
                search();
            }

            async function retry(id) {
                try {
                    const response = await window.EmailApi.retry(id);
                    response.success ? store.flashSuccess(response.message) : store.flashWarning(response.message);
                } catch (e) {
                    store.flashError(e.message);
                }
                await load();
            }

            async function remove(id) {
                if (!window.confirm('确认软删除这封邮件？数据会物理保留，仅查询不再返回。')) {
                    return;
                }
                try {
                    const response = await window.EmailApi.remove(id);
                    store.flashSuccess(response.message);
                } catch (e) {
                    store.flashError(e.message);
                }
                await load();
            }

            // 浏览器前进/后退时，URL 是唯一事实来源
            watch(function () { return route.query; }, function (query) {
                if (ignoreNextQueryChange) {
                    return;
                }
                status.value = query.status || '';
                keyword.value = query.keyword || '';
                page.value = Number(query.page) || 1;
                pageSize.value = Number(query.pageSize) || 20;
                load();
            });

            onMounted(load);
            return { status, keyword, page, pageSize, result, loading, error, statuses, search, reset, go, retry, remove, format };
        },
        template: `
<div>
  <div class="page-header">
    <h1>邮件</h1>
    <div class="actions">
      <router-link class="btn btn-primary" to="/compose">发送测试邮件</router-link>
    </div>
  </div>

  <div v-if="error" class="alert alert-error">{{ error }}</div>

  <form class="panel filter-form" @submit.prevent="search">
    <label>状态
      <select v-model="status">
        <option value="">全部</option>
        <option v-for="item in statuses" :key="item" :value="item">{{ item }}</option>
      </select>
    </label>
    <label>关键字
      <input type="text" v-model="keyword" placeholder="主题模糊匹配 / 收件人完整地址" />
    </label>
    <label>每页
      <select v-model.number="pageSize">
        <option :value="10">10</option>
        <option :value="20">20</option>
        <option :value="50">50</option>
      </select>
    </label>
    <button class="btn" type="submit">查询</button>
    <button class="btn btn-ghost" type="button" @click="reset">重置</button>
  </form>

  <div class="panel">
    <table class="table">
      <thead>
        <tr><th>主题</th><th>收件人</th><th>状态</th><th>重试</th><th>附件</th><th>创建时间</th><th></th></tr>
      </thead>
      <tbody>
        <tr v-if="loading"><td colspan="7" class="empty">加载中…</td></tr>
        <tr v-else-if="result.items.length === 0"><td colspan="7" class="empty">没有匹配的邮件</td></tr>
        <tr v-for="item in result.items" :key="item.id">
          <td>
            <router-link :to="'/emails/' + item.id">{{ item.subject }}</router-link>
            <span v-if="item.isHtml" class="tag">HTML</span>
          </td>
          <td class="muted">{{ item.recipients }}</td>
          <td><status-badge :status="item.status"></status-badge></td>
          <td>{{ item.retryCount }}</td>
          <td>{{ item.attachmentCount }}</td>
          <td class="muted">{{ format.date(item.createdAt) }}</td>
          <td class="row-actions">
            <router-link class="btn btn-sm" :to="'/emails/' + item.id">详情</router-link>
            <button class="btn btn-sm" @click="retry(item.id)">重试</button>
            <button class="btn btn-sm btn-danger" @click="remove(item.id)">删除</button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <pager :page="result" @go="go"></pager>
</div>`
    };

    /* ------------------------------------------------------------ 邮件详情 */

    const EmailDetailPage = {
        setup: function () {
            const store = window.EmailStore;
            const format = window.EmailFormat;
            const route = VueRouter.useRoute();
            const router = VueRouter.useRouter();

            const email = ref(null);
            const loading = ref(true);
            const error = ref('');
            const busy = ref(false);

            async function load() {
                loading.value = true;
                error.value = '';
                try {
                    email.value = await window.EmailApi.email(route.params.id);
                } catch (e) {
                    error.value = e.message;
                    email.value = null;
                } finally {
                    loading.value = false;
                }
            }

            async function retry() {
                busy.value = true;
                try {
                    const response = await window.EmailApi.retry(email.value.id);
                    response.success ? store.flashSuccess(response.message) : store.flashWarning(response.message);
                } catch (e) {
                    store.flashError(e.message);
                } finally {
                    busy.value = false;
                }
                await load();
            }

            async function remove() {
                if (!window.confirm('确认软删除这封邮件？数据会物理保留，仅查询不再返回。')) {
                    return;
                }
                busy.value = true;
                try {
                    await window.EmailApi.remove(email.value.id);
                    store.flashSuccess('邮件已软删除。');
                    router.push('/emails');
                } catch (e) {
                    store.flashError(e.message);
                    busy.value = false;
                }
            }

            const attachmentUrl = window.EmailApi.attachmentUrl;

            watch(function () { return route.params.id; }, load);
            onMounted(load);
            return { email, loading, error, busy, retry, remove, format, attachmentUrl };
        },
        template: `
<div>
  <div class="page-header">
    <h1>邮件详情</h1>
    <div class="actions">
      <router-link class="btn btn-ghost" to="/emails">返回列表</router-link>
      <button class="btn" :disabled="busy || !email" @click="retry">重试发送</button>
      <button class="btn btn-danger" :disabled="busy || !email" @click="remove">软删除</button>
    </div>
  </div>

  <div v-if="loading" class="panel muted">加载中…</div>
  <div v-else-if="error" class="alert alert-error">{{ error }}</div>

  <div v-else-if="email">
    <div class="grid-2">
      <section class="panel">
        <h2>基本信息</h2>
        <dl class="kv">
          <dt>主题</dt><dd>{{ email.subject }}</dd>
          <dt>状态</dt><dd><status-badge :status="email.record ? email.record.status : 'Init'"></status-badge></dd>
          <dt>邮件 Id</dt><dd class="mono">{{ email.id }}</dd>
          <dt>收件人</dt><dd>{{ format.recipients(email.to) }}</dd>
          <dt v-if="email.cc && email.cc.length">抄送</dt>
          <dd v-if="email.cc && email.cc.length">{{ format.recipients(email.cc) }}</dd>
          <dt v-if="email.bcc && email.bcc.length">密送</dt>
          <dd v-if="email.bcc && email.bcc.length">{{ format.recipients(email.bcc) }}</dd>
          <dt>发件人</dt><dd>{{ email.from || '（使用 SmtpSettings.SenderEmail）' }}</dd>
          <dt>正文类型</dt><dd>{{ email.isHtml ? 'HTML' : '纯文本' }}</dd>
          <dt>创建 / 更新</dt><dd class="muted">{{ format.dateTime(email.createdAt) }} / {{ format.dateTime(email.updatedAt) }}</dd>
        </dl>
      </section>

      <section class="panel">
        <h2>发送记录</h2>
        <dl v-if="email.record" class="kv">
          <dt>状态</dt><dd>{{ email.record.status }}</dd>
          <dt>重试次数</dt><dd>{{ email.record.retryCount }}</dd>
          <dt>发送时间</dt><dd class="muted">{{ format.dateTime(email.record.sentTime) }}</dd>
          <dt>失败时间</dt><dd class="muted">{{ format.dateTime(email.record.failedTime) }}</dd>
          <dt>上次重试</dt><dd class="muted">{{ format.dateTime(email.record.lastRetryTime) }}</dd>
          <dt v-if="email.record.failedAddresses && email.record.failedAddresses.length">失败地址</dt>
          <dd v-if="email.record.failedAddresses && email.record.failedAddresses.length">{{ format.recipients(email.record.failedAddresses) }}</dd>
        </dl>
        <p v-else class="muted">没有发送记录。</p>
        <div v-if="email.record && email.record.errorMessage" class="alert alert-error">
          <strong>{{ email.record.errorMessage }}</strong>
          <pre v-if="email.record.errorDetails" class="body-source small">{{ email.record.errorDetails }}</pre>
        </div>
      </section>
    </div>

    <section class="panel">
      <h2>正文</h2>
      <!-- sandbox 空值表示禁用脚本/表单等能力，避免渲染库里的 HTML 时执行脚本 -->
      <iframe v-if="email.isHtml" class="preview" sandbox :srcdoc="email.body" title="HTML 正文预览"></iframe>
      <pre class="body-source">{{ email.body }}</pre>
    </section>

    <section class="panel">
      <h2>附件（{{ email.attachments.length }}）</h2>
      <p v-if="email.attachments.length === 0" class="muted">该邮件没有附件。</p>
      <table v-else class="table">
        <thead>
          <tr><th>文件名</th><th>类型</th><th>大小</th><th>存储位置</th><th>FTP 路径</th><th></th></tr>
        </thead>
        <tbody>
          <tr v-for="attachment in email.attachments" :key="attachment.id">
            <td>{{ attachment.fileName }}</td>
            <td class="muted">{{ attachment.contentType }}</td>
            <td class="muted">{{ format.size(attachment.fileSize) }}</td>
            <td><span class="tag">{{ attachment.hasContent ? '数据库' : (attachment.isStoredInFtp ? 'FTP' : '无内容') }}</span></td>
            <td class="muted mono small">{{ attachment.filePath || '-' }}</td>
            <td class="row-actions"><a class="btn btn-sm" :href="attachmentUrl(attachment.id)">下载</a></td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</div>`
    };

    /* ------------------------------------------------------------ 发送测试邮件 */

    const ComposePage = {
        setup: function () {
            const store = window.EmailStore;
            const router = VueRouter.useRouter();

            const form = Vue.reactive({
                to: '',
                cc: '',
                bcc: '',
                from: '',
                subject: '来自 Email.WebApp 的测试邮件',
                body: '这是一封通过 WebApp 控制台发送的测试邮件。',
                isHtml: false
            });
            const files = ref([]);
            const busy = ref(false);
            const error = ref('');

            function onFiles(event) {
                files.value = Array.from(event.target.files || []);
            }

            async function submit() {
                error.value = '';

                if (!form.to.trim()) {
                    error.value = '请至少填写一个收件人。';
                    return;
                }
                if (!form.subject.trim()) {
                    error.value = '请填写主题。';
                    return;
                }
                if (!form.body.trim()) {
                    error.value = '请填写正文。';
                    return;
                }

                const data = new FormData();
                data.append('to', form.to);
                data.append('cc', form.cc || '');
                data.append('bcc', form.bcc || '');
                data.append('from', form.from || '');
                data.append('subject', form.subject);
                data.append('body', form.body);
                data.append('isHtml', form.isHtml ? 'true' : 'false');
                files.value.forEach(function (file) {
                    data.append('files', file, file.name);
                });

                busy.value = true;
                try {
                    const email = await window.EmailApi.send(data);
                    store.flashSuccess('邮件已提交（Id = ' + email.id + '）。');
                    router.push('/emails/' + email.id);
                } catch (e) {
                    error.value = e.message;
                } finally {
                    busy.value = false;
                }
            }

            return { form, files, busy, error, onFiles, submit };
        },
        template: `
<div>
  <div class="page-header">
    <h1>发送测试邮件</h1>
    <div class="actions"><router-link class="btn btn-ghost" to="/emails">邮件列表</router-link></div>
  </div>

  <div class="panel">
    <p class="muted">
      走完整链路：先入库（状态 Init）→ 调用 SMTP 发送 → 成功后把附件归档到 FTP。
      发送失败的邮件会以 Failed 状态留在库里，可手动重试，也会被后台重试调度接管。
    </p>

    <div v-if="error" class="alert alert-error">{{ error }}</div>

    <form @submit.prevent="submit">
      <div class="form-row">
        <label>收件人</label>
        <input type="text" v-model="form.to" placeholder="多个地址用逗号、分号或换行分隔" />
      </div>
      <div class="form-row">
        <label>抄送</label>
        <input type="text" v-model="form.cc" />
      </div>
      <div class="form-row">
        <label>密送</label>
        <input type="text" v-model="form.bcc" />
      </div>
      <div class="form-row">
        <label>发件人</label>
        <input type="text" v-model="form.from" placeholder="留空则使用 SmtpSettings.SenderEmail" />
      </div>
      <div class="form-row">
        <label>主题</label>
        <input type="text" v-model="form.subject" />
      </div>
      <div class="form-row">
        <label>正文</label>
        <textarea rows="10" v-model="form.body"></textarea>
      </div>
      <div class="form-row inline">
        <label class="checkbox"><input type="checkbox" v-model="form.isHtml" /> 正文是 HTML</label>
      </div>
      <div class="form-row">
        <label>附件</label>
        <input type="file" multiple @change="onFiles" />
        <span class="muted small">支持多选，单个请求最大 30 MB</span>
      </div>
      <div class="form-actions">
        <button class="btn btn-primary" type="submit" :disabled="busy">{{ busy ? '发送中…' : '发送' }}</button>
        <router-link class="btn btn-ghost" to="/emails">取消</router-link>
      </div>
    </form>
  </div>
</div>`
    };

    /* ------------------------------------------------------------ 依赖诊断 */

    const DiagnosticsPage = {
        setup: function () {
            const data = ref(null);
            const loading = ref(true);
            const error = ref('');

            async function load() {
                loading.value = true;
                error.value = '';
                try {
                    data.value = await window.EmailApi.diagnostics();
                } catch (e) {
                    error.value = e.message;
                } finally {
                    loading.value = false;
                }
            }

            onMounted(load);
            return { data, loading, error, load, format: window.EmailFormat };
        },
        template: `
<div>
  <div class="page-header">
    <h1>依赖诊断</h1>
    <div class="actions"><button class="btn" :disabled="loading" @click="load">重新检测</button></div>
  </div>

  <div v-if="error" class="alert alert-error">{{ error }}</div>

  <p v-if="data" class="muted">
    环境：{{ data.environment }} · 检测时间：{{ format.dateTime(data.timestamp) }}
    <span :class="data.allHealthy ? 'badge badge-sent' : 'badge badge-failed'">
      {{ data.allHealthy ? '全部正常' : '存在异常依赖' }}
    </span>
  </p>

  <section class="cards">
    <div v-for="item in (data ? data.items : [])" :key="item.target"
         class="card diag" :class="item.success ? 'diag-ok' : 'diag-bad'">
      <div class="diag-head"><span class="dot"></span><span class="diag-title">{{ item.target }}</span></div>
      <div class="diag-msg">{{ item.message }}</div>
      <div class="muted small">耗时 {{ item.elapsedMilliseconds }} ms</div>
    </div>
  </section>

  <section class="panel">
    <h2>说明</h2>
    <ul class="muted">
      <li><strong>SMTP</strong>：按 Redis 中 SmtpSettings 建立连接并认证，不发送邮件。</li>
      <li><strong>FTP</strong>：按 Redis 中 FtpSettings 建立连接，验证附件归档通道。</li>
      <li><strong>Database</strong>：检查 SQL Server 连接是否可用。</li>
      <li><strong>Redis</strong>：PING 一次，验证动态配置来源是否可达。</li>
    </ul>
  </section>
</div>`
    };

    window.EmailPages = {
        DashboardPage: DashboardPage,
        EmailListPage: EmailListPage,
        EmailDetailPage: EmailDetailPage,
        ComposePage: ComposePage,
        DiagnosticsPage: DiagnosticsPage
    };
})();
