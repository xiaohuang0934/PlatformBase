# PlatformBase 文档导航

## 后端文档 `docs/`

### 入门（推荐阅读顺序）

| # | 文档 | 说明 |
|---|------|------|
| 1 | [architecture.md](architecture.md) | 分层架构、中间件管道、实体继承、签名密钥、响应模型 |
| 2 | [configuration.md](configuration.md) | appsettings.json 配置项说明、环境变量覆盖 |
| 3 | [dev-guide.md](dev-guide.md) | 新增业务模块完整教程（实体→DTO→Service→Controller） |

### 核心业务

| 文档 | 说明 |
|------|------|
| [auth.md](auth.md) | 认证授权：登录流程、JWT 签发/验证、会话上下文、锁定机制 |
| [permissions.md](permissions.md) | RBAC 三层权限模型、判定链路、Redis 缓存、种子数据 |
| [modules.md](modules.md) | 14 个业务模块的端点、DTO 字段、业务逻辑 |
| [database.md](database.md) | 21 张表的 ER 说明、索引策略、复合主键设计 |

### 参考

| 文档 | 说明 |
|------|------|
| [api-reference.md](api-reference.md) | 全量 API 端点表（方法/路径/权限） |
| [error-codes.md](error-codes.md) | 错误码分段、所有错误码枚举 |
| [patterns.md](patterns.md) | 代码模式：Expression AppendIf、软删除、BCrypt、Stamp 验证 |

### 运维 & 设计

| 文档 | 说明 |
|------|------|
| [deployment.md](deployment.md) | 生产部署：Migration 切换、Docker Compose、HTTPS |
| [design-decisions.md](design-decisions.md) | 关键设计决策：仓储模式、多租户、软删除、DTO 映射 |
| [changelog.md](changelog.md) | 逐版变更日志（v0.1 → v1.7） |

---

## 前端文档 `frontend/docs/`

| 文档 | 说明 |
|------|------|
| [architecture.md](../frontend/docs/architecture.md) | 技术选型（Vue3+Vite+ElementPlus+Vant）、分层架构、目录结构 |
| [dev-guide.md](../frontend/docs/dev-guide.md) | 新增模块流程（API→Store→PC 页面→路由→移动端页面） |
| [api-integration.md](../frontend/docs/api-integration.md) | axios 封装、拦截器、自动刷新 Token、文件下载 |
| [mobile-guide.md](../frontend/docs/mobile-guide.md) | 断点、布局切换、van-list 加载、抽屉导航 |
| [permissions.md](../frontend/docs/permissions.md) | 权限码矩阵、v-permission 指令、路由守卫 |

---

## 按角色推荐阅读

| 角色 | 必读 | 参考 |
|------|------|------|
| 新加入后端开发 | architecture → configuration → dev-guide | patterns |
| 后端功能开发 | modules（对应模块） → api-reference | database |
| 前端开发 | 前端 architecture → dev-guide → api-integration | 前端 permissions |
| 运维/部署 | deployment → configuration | 后端 architecture（中间件管道） |
| 架构评审 | design-decisions → architecture → database | auth → permissions |
