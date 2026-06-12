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

  function addErrorLog(err: Error, vm: any, info: string) {
    logs.value.push({
      err,
      vm,
      info,
      url: window.location.href,
      time: new Date().toISOString(),
    })
  }

  function clearErrorLog() {
    logs.value = []
  }

  return { logs, addErrorLog, clearErrorLog }
})
