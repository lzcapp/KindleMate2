# AGENTS.md — 项目约定

给自动化代理（与贡献者）的项目约定。**改动前先读这一页。**

## 文档同步（重要）

- **改了 `README.md` 后，必须同步 `docs/index.html`（GitHub Pages 站点）**。
  站点是从 README 手工整理的，二者会漂移。用户可见信息的改动（系统要求、下载/校验、安装步骤、
  兼容性、许可证、特性列表等）要**一起改**。
- `README.md` ↔ `README.en.md` 互相对应，需同步维护。
- `arch.md` 是架构/构建/发布/边界文档；涉及这些的改动要更新它。
- 相关自动引用：站点根 `https://github.lzc.app/KindleMate2/`（Pages 源 = `main` 分支 `/docs`）。

## 分支与 PR

- **`main` 受保护**：需要 PR + 3 个必需状态检查 —— `build-and-test`、`cross-platform (ubuntu-latest)`、
  `cross-platform (macos-latest)`。**不要在 main 上直接提交/推送**（会被 `GH006` 拒绝）。
  小改动同样走：分支 → 提交 → PR → 等 CI 全绿 → 评论 review。
- **代理默认不合并**：可以提交、推送、开 PR、等 CI 全绿、`gh pr review --comment` 发自审评论，
  但**不要 merge** —— 合并由维护者人工过目后执行（2026-09-25 约定；用户明确要求合并时除外）。
- 提交使用 **GPG 签名**（本机 `commit.gpgsign=true`）。
- 分支命名沿用仓库习惯：`fix/…`、`feat/…`、`chore/…`、`docs/…`、`ci/…`。

## 版本与发布

- **版本号来自 git tag**（日期式，如 `2026.09.24`）。**不维护 `csproj <Version>`**：
  `KindleMate2.Avalonia.csproj` 的 MSBuild 目标 `Km2SetVersionFromGitTag` 在构建期取最新 tag（去前导零）派生；
  发布时 `release.yml` 用 `-p:Version` 覆盖。
- **发版**：`Actions → release-prep → Run workflow`（可填版本号，留空 = 当天北京时间）。
  它会打 **GPG 签名的 annotated tag**、推 tag、dispatch `release.yml`（跨平台构建 + 建 Release）。
- **自动更新走 GitHub Releases API**（`Application/Services/UpdateChecker`），**不使用 AppCast**。
  下载后按 Release 里的 `SHA256SUMS` 校验；Release 另附 `SHA256SUMS.asc` 与公钥 `KindleMate2-release-key.asc`。
  （旧的 `docs/update_*.xml` 已删除，勿再引入。）

## 测试

- `dotnet test` 默认**不碰真机/外网**：真机探测需 `KM2_MANUAL_DEVICE_TESTS=1`，联网探测需
  `KM2_MANUAL_NETWORK_TESTS=1`；两类均带 `[Trait("Category","Manual")]`。
- 视图层（Avalonia VM）行为单测够不着，靠 `--smoke` 探针 + CI 的 grep 断言覆盖。

## 许可证

- `LICENSE` 为 **GPL-3.0**（README 徽章/文案需一致）。改动许可证前注意外部贡献者（如 PR #35 的作者）的同意。
