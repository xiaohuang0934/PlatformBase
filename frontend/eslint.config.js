import antfu from '@antfu/eslint-config'

export default antfu({
  vue: true,
  typescript: true,
  formatters: {
    css: true,
    html: true,
  },
  rules: {
    'no-console': 'warn',
    'vue/multi-word-component-names': 'off',
    'style/max-statements-per-line': 'off',
  },
})
