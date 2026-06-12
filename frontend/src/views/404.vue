<template>
  <div class="error-page">
    <h1 class="error-page__code">
      404
    </h1>
    <p class="error-page__message">
      页面不存在
    </p>
    <el-button type="primary" @click="$router.push('/')">
      返回首页
    </el-button>
  </div>
</template>

<style scoped lang="scss">
.error-page {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  text-align: center;

  &__code {
    font-size: 72px;
    font-weight: 700;
    color: $color-text-secondary;
    margin-bottom: $spacing-md;
  }

  &__message {
    font-size: $font-size-md;
    color: $color-text-secondary;
    margin-bottom: $spacing-xl;
  }
}
</style>
