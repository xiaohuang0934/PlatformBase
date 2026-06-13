# 移动端适配指南 / Mobile Guide

## 断点

| 设备 | 宽度 | 布局 |
|------|------|------|
| Mobile | < 768px | MobileLayout（底部 TabBar） |
| Desktop | ≥ 768px | DesktopLayout（侧边栏 + 顶栏） |

## 设备检测

```ts
// composables/useDevice.ts
const { isMobile, width } = useDevice()
```

## 布局切换

`App.vue` 根据 `isMobile` 决定渲染 DesktopLayout 还是 MobileLayout。

路由组件按 `window.innerWidth` 在 Desktop/Mobile 版本间动态切换。

## 移动端布局

```
┌──────────────────────────┐
│ 🔍 搜索...               │  ← van-search（sticky）
│ [筛选项]                  │  ← van-tabs（可选）
├──────────────────────────┤
│ [  + 添 加  ]            │  ← 全宽 van-button
├──────────────────────────┤
│ ┌──────────────────────┐ │
│ │ 卡片标题        ＞   │ │  ← 可点击进入详情
│ │ 字段 · 字段          │ │
│ │   查看详情  ›        │ │
│ └──────────────────────┘ │
│           加载更多...     │  ← van-list
├──────────────────────────┤
│ 工作台 │ 应用 │ 消息 │ 我│  ← 底部 TabBar
└──────────────────────────┘
```

## 移动端 CRUD 模式

| 操作 | 交互 | 组件 |
|------|------|------|
| 列表 | 卡片 + 滚动加载 | `van-list` |
| 新增 | action-sheet 底部表单 | `van-action-sheet` |
| 编辑 | 点击卡片 → 进入编辑页 | 全屏编辑页 |
| 删除 | action-sheet 底部按钮 → 确认弹窗 | `van-dialog` |
| 返回 | 全局 `van-nav-bar` left-arrow | `src/layouts/MobileLayout/` |

## 全局导航

- **TabBar**: 工作台 / 应用 / 消息 / 我的
- **抽屉菜单**: 左侧弹出完整菜单树（所有模块的入口）
- **返回按钮**: 非 Tab 页面自动显示顶部返回按钮
