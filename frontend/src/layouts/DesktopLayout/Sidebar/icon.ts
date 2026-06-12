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
  Share,
  Tickets,
  Timer,
  UserFilled,
} from '@element-plus/icons-vue'

const iconLookup: Record<string, Component> = {
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
  Share,
  Tickets,
  Timer,
  UserFilled,
}

export function resolveIcon(name: string | null): Component | null {
  if (!name)
    return null
  return iconLookup[name] ?? null
}
