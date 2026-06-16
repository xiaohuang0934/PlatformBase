# 前端架构设计 / Frontend Architecture

## 技术选型

| 层级 | 技术 | 选型理由 |
|------|------|---------|
| 框架 | Vue 3 + Composition API | 团队熟悉，组合式 API 天然支持逻辑复用 |
| 语言 | TypeScript strict | 强类型约束，减少运行时错误 |
| 构建 | Vite | 秒级冷启动，HMR 极快 |
| PC UI | Element Plus | 企业后台最成熟，表格/表单/菜单树组件丰富 |
| 移动 UI | Vant | 移动端组件最全（TabBar/SwipeCell/ActionSheet） |
| 状态管理 | Pinia | Vue 3 官方推荐，组合式 API 风格 |
| 图标 | @tabler/icons-vue | 统一 PC/移动双端图标，风格简约 |

## 分层架构

```
┌──────────────────────────────────────┐
│  Views (页面)                        │
│  DesktopXxxList.vue / MobileXxxList  │
├──────────────────────────────────────┤
│  Components (通用组件)               │
│  Breadcrumb / TagsView / BackToTop   │
│  TenantSelector (v1.8新增)           │
│  OrgSelector / OrgTree (v1.8新增)    │
├──────────────────────────────────────┤
│  Composables (业务逻辑)              │
│  useDevice / useTableSelection       │
│  useTenantSwitch (v1.8新增)          │
├──────────────────────────────────────┤
│  Stores (Pinia 状态)                 │
│  auth / permission / theme / tags    │
│  org (v1.8新增: 部门树+用户查询)     │
├──────────────────────────────────────┤
│  API (HTTP 请求)                     │
│  axios + 拦截器 + 自动 Token 刷新    │
├──────────────────────────────────────┤
│  Utils (工具)                        │
│  token / refresh / download / validate│
├──────────────────────────────────────┤
│  Types (TypeScript 类型)             │
│  ApiResult<T> / DTOs                 │
└──────────────────────────────────────┘
```

依赖方向：View → Component/Composable → Store → API → Utils/Types（单向）

## 路由架构

### 静态路由（无权限）

```
/login          → 登录页
/dashboard      → 工作台
/403            → 无权限
/404            → 页面不存在
/m/apps         → 应用中心（移动端）
/m/notifications → 消息通知（移动端）
/m/profile      → 个人中心（移动端）
```

### 动态路由（后端菜单树驱动）

登录后调用 `GET /menus/tree`，将菜单树转换为 Vue Router 路由表，通过 `router.addRoute()` 动态注册。

路由组件按 `window.innerWidth` 自动加载 Desktop/Mobile 版本。

## 状态管理 (v1.8)

| Store | 职责 | 持久化 | v1.8 变更 |
|------|------|:--:|---------|
| `auth` | token + userId/username/userType/isSuperAdmin + permissions | localStorage (token) | 新增 userType、isSuperAdmin；roles 字段移除 |
| `permission` | 菜单树 + 路由注册状态 | — | 支持按租户视角刷新菜单 |
| `app` | 侧边栏折叠 + 设备类型 + 当前租户选择 | — | 新增 currentTenantId |
| `theme` | 暗/亮模式 | localStorage | |
| `tagsView` | 多页签列表 + 缓存 | — | |
| `settings` | 布局设置（固定顶栏等） | — | |
| `errorLog` | 前端错误日志 | — | |

### auth Store 类型定义

```ts
interface AuthState {
  token: string
  userId: string | null
  username: string | null
  userType: number | null // 1=PlatformAdmin, 2=TenantAdmin, 3=TenantUser
  isSuperAdmin: boolean
  permissions: string[] // GET /auth/permissions 返回的权限编码列表
  tenantIds: string[] // GET /tenants/accessible 返回的租户列表（平台用户）
}
```

## 主题系统

CSS 自定义属性双模驱动：

```
:root / [data-theme="dark"]  → 墨石暗色 (#0d0d11 / #4f8cf7)
[data-theme="light"]         → 中性亮色 (#f6f6f8 / #3b78e7)
```

`index.html` 内联脚本在页面渲染前读取 localStorage 设置 `data-theme`，防止闪烁。

## 权限控制 (三道防线)

| 防线 | 位置 | 机制 | v1.8 变更 |
|------|------|------|---------|
| 第 1 道 | `router/permission.ts` | `beforeEach` 守卫：未登录跳转 → 动态路由注册 | 超级管理员路由不裁剪 |
| 第 2 道 | `directives/permission.ts` | `v-permission` 指令：无权限的 DOM 元素直接移除 | 支持 `v-user-type` 指令 |
| 第 3 道 | 后端 `[Permission]` | API 层终极校验 | 后端自动放行 SuperAdmin |

## Token 刷新机制

```
请求拦截器
  ├─ 无 token → 放行（匿名请求）
  ├─ token 有效 → 直接附加 Bearer
  └─ token < 60s 过期 → 调 refreshAccessToken()
                           ├─ 成功 → 换新 token
                           └─ 失败 → 清除 token → 跳转登录

响应拦截器
  ├─ 401 + 未重试 → refreshAccessToken() → 重试原请求
  └─ 401 + 已重试 → 清除 token → 跳转登录
```

并发请求保护：同时多个请求发现 token 过期时，仅第一个发刷新请求，其余排队等待新 token。

## v1.8 新增组件规格

### TenantSelector（租户选择器）

- **权限限制**：仅 `userType === 1`（PlatformAdmin）可见
- **数据源**：`GET /tenants/accessible`
- **行为**：切换时重置当前页状态 + 重新加载菜单树 + 重新加载列表数据

### OrgSelector / OrgTree（部门选择器）

- **使用场景**：用户创建/编辑表单中的部门多选
- **数据源**：`GET /organization-units` 树形数据
- **交互**：支持勾选多个部门节点（平铺式或树形）

---

> **完整文档导航**：[docs/README.md](../../docs/README.md) — 后端 + 前端全部文档索引，含按角色推荐阅读路径
