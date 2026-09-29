# 跨电脑提交规则

## Mac 未安装 Houdini

仓库保留 `Assets/Plugins/HoudiniEngineUnity/` 及目录 `.meta`，供 Windows 和安装了 Houdini 的电脑使用。本机删除插件不等于要从仓库删除插件；不要提交这些删除，也不要用 `git rm --cached` 取消跟踪。

本次 Mac 配置仅写入本地 Git 索引和 `.git/hooks/pre-commit`，不会通过 push/pull 同步到其他电脑：

- 已删除的 Houdini 插件文件及目录 `.meta` 标为 `skip-worktree`，正常暂存时不纳入本机删除。
- `Packages/manifest.json` 与 `Packages/packages-lock.json` 同样标为 `skip-worktree`，保留本机移除 Windows 绝对路径包依赖后的内容。
- 本地提交钩子拒绝提交 Houdini 插件、上述两个包文件，以及本次出现自动升级的五个渲染配置文件。

`skip-worktree` 不是永久忽略或同步隔离。切换分支、合并、拉取涉及这些路径的更新时，Git 可能要求处理本地差异；操作前备份本机配置，出现冲突时停止处理，不要强制覆盖。钩子也不能防止使用 `--no-verify` 绕过检查。其他克隆不会自动获得本机保护。

需要重新审核包配置时，可先运行：

```sh
git update-index --no-skip-worktree -- Packages/manifest.json Packages/packages-lock.json
git diff -- Packages/manifest.json Packages/packages-lock.json
```

需要重新启用 Houdini 时，先关闭 Unity，取消插件路径的本地标记，再从当前提交恢复插件（确认这些路径没有需要保留的本地文件）：

```sh
git ls-files -z -- Assets/Plugins/HoudiniEngineUnity Assets/Plugins/HoudiniEngineUnity.meta | git update-index --no-skip-worktree -z --stdin
git restore --source=HEAD --worktree -- Assets/Plugins/HoudiniEngineUnity Assets/Plugins/HoudiniEngineUnity.meta
```

有意提交受保护文件时，先审核差异，再调整本机 `.git/hooks/pre-commit` 的保护名单；不要日常绕过钩子。

## 提交前检查

1. 用 `git status` 和 `git diff` 确认修改范围，按文件暂存，避免直接提交所有改动。
2. 用 `git diff --cached --name-status` 与 `git diff --cached` 审核最终提交内容。
3. 资源及对应 `.meta` 一起提交，避免破坏 GUID 引用。
4. 不提交本机缓存、机器绝对路径、仅由本机导入或包版本差异引起的配置升级。没有 Houdini 的电脑应避免保存依赖该插件的源场景或预制体，以免把组件丢失写入资源。

## 现有忽略规则审核

- Unity 的 Library、Temp、Logs、UserSettings 等已忽略。
- 新增忽略生成的 `Assets/SceneDependencyCache/` 及对应 `.meta`。
- 现有规则忽略整个 `Packages/`、`ProjectSettings/`，以及部分单独文件。这些规则**不会忽略已经跟踪的文件的修改**，也不能保护其他电脑免受已提交删除的影响。
- 上述整目录忽略会让新增包与项目设置容易漏交。本次保留原规则，避免扩大影响；以后若要统一整理，应先在各电脑确认 Unity 版本、包依赖和机器路径，再单独审核修改。
- 当前包清单中的 SideFX VAT 依赖指向 Windows 的 `D:/Program Files/...`。仓库暂保留该依赖，本次 Mac 上移除它的修改不提交。

本次共享提交仅包含本说明和缓存忽略规则，不改变项目资源、插件或依赖。
