import ElementPlus from 'element-plus'
import { createPinia } from 'pinia'
import { createApp } from 'vue'
import App from './App.vue'
import { permission, userType } from './directives/permission'

import router from './router'
import { useErrorLogStore } from './stores/errorLog'
import 'element-plus/theme-chalk/index.css'
import 'vant/lib/index.css'

import './styles/tokens.scss'
import './styles/reset.scss'
import './styles/glass.scss'
import './styles/desktop.scss'
import './styles/mobile.scss'

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(ElementPlus, { size: 'default' })
app.directive('permission', permission)
app.directive('user-type', userType)

// 前端错误捕获
app.config.errorHandler = (err: unknown, instance, info) => {
  const errorLog = useErrorLogStore()
  errorLog.addErrorLog(err as Error, instance, info)
}

app.mount('#app')
