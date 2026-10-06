# 内置前端运行时

为了让控制台在**离线环境**也能直接运行，Vue 运行时随项目一起提供，浏览器直接从 `wwwroot/vendor` 加载，
不使用 CDN，也不需要 `npm install` / 打包步骤。

| 文件 | 版本 | 来源 | 许可 |
|---|---|---|---|
| `vue.global.prod.js` | 3.4.38 | `node_modules/vue/dist/` | MIT |
| `vue-router.global.prod.js` | 4.4.3 | `node_modules/vue-router/dist/` | MIT |

两个文件均使用官方 production 构建（`vue.global.prod.js` 含运行时模板编译器，因此组件可以用 template 字符串书写）。

## 升级方式

```bash
npm install vue@latest vue-router@latest
copy node_modules\vue\dist\vue.global.prod.js                 Email.WebApp\wwwroot\vendor\
copy node_modules\vue-router\dist\vue-router.global.prod.js   Email.WebApp\wwwroot\vendor\
```

> 如果后续想改成 Vite + 单文件组件（`.vue`）工程，把 `ClientApp` 的构建产物输出到 `wwwroot` 即可，
> 后端的 JSON 接口与 SPA 回落配置都不用改。
