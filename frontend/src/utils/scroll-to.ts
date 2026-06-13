/** 平滑滚动到指定位置 */
export function scrollTo(to: number, duration = 500) {
  const start = window.scrollY
  const change = to - start
  const startTime = performance.now()

  /** Animate Scroll */
  function animateScroll(currentTime: number) {
    const elapsed = currentTime - startTime
    const progress = Math.min(elapsed / duration, 1)

    const easeInOutQuad = progress < 0.5
      ? 2 * progress * progress
      : -1 + (4 - 2 * progress) * progress

    window.scrollTo(0, start + change * easeInOutQuad)

    if (elapsed < duration) {
      requestAnimationFrame(animateScroll)
    }
  }

  requestAnimationFrame(animateScroll)
}
