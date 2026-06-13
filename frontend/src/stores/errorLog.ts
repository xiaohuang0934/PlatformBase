import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface ErrorLogItem {
  err: Error
  vm: any
  info: string
  url: string
  time: string
}

export const useErrorLogStore = defineStore('errorLog', () => {
  const logs = ref<ErrorLogItem[]>([])

  /** 添加 Error Log */
  function addErrorLog(err: Error, vm: any, info: string) {
    logs.value.push({
      err,
      vm,
      info,
      url: window.location.href,
      time: new Date().toISOString(),
    })
  }

  /** 清空错误日志 */
  function clearErrorLog() {
    logs.value = []
  }

  return { logs, addErrorLog, clearErrorLog }
})
