<script setup lang="ts">
import { IconCalendar, IconFilter, IconX } from '@tabler/icons-vue'
import { computed, reactive, ref, watch } from 'vue'

/** 筛选项类型 */
export type FilterItemType = 'select' | 'daterange'

/** 下拉选项 */
export interface FilterOption {
  label: string
  value: any
}

/** 筛选项配置 */
export interface FilterItemConfig {
  key: string
  title: string
  type: FilterItemType
  multiple?: boolean
  options?: FilterOption[]
  closable?: boolean
}

/** 标签项 */
interface TagItem {
  key: string
  label: string
  closable: boolean
}

interface Props {
  keyword?: string
  items: FilterItemConfig[]
  moreItems?: FilterItemConfig[]
  modelValue: Record<string, any>
}

const props = withDefaults(defineProps<Props>(), {
  keyword: '',
  moreItems: () => [],
  modelValue: () => ({}),
})

const emit = defineEmits<{
  'update:modelValue': [value: Record<string, any>]
  'update:keyword': [value: string]
  search: []
  filterChange: [value: Record<string, any>]
}>()

/** 底部弹出面板 */
const bottomPanelVisible = ref(false)
const bottomPanelType = ref<'select' | 'daterange-start' | 'daterange-end' | null>(null)
const activeItemKey = ref<string | null>(null)

/** 更多抽屉 */
const moreDrawerVisible = ref(false)

/** 临时值 */
const tempSelectValue = ref<any>(null)
const tempDateStart = ref<Date | null>(null)
const tempDateEnd = ref<Date | null>(null)

/** 日期滚轮选择器 */
const dateRollerVisible = ref(false)
const dateRollerValue = ref(new Date())
const dateRollerTarget = ref<'start' | 'end' | null>(null)

/** 标签列表 */
const tagsList = ref<TagItem[]>([])

/** 激活数量 */
const activeCount = computed(() => {
  let count = 0
  const allItems = [...props.items, ...props.moreItems]
  for (const item of allItems) {
    const value = props.modelValue[item.key]
    if (item.type === 'select') {
      if (Array.isArray(value) && value.length > 0) count += value.length
      else if (value !== undefined && value !== null) count++
    }
    else if (item.type === 'daterange') {
      if (value?.startDate || value?.endDate) count++
    }
  }
  return count
})

/** 初始化标签 */
function initTags() {
  tagsList.value = []
  const allItems = [...props.items, ...props.moreItems]
  for (const item of allItems) {
    const value = props.modelValue[item.key]
    if (item.type === 'select') {
      if (Array.isArray(value)) {
        for (const val of value) {
          const option = item.options?.find(o => o.value === val)
          if (option) {
            tagsList.value.push({ key: item.key, label: option.label, closable: item.closable !== false })
          }
        }
      }
      else if (value !== undefined && value !== null) {
        const option = item.options?.find(o => o.value === value)
        if (option) {
          tagsList.value.push({ key: item.key, label: option.label, closable: item.closable !== false })
        }
      }
    }
    else if (item.type === 'daterange') {
      if (value?.startDate && value?.endDate) {
        tagsList.value.push({ key: item.key, label: `${value.startDate} ~ ${value.endDate}`, closable: item.closable !== false })
      }
    }
  }
}

watch(() => props.modelValue, initTags, { deep: true, immediate: true })

/** 获取筛选项 */
function getActiveItem(): FilterItemConfig | undefined {
  return [...props.items, ...props.moreItems].find(i => i.key === activeItemKey.value)
}

/** 打开筛选项（底部弹出） */
function openFilter(item: FilterItemConfig) {
  activeItemKey.value = item.key
  bottomPanelVisible.value = true

  if (item.type === 'select') {
    bottomPanelType.value = 'select'
    tempSelectValue.value = Array.isArray(props.modelValue[item.key])
      ? [...props.modelValue[item.key]]
      : props.modelValue[item.key] ?? null
  }
  else if (item.type === 'daterange') {
    const val = props.modelValue[item.key]
    tempDateStart.value = val?.startDate ? new Date(val.startDate) : null
    tempDateEnd.value = val?.endDate ? new Date(val.endDate) : null
    // 直接弹出开始日期选择器
    openDateRoller('start')
  }
}

/** 打开日期滚轮选择器 */
function openDateRoller(target: 'start' | 'end') {
  dateRollerTarget.value = target
  dateRollerValue.value = target === 'start'
    ? (tempDateStart.value || new Date())
    : (tempDateEnd.value || new Date())
  dateRollerVisible.value = true
}

