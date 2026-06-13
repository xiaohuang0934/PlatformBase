# 开发指南 / Developer Guide

## 环境搭建

```bash
# 安装依赖
cd frontend
npm install

# 启动开发服务器
npm run dev        # http://localhost:5173
```

后端 API 代理已配置在 `vite.config.ts` 中：
```ts
server: {
  proxy: { '/api': { target: 'http://localhost:5269', changeOrigin: true } }
}
```

## 如何新增业务模块

### Step 1 — API 层 (`src/api/{module}.ts`)

```ts
import type { ApiResult, PagedResult } from '@/types/api-result'
import http from './index'

const BASE = '/your-module'

export function getList(params?: Record<string, unknown>): Promise<ApiResult<PagedResult<any>>> {
  return http.get(BASE, { params }).then(res => res.data)
}
```

### Step 2 — 类型定义 (`src/types/{module}.ts`)

```ts
export interface YourDto {
  id: string
  name: string
  // ...
}
```

### Step 3 — PC 端列表页 (`views/{module}/Desktop{Module}List.vue`)

参考模板模式：
- `<page-header>` + `<page-header__title>`
- `<TableToolbar>` + `<div class="search-bar">`
- `<el-table>` + `<el-pagination>`
- `<el-dialog>` CRUD 弹窗

### Step 4 — 移动端列表页 (`views/{module}/Mobile{Module}List.vue`)

参考模板模式：
- `<van-sticky>` + `<van-search>`
- `<van-button>` 添加按钮
- `<van-list>` + 卡片列表
- `<van-action-sheet>` CRUD 表单

### Step 5 — 路由注册

在 `src/router/modules/system.ts` 的 `componentMap` 中添加：

```ts
const componentMap: Record<string, () => Promise<unknown>> = {
  'your-module': () => import('@/views/your-module/DesktopYourModuleList.vue'),
  // ...
}
```

### Step 6 — 后端种子数据

在 `DataSeeder.cs` 的 `SeedMenusAsync` 中添加菜单条目。

## 代码规范

### 文件命名

| 类别 | 规范 | 示例 |
|------|------|------|
| 组件文件 | PascalCase | `UserList.vue` |
| 工具/API/Store | kebab-case | `user-api.ts`, `use-device.ts` |
| 目录 | kebab-case | `system-params/` |
| CSS 类 | BEM / kebab-case | `.card-header__title` |

### 组件编写

```vue
<script setup lang="ts">
import type { Ref } from 'vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const loading = ref(false)

async function fetchData() {
  loading.value = true
  try {
    // ...
  } catch {
    ElMessage.error('加载失败，请重试')
  } finally { loading.value = false }
}
</script>
```

### API 错误处理

**所有 fetchList 必须有 catch 块**，模板模式：

```ts
try {
  const res = await api.getList(...)
  list.value = res.data.items
  total.value = res.data.totalCount
} catch {
  ElMessage.error('加载失败，请重试')
} finally { loading.value = false }
```

### Dialog 提交错误处理

**所有 handleSubmit 必须有 catch 块**：

```ts
async function handleSubmit() {
  submitting.value = true
  try {
    await api.create(data)
    ElMessage.success('创建成功')
    dialogVisible.value = false; fetchList()
  } catch {
    ElMessage.error('操作失败，请重试')
  } finally { submitting.value = false }
}
```

### 删除确认

**所有删除操作必须有 ElMessageBox.confirm**：

```ts
async function handleDelete(row) {
  try {
    await ElMessageBox.confirm(`确定删除 "${row.name}" 吗？`, '确认删除', {
      confirmButtonText: '删除',
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch { return }
  await api.delete(row.id)
  ElMessage.success('已删除')
  fetchList()
}
```
