/*
 * 应用入口：注册共享组件、配置 vue-router、挂载根组件。
 * 新增页面只需三处改动：pages.js 加组件 → 这里加路由 → 导航里加链接。
 */
(function () {
    'use strict';

    const Root = {
        template: `
<div class="layout">
  <header class="topbar">
    <div class="brand">EmailService 控制台</div>
    <nav class="nav">
      <router-link class="nav-link" to="/">概览</router-link>
      <router-link class="nav-link" to="/emails">邮件</router-link>
      <router-link class="nav-link" to="/compose">发送测试邮件</router-link>
      <router-link class="nav-link" to="/diagnostics">依赖诊断</router-link>
    </nav>
  </header>

  <main class="container">
    <flash-banner></flash-banner>
    <router-view></router-view>
  </main>

  <footer class="footer">EmailService · Vue 3 前端控制台（Email.WebApp）· 仅用于开发联调</footer>
</div>`
    };

    const routes = [
        { path: '/', name: 'dashboard', component: window.EmailPages.DashboardPage },
        { path: '/emails', name: 'emails', component: window.EmailPages.EmailListPage },
        { path: '/emails/:id', name: 'email-detail', component: window.EmailPages.EmailDetailPage },
        { path: '/compose', name: 'compose', component: window.EmailPages.ComposePage },
        { path: '/diagnostics', name: 'diagnostics', component: window.EmailPages.DiagnosticsPage },
        { path: '/:pathMatch(.*)*', redirect: '/' }
    ];

    const router = VueRouter.createRouter({
        // history 模式：后端已配置 MapFallbackToFile，刷新子路由不会 404
        history: VueRouter.createWebHistory(),
        routes: routes
    });

    const app = Vue.createApp(Root);

    app.component('status-badge', window.EmailComponents.StatusBadge);
    app.component('pager', window.EmailComponents.Pager);
    app.component('flash-banner', window.EmailComponents.FlashBanner);

    app.use(router);
    app.mount('#app');
})();
