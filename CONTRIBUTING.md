# 贡献参考文档

## 代码
这个程序使用 [.NET 10](https://learn.microsoft.com/zh-cn/dotnet/core/whats-new/dotnet-10/overview) 框架。  
这个程序的代码并不复杂，都在 [PinAction/Program.cs](PinAction/Program.cs) 里面。参考代码注释足以理解。

## 安装程序
我使用 [Inno Setup](https://jrsoftware.org/isinfo.php) 制作安装程序，信息存在 [Installer.iss](Installer.iss) 中。

## 构建
> 项目中有一个工作流文件 [.github/workflows/release.yaml](.github/workflows/release.yaml) 可以用来自动构建。  

```bash
dotnet publish PinAction --configuration Release
```

或者使用 [Visual Studio](https://visualstudio.microsoft.com/) 生成。

## 本地化
如果你想修改本地化内容，_可以_ 用 [Visual Studio](https://visualstudio.microsoft.com/) 打开 `PinAction/PinAction.slnx`，然后打开项目中的 `Resources/Strings.resx`，在那里编辑现有本地化内容。  
如果你想新增本地化语言，请添加 `Resources/Strings.<区域代码>.resx` 文件。

请基于 `zh-CN` (简体中文) 进行翻译。

## 提交消息
对于 Git 提交消息，请遵守 `<type>(<scope>): <subject>` 的格式。  
对于中断性修改，请在 `<type>(<scope>)` 后面添加 `!`，即 `<type>(<scope>)!: <subject>` 的格式。  
如果可以，请尽量使用英文或中文（简繁均可）写提交消息，不要混用（专有名词除外）。
