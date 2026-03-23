# Pin Action

> <p style="text-align: center"><a href="README.md">English</a></p>

固定工作流依赖版本到全长哈希。  

![使用示例](example.gif)

## 如何使用

你可以通过 winget 安装:  

```bash
winget install --id DuckStudio.PinAction -s winget -e
```

然后运行:

```bash
pinaction <文件或目录>
```

你可以同时传递多个文件或目录。  
对于目录，它会尝试递归查找其中的 `.yaml` 或 `.yml` 文件。

## 一些问题
### 它支持使用 GitHub Token 吗？

当我学会在 C# 中读取和存储 Token 时，我想会的。  
不过当前不支持，你可以在源代码中硬编码一个。

### 它能跳过一些工作流吗？

请修改代码，代码中有给示例。

### 为什么要固定版本到全长哈希？

这是 [GitHub 推荐的做法](https://docs.github.com/zh/actions/reference/security/secure-use#using-third-party-actions)，在部分项目中被[视为强制要求](https://github.blog/changelog/2025-08-15-github-actions-policy-now-supports-blocking-and-sha-pinning-actions/)。  
如果你的工作流依赖没有启用[不可变版本](https://docs.github.com/zh/code-security/concepts/supply-chain-security/immutable-releases)，则你的工作流可能会受到上游依赖再次修改同一版本的影响。  
将版本固定到全长哈希可以确保你的工作流始终使用相同的代码，即使上游依赖修改了同一版本。  

### 什么是“全长哈希”？

指定的工作流版本（Tag）对应的 Git Commit Hash。

### 这个程序是怎么替换内容的？

我很懒，所以我根本没有解析 YAML，只是对带有 `uses:` 的行 `.Split()` 了几下后用正则匹配。  
详见源代码文件中的 `PinActionHash` 方法。

### 为什么它没有图标？

我不会画。思考了一小时后发现我已急哭。

## 许可

本程序使用 [MIT 许可证](https://github.com/DuckDuckStudio/PinAction/blob/main/LICENSE.txt)。

### 依赖

本程序的实现离不开这些项目，感谢开源社区！

| 包 | 许可证 |
|-----|-----|
| [Octokit](https://www.nuget.org/packages/Octokit/) | MIT License |
| [DuckStudio.CatFood](https://www.nuget.org/packages/DuckStudio.CatFood) | Apache License 2.0 |
| [Spectre.Console](https://www.nuget.org/packages/Spectre.Console/) | MIT License |

有关这些依赖的许可文件，请参见 [NOTICE.md](https://github.com/DuckDuckStudio/PinAction/blob/main/NOTICE.md)。
