# JLZ Project View Enhancer

一个独立的 Unity UPM 包，用于增强 Project 窗口的显示与交互体验。

## 功能

- 文件夹颜色与字体样式自定义
- 缩进引导线
- 当前选中路径高亮
- 交替行背景
- 双栏模式下的文件夹符号覆盖
- 基于反射的 TreeView 布局补丁

## 包结构

- `Editor/JLZ.ProjectViewEnhancer.Editor.asmdef`：编辑器程序集
- `Editor/ProjectViewEnhancer`：编辑器脚本
- `Editor/Icons/ExtractedUnityIcons`：可选的自定义覆盖图标资源

## 设置文件

设置保存在 `ProjectSettings/ProjectViewEnhancerSettings.asset`。

## 安装

在项目的 `Packages/manifest.json` 中加入以下依赖之一。

本地文件依赖：

```json
"com.jlz.project-view-enhancer": "file:../LocalPackages/com.jlz.project-view-enhancer"
```

Git 依赖：

```json
"com.jlz.project-view-enhancer": "https://your-git-host/your-org/com.jlz.project-view-enhancer.git#1.0.0"
```

## 说明

- 这个仓库既可以作为本地文件包使用，也可以作为基于 Git 的 UPM 包使用。
- Unity 在项目中会将它解析为 `Packages/com.jlz.project-view-enhancer`。
- 旧路径 `Assets/Game/Art/Editor/ProjectViewEnhancer/ExtractedUnityIcons` 下的图标资源会自动映射到当前包路径。
