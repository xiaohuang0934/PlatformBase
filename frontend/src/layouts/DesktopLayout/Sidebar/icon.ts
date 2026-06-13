import type { Component } from 'vue'
import {
  Avatar,
  Collection,
  DataAnalysis,
  Document,
  Folder,
  Lock,
  Menu,
  Monitor,
  OfficeBuilding,
  Operation,
  Setting,
  Tickets,
  Timer,
  UserFilled,
} from '@element-plus/icons-vue'
import {
  IconActivity,
  IconBell,
  IconBooks,
  IconBuilding,
  IconChartArrows,
  IconClock,
  IconDownload,
  IconFileText,
  IconFolders,
  IconLock,
  IconMenu2,
  IconSettings,
  IconSettingsCog,
  IconShare,
  IconTicket,
  IconTool,
  IconUpload,
  IconUsers,
  IconUserShield,
} from '@tabler/icons-vue'

const iconLookup: Record<string, Component> = {
  // Tabler Icons (主图标集)
  IconActivity,
  IconBell,
  IconBooks,
  IconBuilding,
  IconChartArrows,
  IconClock,
  IconDownload,
  IconFileText,
  IconFolders,
  IconLock,
  IconMenu2,
  IconSettings,
  IconSettingsCog,
  IconShare,
  IconTicket,
  IconTool,
  IconUpload,
  IconUserShield,
  IconUsers,
  // Element Plus 图标（回退兼容）
  Avatar,
  Collection,
  DataAnalysis,
  Document,
  Folder,
  Monitor,
  OfficeBuilding,
  Operation,
  Setting,
  Tickets,
  Timer,
  UserFilled,
  Lock,
  Menu,
}

export function resolveIcon(name: string | null): Component | null {
  if (!name)
    return null
  return iconLookup[name] ?? null
}
