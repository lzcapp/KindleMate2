import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
    plugins: [vue()],
    build: {
        // 将打包产物直接输出到 C# 项目的 wwwroot 目录下
        outDir: '../KindleMate2.PhotinoUI/wwwroot',
        // 打包前清空旧文件
        emptyOutDir: true,
    }
})