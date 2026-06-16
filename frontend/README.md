# PlatformBase 前端项目

基于 Vue 3 + TypeScript + Vite 的企业级管理后台前端。

## 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 框架 | Vue 3 + Composition API + `<script setup>` | 3.5+ |
| 语言 | TypeScript (strict) | 5.5+ |
| 构建 | Vite | 6+ |
| 状态管理 | Pinia | 2+ |
| 路由 | Vue Router 4 | 4+ |
| HTTP 客户端 | Axios | 1.7+ |
| PC UI | Element Plus | 2.9+ |
| 移动 UI | Vant | 4.9+ |
| 图标 | @tabler/icons-vue | — |
| CSS | SCSS + CSS 自定义属性 | — |
| 代码规范 | ESLint + Prettier | — |

## 快速开始

```bash
cd frontend
npm install
npm run dev        # 启动开发服务器 → http://localhost:5173
```

API 代理已配置，开发时自动转发 `/api` 请求到 `http://localhost:5269`。

## 可执行脚本

| 命令 | 说明 |
|------|------|
| `npm run dev` | 启动开发服务器 |
| `npm run build` | 生产构建（type-check + vite build） |
| `npm run preview` | 预览生产构建 |
| `npm run lint` | ESLint 检查 |
| `npm run format` | Prettier 格式化 |

## 项目结构

```
frontend/
├── index.html                   # 入口 HTML
├── vite.config.ts               # Vite 配置（别名/代理/自动导入/SCSS）
├── tsconfig.json                # TypeScript 配置
├── eslint.config.js             # ESLint 规则
├── .prettierrc                  # 代码格式化
├── commitlint.config.cjs        # 提交信息规范
│
├── docs/                        # 前端文档
│   ├── architecture.md          # 架构设计
│   ├── dev-guide.md             # 开发指南
│   ├── api-integration.md       # API 对接规范
│   ├── permissions.md           # 权限映射
│   └── mobile-guide.md          # 移动端适配指南
│
├── src/
│   ├── main.ts                  # 应用入口
│   ├── App.vue                  # 根组件（设备感知布局切换）
│   ├── settings.ts              # 全局配置
│   │
│   ├── api/                     # API 请求层
│   │   ├── index.ts             # axios 实例 + 拦截器（JWT 自动刷新）
│   │   ├── auth.ts              # 认证 API
│   │   ├── users.ts             # 用户管理
│   │   ├── roles.ts             # 角色管理
│   │   ├── permissions.ts       # 权限管理
│   │   ├── menus.ts             # 菜单管理
│   │   ├── tenants.ts           # 租户管理
│   │   ├── data-dict.ts         # 数据字典
│   │   ├── system-params.ts     # 系统参数
│   │   ├── operation-logs.ts    # 操作日志
│   │   ├── notifications.ts     # 消息通知
│   │   ├── files.ts             # 文件管理
│   │   ├── jobs.ts              # 定时任务
│   │   ├── organization.ts      # 组织架构
│   │   └── import-export.ts     # 导入导出
│   │
│   ├── types/                   # TypeScript 类型定义
│   │   ├── api-result.ts        # ApiResult<T>、PagedResult<T>、PagedRequest
│   │   ├── auth.ts              # 用户/角色/权限/菜单 DTO
│   │   └── user.ts              # 用户 DTO
│   │
│   ├── stores/                  # Pinia 状态管理
│   │   ├── auth.ts              # 认证（token + 用户 + 权限码）
│   │   ├── app.ts               # 应用状态（侧边栏/设备类型）
│   │   ├── permission.ts        # 权限（菜单树 + 动态路由）
│   │   ├── theme.ts             # 主题切换（暗/亮双模）
│   │   ├── tagsView.ts          # 多页签管理
│   │   ├── settings.ts          # 布局设置
│   │   └── errorLog.ts          # 前端错误日志
│   │
│   ├── router/                  # 路由
│   │   ├── index.ts             # 路由实例
│   │   ├── permission.ts        # 路由守卫（认证 + 动态路由注册）
│   │   └── modules/             # 路由模块
│   │       ├── auth.ts          # 静态路由（login/dashboard/403/404 + 移动端路由）
│   │       └── system.ts        # 动态路由生成（菜单树 → 路由表）
│   │
│   ├── layouts/                 # 布局
│   │   ├── DesktopLayout/       # PC 端布局
│   │   │   ├── index.vue         # 布局入口
│   │   │   ├── Navbar.vue        # 顶栏（面包屑 + 主题切换 + 用户菜单）
│   │   │   ├── AppMain.vue       # 主内容区（keep-alive + 路由视图）
│   │   │   └── Sidebar/          # 侧边栏
│   │   │       ├── index.vue      # 侧边栏容器
│   │   │       ├── Logo.vue       # Logo
│   │   │       ├── SidebarItem.vue # 菜单项递归渲染
│   │   │       └── icon.ts        # 图标解析
│   │   └── MobileLayout/        # 移动端布局
│   │       └── index.vue         # TabBar + 抽屉菜单 + 全局导航条
│   │
│   ├── components/              # 通用组件
│   │   ├── Breadcrumb.vue       # 面包屑导航
│   │   ├── Hamburger.vue        # 侧边栏折叠按钮
│   │   ├── TagsView.vue         # 多页签（右键菜单 + 关闭管理）
│   │   ├── BackToTop.vue        # 返回顶部
│   │   ├── TableToolbar.vue     # 表格工具栏
│   │   └── ChangePasswordDialog.vue # 修改密码弹窗
│   │
│   ├── composables/             # 组合式函数
│   │   ├── useDevice.ts         # 设备检测（isMobile）
│   │   └── useTableSelection.ts # 表格多选逻辑
│   │
│   ├── directives/              # 自定义指令
│   │   └── permission.ts        # v-permission 按钮级权限控制
│   │
│   ├── utils/                   # 工具函数
│   │   ├── token.ts             # Token 存取（accessToken/refreshToken/过期时间）
│   │   ├── refresh.ts           # 静默刷新（请求队列防并发）
│   │   ├── download.ts          # 文件下载（axios blob）
│   │   ├── validate.ts          # 表单校验
│   │   ├── scroll-to.ts         # 平滑滚动
│   │   ├── permission.ts        # 权限工具
│   │   ├── get-page-title.ts    # 页面标题
│   │   └── index.ts             # 通用工具
│   │
│   ├── styles/                  # 样式
│   │   ├── tokens.scss          # 设计令牌（暗/亮双模 120+ CSS 变量）
│   │   ├── variables.scss       # SCSS 变量
│   │   ├── reset.scss           # 样式重置 + 焦点指示器
│   │   ├── glass.scss           # 玻璃拟态 + 网格背景
│   │   ├── desktop.scss         # PC 端全局样式
│   │   └── mobile.scss          # 移动端全局样式
│   │
│   └── views/                   # 页面视图（按模块组织）
│       ├── login/               # 登录页
│       ├── dashboard/           # 工作台（Desktop + Mobile）
│       ├── users/               # 用户管理
│       ├── roles/               # 角色管理
│       ├── permissions/         # 权限管理
│       ├── menus/               # 菜单管理
│       ├── tenants/             # 租户管理
│       ├── data-dict/           # 数据字典
│       ├── system-params/       # 系统参数
│       ├── operation-logs/      # 操作日志
│       ├── notifications/       # 消息通知
│       ├── files/               # 文件管理
│       ├── jobs/                # 定时任务
│       ├── organization-units/  # 组织架构
│       ├── import-export/       # 导入导出
│       ├── apps/                # 应用中心（移动端）
│       └── profile/             # 个人中心（移动端）
```

