using Octokit;
using Spectre.Console;
using PinAction.Resources;
using DuckStudio.CatFood.Functions;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace PinAction
{
    internal partial class Program
    {
        /// <summary>
        /// 入口函数
        /// </summary>
        /// <param name="args">参数</param>
        /// <returns>退出代码</returns>
        static int Main(string[] args)
        {
            if (
                (args.Length == 0) || // 不提供参数
                (
                    // 单独提供帮助相关参数
                    (args.Length == 1) &&
                    (args[0] is "help"
                             or "--help"
                             or "-h"
                             or "/?")
                )
            )
            {
                Console.WriteLine(Strings.Help);
                Console.WriteLine();
                Console.WriteLine(Strings.HelpLine1);
                Console.WriteLine(Strings.HelpLine2);
                Console.WriteLine();
                Console.WriteLine($"-v ver --ver --version   {Strings.HelpShowVersion}");
                Console.WriteLine($"--license license        {Strings.HelpShowLicense}");
                Console.WriteLine($"-h --help help /?        {Strings.HelpShowHelp}");
                return 0;
            }

            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "ver":
                    case "--version":
                    case "--ver":
                    case "-v":
                        Console.MarkupLine($"PinAction {Strings.Version} [green]develop[/] by [link=https://duckduckstudio.github.io/yazicbs.github.io/]鸭鸭「カモ」[/]");
                        Console.WriteLine();
                        Console.MarkupLine(Strings.HelpVer2License);
                        return 0;
                    case "license":
                    case "--license":
                        Table table = new Table()
                            .Border(TableBorder.Rounded)
                            .ShowRowSeparators();
                        table.AddColumn(Strings.Package).AddColumn(Strings.License);
                        table.AddRow("PinAction", "[link=https://github.com/DuckDuckStudio/PinAction/blob/main/LICENSE.txt]MIT License[/]");
                        table.AddRow("Octokit", "[link=https://github.com/octokit/octokit.net/blob/main/LICENSE.txt]MIT License[/]");
                        table.AddRow("DuckStudio.CatFood", "[link=https://github.com/DuckDuckStudio/DuckStudio.CatFood/blob/main/LICENSE]Apache License 2.0[/]");
                        table.AddRow("Spectre.Console", "[link=https://github.com/spectreconsole/spectre.console/blob/main/LICENSE.md]MIT License[/]");
                        AnsiConsole.Write(table); // 不要改成 Console.Write()
                        return 0;
                }
            }

            // 循环每个参数
            foreach (string path in args)
            {
                // 转为绝对路径
                string fullPath = Path.GetFullPath(path);

                if (File.Exists(fullPath))
                {
                    if (!PinActionHash(fullPath))
                    {
                        return 1;
                    }
                }
                else if (Directory.Exists(fullPath))
                {
                    // 递归目录下的 .yaml / .yml 文件
                    foreach (string file in Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!PinActionHash(file))
                        {
                            return 1;
                        }
                    }
                }
                else
                {
                    Console.MarkupLine($"{Print.MSHead.Error} {string.Format(Strings.ErrorPathNotExist, AnsiMarkup.Escape(fullPath))}");
                    return 3;
                }
            }

            return 0;
        }

        /// <summary>
        /// 固定工作流版本到全长哈希。
        /// </summary>
        /// <param name="path">工作流文件路径</param>
        /// <returns>是否成功</returns>
        private static bool PinActionHash(string path)
        {
            // 读取文件内容，并按行分隔
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("uses:"))
                {
                    // 移除注释
                    string[] cleanLinePaths = lines[i].Split('#');

                    // 非注释内容匹配正则 "^\s+uses:\s*([^@]+)@([^@|\s]+)\s*$"
                    Match match = UsesRegex().Match(cleanLinePaths[0]);

                    if (match.Success)
                    {
                        string repo = match.Groups[1].Value;
                        string tag = match.Groups[2].Value;

                        Console.MarkupLine($"{Print.MSHead.Information} {string.Format(Strings.FindAction, AnsiMarkup.Escape(Path.GetRelativePath(Environment.CurrentDirectory, path)), AnsiMarkup.Escape($"{repo}@{tag}"))}");

                        // 在这里你可以定义排除哪些项
                        // 例如排除以 actions/ 开头的项（actions/*@*）
                        // if (repo.StartsWith("actions/"))
                        // {
                        //     Console.MarkupLine($"{Print.MSHead.Warning} 跳过 {AnsiMarkup.Escape($"{repo}@{tag}")}，因为它是官方工作流");
                        //     continue;
                        // }


                        // 操作前检查
                        // 检查是否已经是哈希值（40个十六进制字符）
                        if (HashRegex().IsMatch(tag))
                        {
                            Console.MarkupLine($"{Print.MSHead.Information} {string.Format(Strings.SkippingAlreadyPinnedHashes, AnsiMarkup.Escape($"{repo}@{tag}"))}");
                            continue;
                        }
                        // 检查仓库是否是 owner/repo 的格式
                        if (repo.Split('/').Length != 2)
                        {
                            Console.MarkupLine($"{Print.MSHead.Warning} {AnsiMarkup.Escape(repo)} 看起来不像是仓库的格式，跳过 {AnsiMarkup.Escape($"{repo}@{tag}")}");
                            continue;
                        }


                        string? hash;
                        if (!pinedActions.TryGetValue($"{repo}@{tag}", out hash))
                        {
                            // 尝试 tags/{tag} 和 heads/{tag}
                            foreach (string refType in new[] { "tags", "heads" })
                            {
                                try
                                {
                                    // 获取该版本的 git commit hash
                                    Reference reference = GitHubClient.Git.Reference.Get(repo.Split('/')[0], repo.Split('/')[1], $"{refType}/{tag}").Result;
                                    hash = reference.Object.Sha;

                                    pinedActions.TryAdd($"{repo}@{tag}", hash);
                                    break;
                                }
                                catch (AggregateException ex) when (ex.InnerException != null)
                                {
                                    Console.Markup($"{Print.MSHead.Warning} {Strings.ErrorGetHashFailed}");

                                    switch (ex.InnerException)
                                    {
                                        // 还要再试的用 break;
                                        // 最后一次的用 continue;
                                        // 直接整个程序失败的 return false;
                                        case Octokit.NotFoundException:
                                            if (refType == "tags")
                                            {
                                                AnsiConsole.MarkupLineInterpolated($"[yellow]{string.Format(Strings.ErrorTagNotFound, tag)}[/]");
                                                break;
                                            }
                                            else
                                            {
                                                AnsiConsole.MarkupLineInterpolated($"[red]{string.Format(Strings.ErrorBranchNotFound, tag, $"{repo}@{tag}")}[/]");
                                                continue;
                                            }
                                        case Octokit.RateLimitExceededException:
                                            Console.MarkupLine($"[yellow]{Strings.ErrorRateLimitExceeded}[/]");
                                            return false;
                                        default:
                                            AnsiConsole.MarkupLineInterpolated($"[red]{ex.InnerException.Message}[/]");
                                            continue;
                                    }
                                }
                            }
                        }
#if DEBUG
                        else
                        {
                            Console.MarkupLine($"{Print.MSHead.Debug} 读取缓存 {AnsiMarkup.Escape($"{repo}@{hash}")} # {AnsiMarkup.Escape(tag)}");
                        }
#endif

                        if (hash is null)
                        {
                            continue;
                        }

                        lines[i] = $"{cleanLinePaths[0].Replace($"{repo}@{tag}", $"{repo}@{hash}")} # {tag}";
                        if (cleanLinePaths.Length > 1)
                        {
                            // 将注释部分重新添加到行末
                            foreach (string commentPart in cleanLinePaths.Skip(1))
                            {
                                lines[i] += commentPart;
                            }
                        }

                        Console.MarkupLine($"{Print.MSHead.Success} {Strings.Pinned} {AnsiMarkup.Escape($"{repo}@{hash}")} # {AnsiMarkup.Escape(tag)}");
                    }
                }
            }

            // 将修改后的内容写回文件
            File.WriteAllLines(path, lines);
            return true;
        }

        private static readonly GitHubClient GitHubClient = new(new ProductHeaderValue("PinAction"))
        {
            // 如果你想让请求使用 GitHub Token，可以将 Token 临时填在这里
            // 记得用完后 撤销/删除/轮换 Token。
            // Credentials = new Credentials("Your Token")
        };

        /// <summary>
        /// <para>缓存已固定哈希值的 Action，第二次遇到时不用再去请求 GitHub API 获取。</para>
        /// <para>按 <c>repo@tag</c>, <c>hash</c> 一对存储。</para>
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> pinedActions = new();

        [GeneratedRegex(@"^\s*uses:\s*([^@]+)@([^@|\s]+)\s*$")]
        private static partial Regex UsesRegex();

        [GeneratedRegex(@"^[a-fA-F0-9]{40}$")]
        private static partial Regex HashRegex();
    }
}
