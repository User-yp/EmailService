/*
 * 离线静态校验：用 Node 的 vm 沙箱加载 wwwroot 下的前端脚本，
 * 让 Vue 的运行时编译器编译每个组件的 template，提前发现模板语法错误。
 * 用法：node tools/check-vue-templates.js
 */
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const root = path.join(__dirname, '..', 'Email.WebApp', 'wwwroot');
const sandbox = { console: console, setTimeout: setTimeout, clearTimeout: clearTimeout };
sandbox.window = sandbox;
sandbox.self = sandbox;

// Vue 浏览器版编译器在遇到 "&"（例如模板里的 &&）时会调用 decodeHtmlBrowser 解码实体：
//   asAttr: div.innerHTML = `<div foo="${raw}">`; div.children[0].getAttribute('foo')
//   文本:   div.innerHTML = raw;               div.textContent
// 这里按同样方式实现一个最小桩。浏览器把 "&&" 视作 ambiguous ampersand 并按字面保留，
// 因此桩也只反转义 &quot; 之后原样返回，行为与浏览器一致。
function createFakeElement() {
    const element = {
        textContent: '',
        attribute: '',
        children: [],
        getAttribute: function () { return element.attribute; }
    };
    Object.defineProperty(element, 'innerHTML', {
        get: function () { return element.textContent; },
        set: function (value) {
            const match = /^<div foo="([\s\S]*)">$/.exec(value);
            if (match) {
                element.attribute = match[1].replace(/&quot;/g, '"');
            } else {
                element.textContent = value;
            }
            element.children = [element];
        }
    });
    return element;
}

sandbox.document = { createElement: createFakeElement };
vm.createContext(sandbox);

function load(relativePath) {
    const code = fs.readFileSync(path.join(root, relativePath), 'utf8');
    vm.runInContext(code, sandbox, { filename: relativePath });
}

// 用开发版编译器：prod 构建会把编译错误的具体信息压缩成错误码链接，不利于排查
const vueFile = fs.existsSync(path.join(root, 'vendor', 'vue.global.js')) ? 'vendor/vue.global.js' : 'vendor/vue.global.prod.js';
load(vueFile);
load('vendor/vue-router.global.prod.js');
load('js/api.js');
load('js/ui.js');
load('js/pages.js');

// app.js 会真正挂载应用，这里把 router / createApp 换成桩，只取根组件做模板校验
let rootComponent = null;
sandbox.VueRouter.createWebHistory = function () { return {}; };
sandbox.VueRouter.createRouter = function (options) { return { options: options, install: function () { } }; };
sandbox.Vue.createApp = function (component) {
    rootComponent = component;
    return { component: function () { }, use: function () { }, mount: function () { } };
};
load('js/app.js');

const components = [];
Object.keys(sandbox.window.EmailComponents).forEach(function (name) {
    components.push({ name: name, template: sandbox.window.EmailComponents[name].template });
});
Object.keys(sandbox.window.EmailPages).forEach(function (name) {
    components.push({ name: name, template: sandbox.window.EmailPages[name].template });
});
if (rootComponent && rootComponent.template) {
    components.push({ name: 'RootComponent', template: rootComponent.template });
}

let failed = 0;
components.forEach(function (component) {
    if (!component.template) {
        console.log('SKIP ' + component.name + '（无 template）');
        return;
    }
    try {
        sandbox.Vue.compile(component.template);
        console.log('OK   ' + component.name);
    } catch (error) {
        failed++;
        console.log('FAIL ' + component.name + ' -> ' + error.message);
    }
});

console.log('---');
console.log(components.length + ' 个组件，失败 ' + failed + ' 个');
process.exit(failed === 0 ? 0 : 1);