## 模块命名约定

每个业务模块包含以下文件（以 `users` 为例）：

```
views/users/
├── DesktopUsersList.vue    # PC 端列表页
├── MobileUsersList.vue     # 移动端列表页
├── MobileUserDetail.vue    # 移动端详情页
└── MobileUserEdit.vue      # 移动端编辑页（创建+编辑共用）
```

## 架构设计

### 数据流

```
View (Vue) → Composable (业务逻辑) → Store (状态) → API (HTTP) → 后端
```

- **View 层**仅负责 UI 渲染和事件绑定
- **Composable 层**封装可复用的业务逻辑（设备检测、表格选择等）
- **Store 层**管理全局共享状态（认证、权限、主题等）
- **API 层**统一 HTTP 请求（axios 拦截器自动处理 token 刷新）

### 权限控制

三层防线：

| 层级 | 机制 | 说明 |
|------|------|------|
| 路由守卫 | `router.beforeEach` | 未登录 → 重定向登录；动态路由按菜单树注册 |
| 按钮级 | `v-permission` 指令 | 无权限的按钮直接从 DOM 移除 |
| API 层 | 后端 `[Permission]` | 最终防线，拦截无权限的 API 请求 |

### 主题系统

通过 `data-theme` 属性切换暗/亮模式，所有颜色通过 CSS 自定义属性驱动：

```html
<html data-theme="dark">
  ← 暗色模式（默认）
  <html data-theme="light">
    ← 亮色模式
  </html>
</html>
```

120+ 个设计令牌覆盖背景、文字、主色、边框、侧边栏、Element Plus 变量等。

### 移动端适配

- `composables/useDevice.ts` — 监听窗口尺寸，768px 为断点
- `App.vue` — 按设备渲染 `DesktopLayout` 或 `MobileLayout`
- 路由组件按 `window.innerWidth` 动态加载 Desktop/Mobile 版本
- 移动端布局：顶部搜索 + 卡片列表 + 底部 TabBar + 抽屉菜单

## 新增模块指南

1. **API 层**：在 `src/api/` 下新建模块文件，导出 API 函数
2. **类型**：在 `src/types/` 下添加 DTO 接口
3. **PC 端页面**：在 `src/views/{module}/` 下创建 `Desktop{Module}List.vue`
4. **移动端页面**：同目录创建 `Mobile{Module}List.vue`
5. **路由**：在 `src/router/modules/system.ts` 的 `componentMap` 或 `resolveComponent` 中注册
6. **菜单**：后端 DataSeeder 添加菜单条目

## 依赖

| 包 | 用途 |
|------|------|
| `vue` | 核心框架 |
| `vue-router` | 路由 |
| `pinia` | 状态管理 |
| `axios` | HTTP 请求 |
| `element-plus` | PC UI 组件库 |
| `vant` | 移动 UI 组件库 |
| `@element-plus/icons-vue` | Element Plus 图标 |
| `@tabler/icons-vue` | Tabler 图标（主图标集） |
| `sass` | SCSS 编译 |
| `unplugin-auto-import` | Vue API 自动导入 |
| `unplugin-vue-components` | 组件自动导入 |