/** 日期滚轮确认 */
function onDateRollerConfirm() {
  const selectedDate = new Date(dateRollerValue.value)
  if (dateRollerTarget.value === 'start') {
    tempDateStart.value = selectedDate
    dateRollerVisible.value = false
    // 自动弹出结束日期选择器
    setTimeout(() => openDateRoller('end'), 300)
  }
  else {
    tempDateEnd.value = selectedDate
    dateRollerVisible.value = false
    // 应用日期筛选
    applyDateFilter()
  }
}

/** 应用日期筛选 */
function applyDateFilter() {
  const key = activeItemKey.value
  if (!key) return
  const newValue = { ...props.modelValue }
  newValue[key] = {
    startDate: tempDateStart.value ? formatDate(tempDateStart.value) : undefined,
    endDate: tempDateEnd.value ? formatDate(tempDateEnd.value) : undefined,
  }
  emit('update:modelValue', newValue)
  emit('filterChange', newValue)
  closeBottomPanel()
}

/** 格式化日期 */
function formatDate(date: Date | null): string {
  if (!date) return ''
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

/** 选择选项 */
function selectOption(option: FilterOption) {
  const item = getActiveItem()
  if (!item) return
  if (item.multiple) {
    if (!Array.isArray(tempSelectValue.value)) tempSelectValue.value = []
    const idx = tempSelectValue.value.indexOf(option.value)
    if (idx > -1) tempSelectValue.value.splice(idx, 1)
    else tempSelectValue.value.push(option.value)
  }
  else {
    tempSelectValue.value = tempSelectValue.value === option.value ? null : option.value
  }
}

/** 确认选择 */
function confirmSelect() {
  const key = activeItemKey.value
  if (!key) return
  const newValue = { ...props.modelValue }
  newValue[key] = tempSelectValue.value ?? undefined
  emit('update:modelValue', newValue)
  emit('filterChange', newValue)
  closeBottomPanel()
}

/** 关闭底部面板 */
function closeBottomPanel() {
  bottomPanelVisible.value = false
  bottomPanelType.value = null
  activeItemKey.value = null
}

/** 打开更多 */
function openMoreDrawer() {
  moreDrawerVisible.value = true
}

/** 清除标签 */
function clearTag(key: string) {
  const newValue = { ...props.modelValue }
  const item = [...props.items, ...props.moreItems].find(i => i.key === key)
  if (item?.type === 'select') {
    newValue[key] = undefined
  }
  else if (item?.type === 'daterange') {
    newValue[key] = { startDate: undefined, endDate: undefined }
  }
  emit('update:modelValue', newValue)
  emit('filterChange', newValue)
}

/** 清除所有 */
function clearAll() {
  const newValue: Record<string, any> = {}
  const allItems = [...props.items, ...props.moreItems]
  for (const item of allItems) {
    if (item.type === 'select') newValue[item.key] = undefined
    else if (item.type === 'daterange') newValue[item.key] = { startDate: undefined, endDate: undefined }
  }
  emit('update:modelValue', newValue)
  emit('filterChange', newValue)
}

function handleSearch() {
  emit('update:keyword', props.keyword)
  emit('search')
}

/** 更多确认 */
function confirmMore() {
  moreDrawerVisible.value = false
}
</script>

<template>
  <div class="mobile-filter">
    <!-- 搜索框 -->
    <div class="mobile-filter__search">
      <van-search
        :model-value="keyword"
        placeholder="搜索"
        shape="round"
        @update:model-value="emit('update:keyword', $event)"
        @search="handleSearch"
        @clear="handleSearch"
      />
    </div>

    <!-- 筛选栏 -->
    <div class="mobile-filter__bar">
      <div
        v-for="item in items"
        :key="item.key"
        class="mobile-filter__btn"
        :class="{ 'mobile-filter__btn--active': activeItemKey === item.key && bottomPanelVisible }"
        @click="openFilter(item)"
      >
        <span>{{ item.title }}</span>
        <svg class="mobile-filter__btn-arrow" :class="{ 'mobile-filter__btn-arrow--open': activeItemKey === item.key && bottomPanelVisible }" viewBox="0 0 1024 1024">
          <path d="M512 714.666667c-12.8 0-25.6-4.266667-34.133333-12.8L153.6 377.6c-21.333333-21.333333-21.333333-51.2 0-72.533333 21.333333-21.333333 51.2-21.333333 72.533333 0L512 588.8l285.866667-285.866667c21.333333-21.333333 51.2-21.333333 72.533333 0 21.333333 21.333333 21.333333 51.2 0 72.533333L546.133333 701.866667c-8.533333 8.533333-21.333333 12.8-34.133333 12.8z" />
        </svg>
      </div>

      <div v-if="moreItems?.length" class="mobile-filter__more" @click="openMoreDrawer">
        <IconFilter class="mobile-filter__more-icon" />
        <span>更多</span>
        <van-badge v-if="activeCount > 0" :content="activeCount" :max="99" />
      </div>
    </div>

    <!-- 已选标签 -->
    <div v-if="tagsList.length > 0" class="mobile-filter__tags">
      <div v-for="(tag, index) in tagsList" :key="`${tag.key}-${index}`" class="mobile-filter__tag">
        <span class="mobile-filter__tag-label">{{ tag.label }}</span>
        <IconX v-if="tag.closable" class="mobile-filter__tag-close" @click="clearTag(tag.key)" />
      </div>
      <span class="mobile-filter__clear" @click="clearAll">清除</span>
    </div>

    <!-- 底部弹出面板：选项选择 -->
    <van-popup v-model:show="bottomPanelVisible" position="bottom" round :style="{ zIndex: 2000 }">
      <div v-if="bottomPanelType === 'select'" class="mobile-filter__bottom-panel">
        <div class="mobile-filter__bottom-header">
          <van-button size="small" type="default" @click="closeBottomPanel">取消</van-button>
          <span class="mobile-filter__bottom-title">{{ getActiveItem()?.title }}</span>
          <van-button size="small" type="primary" @click="confirmSelect">确定</van-button>
        </div>
        <div class="mobile-filter__options">
          <div
            v-for="option in getActiveItem()?.options"
            :key="option.value"
            class="mobile-filter__option"
            :class="{ 'mobile-filter__option--selected': tempSelectValue === option.value || (Array.isArray(tempSelectValue) && tempSelectValue.includes(option.value)) }"
            @click="selectOption(option)"
          >
            {{ option.label }}
          </div>
        </div>
      </div>

      <!-- 日期范围显示（选择后展示） -->
      <div v-else-if="bottomPanelType === 'daterange-start' || bottomPanelType === 'daterange-end'" class="mobile-filter__bottom-panel">
        <div class="mobile-filter__bottom-header">
          <span class="mobile-filter__bottom-title">选择日期范围</span>
        </div>
        <div class="mobile-filter__date-preview">
          <div class="mobile-filter__date-item">
            <span class="label">开始日期</span>
            <span class="value">{{ formatDate(tempDateStart) || '未选择' }}</span>
          </div>
          <span class="sep">~</span>
          <div class="mobile-filter__date-item">
            <span class="label">结束日期</span>
            <span class="value">{{ formatDate(tempDateEnd) || '未选择' }}</span>
          </div>
        </div>
        <div class="mobile-filter__footer">
          <van-button block round type="default" @click="closeBottomPanel">取消</van-button>
          <van-button block round type="primary" @click="applyDateFilter">确定</van-button>
        </div>
      </div>
    </van-popup>

    <!-- 日期滚轮选择器 -->
    <van-popup v-model:show="dateRollerVisible" position="bottom" round :style="{ zIndex: 3000 }">
      <van-date-picker
        v-model="dateRollerValue"
        :title="dateRollerTarget === 'start' ? '选择开始日期' : '选择结束日期'"
        @confirm="onDateRollerConfirm"
        @cancel="dateRollerVisible = false"
      />
    </van-popup>

    <!-- 更多筛选抽屉 -->
    <Teleport to="body">
      <Transition name="drawer-slide">
        <div v-if="moreDrawerVisible" class="mobile-filter__drawer-overlay" @click.self="moreDrawerVisible = false">
          <div class="mobile-filter__drawer">
            <div class="mobile-filter__drawer-header">
              <span>更多筛选</span>
              <IconX class="mobile-filter__drawer-close" @click="moreDrawerVisible = false" />
            </div>
            <div class="mobile-filter__drawer-content">
              <p style="color: var(--color-text-dim); text-align: center; padding: 40px 0;">暂无更多筛选项</p>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped lang="scss">
.mobile-filter {
  position: relative;

  &__search { background: $color-bg-base; }

  &__bar {
    display: flex;
    align-items: center;
    gap: $spacing-sm;
    padding: 0 $spacing-base $spacing-sm;
    background: $color-bg-base;
    overflow-x: auto;
    white-space: nowrap;
    scrollbar-width: none;
    &::-webkit-scrollbar { display: none; }
  }

  &__btn {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 6px 12px;
    font-size: $font-size-sm;
    color: $color-text-regular;
    background: $color-bg-card;
    border: 1px solid $color-border;
    border-radius: $radius-md;
    cursor: pointer;
    transition: all $transition-fast;
    flex-shrink: 0;
    &--active {
      color: $color-primary;
      border-color: $color-border-accent;
      background: $color-primary-dim;
    }
  }

  &__btn-arrow {
    width: 12px;
    height: 12px;
    fill: currentColor;
    transition: transform $transition-fast;
    &--open { transform: rotate(180deg); }
  }

  &__more {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 6px 12px;
    font-size: $font-size-sm;
    color: $color-primary;
    background: $color-primary-dim;
    border: 1px solid $color-border-accent;
    border-radius: $radius-md;
    cursor: pointer;
    flex-shrink: 0;
    margin-left: auto;
    &:active { opacity: 0.8; }
  }

  &__more-icon { width: 14px; height: 14px; }

  &__tags {
    display: flex;
    flex-wrap: wrap;
    gap: $spacing-sm;
    padding: 0 $spacing-base $spacing-sm;
    background: $color-bg-base;
  }

  &__tag {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 4px 8px;
    font-size: $font-size-xs;
    color: $color-primary;
    background: $color-primary-dim;
    border-radius: $radius-sm;
  }

  &__tag-close {
    width: 12px;
    height: 12px;
    cursor: pointer;
    opacity: 0.6;
    &:hover { opacity: 1; }
  }

  &__clear {
    font-size: $font-size-xs;
    color: $color-text-dim;
    cursor: pointer;
    padding: 4px 8px;
    &:hover { color: $color-text-secondary; }
  }

  &__bottom-panel {
    padding: $spacing-base;
    background: $color-bg-card;
    max-height: 60vh;
    display: flex;
    flex-direction: column;
  }

  &__bottom-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: $spacing-base;
    flex-shrink: 0;
  }

  &__bottom-title {
    font-size: $font-size-md;
    font-weight: 600;
    color: $color-text-primary;
  }

  &__options {
    display: flex;
    flex-wrap: wrap;
    gap: $spacing-sm;
    max-height: 400px;
    overflow-y: auto;
    padding: $spacing-sm 0;
  }

  &__option {
    padding: 10px 16px;
    min-height: 40px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: $font-size-sm;
    color: $color-text-regular;
    background: $color-bg-base;
    border: 1px solid $color-border;
    border-radius: $radius-md;
    cursor: pointer;
    transition: all $transition-fast;
    user-select: none;
    -webkit-tap-highlight-color: transparent;
    &--selected {
      color: $color-primary;
      background: $color-primary-dim;
      border-color: $color-border-accent;
    }
    &:active {
      opacity: 0.7;
    }
  }

  &__date-preview {
    display: flex;
    align-items: center;
    gap: $spacing-base;
    padding: $spacing-md;
    background: $color-bg-base;
    border-radius: $radius-md;
    margin-bottom: $spacing-base;
  }

  &__date-item {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: 4px;
    .label {
      font-size: $font-size-xs;
      color: $color-text-dim;
    }
    .value {
      font-size: $font-size-md;
      color: $color-text-primary;
      font-weight: 500;
    }
  }

  &__sep {
    color: $color-text-dim;
    font-size: $font-size-lg;
  }

  &__footer {
    display: flex;
    gap: $spacing-base;
  }

  &__drawer-overlay {
    position: fixed;
    inset: 0;
    z-index: 1000;
    background: rgba(0, 0, 0, 0.5);
    display: flex;
    justify-content: flex-end;
  }

  &__drawer {
    width: 80%;
    max-width: 320px;
    height: 100%;
    background: $color-bg-card;
    display: flex;
    flex-direction: column;
  }

  &__drawer-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: $spacing-md $spacing-base;
    border-bottom: 1px solid $color-border;
    font-size: $font-size-md;
    font-weight: 600;
    color: $color-text-primary;
  }

  &__drawer-close {
    width: 20px;
    height: 20px;
    cursor: pointer;
    opacity: 0.6;
    &:hover { opacity: 1; }
  }

  &__drawer-content {
    flex: 1;
    overflow-y: auto;
    padding: $spacing-base;
  }
}

.drawer-slide-enter-active,
.drawer-slide-leave-active {
  transition: opacity $transition-base;
  .mobile-filter__drawer { transition: transform $transition-base; }
}
.drawer-slide-enter-from,
.drawer-slide-leave-to {
  opacity: 0;
  .mobile-filter__drawer { transform: translateX(100%); }
}
</style>
