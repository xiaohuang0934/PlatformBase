import { computed, onMounted, onUnmounted, ref } from 'vue'

const MOBILE_BREAKPOINT = 768

/** Use Device */
export function useDevice() {
  const width = ref(window.innerWidth)
  const isMobile = computed(() => width.value < MOBILE_BREAKPOINT)

  /** On Resize */
  function onResize() {
    width.value = window.innerWidth
  }

  onMounted(() => window.addEventListener('resize', onResize))
  onUnmounted(() => window.removeEventListener('resize', onResize))

  return { width, isMobile }
}
