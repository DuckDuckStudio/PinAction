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
        private static int Main(string[] args)
        {
            if (
                (args.Length == 0) || // 不提供参数
                args is ["help"
                    or "--help"
                    or "-h"
                    or "/?"]
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
                        AnsiConsole.MarkupLine($"PinAction {Strings.Version} [green]develop[/] by [link=https://duckduckstudio.github.io/yazicbs.github.io/]鸭鸭「カモ」[/]");
                        Console.WriteLine();
                        AnsiConsole.MarkupLine(Strings.HelpVer2License);
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
                        AnsiConsole.Write(table);
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
                    AnsiConsole.MarkupLine($"{Print.MSHead.Error} {string.Format(Strings.ErrorPathNotExist, fullPath)}");
                    return 3;
                }
            }

            return 0;
        }

        /// <summary>
        /// 扫描指定的工作流文件，并将其中的 <c>uses:</c> 引用固定为对应提交的哈希值。
        /// </summary>
        /// <param name="path">要处理的 YAML / YML 工作流文件路径。</param>
        /// <returns>若文件处理成功返回 <see langword="true"/>；失败返回 <see langword="false"/>。</returns>
        private static bool PinActionHash(string path)
        {
            // 读取文件内容，并按行分隔
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("uses:")) continue;

                // 移除注释
                string[] cleanLinePaths = lines[i].Split('#');

                // 非注释内容匹配正则 "^\s+uses:\s*([^@]+)@([^@|\s]+)\s*$"
                Match match = UsesRegex().Match(cleanLinePaths[0]);
                if (!match.Success) continue;

                string repo = match.Groups[1].Value;
                string tag = match.Groups[2].Value;

                AnsiConsole.MarkupLine($"{Print.MSHead.Information} {string.Format(Strings.FindAction, Path.GetRelativePath(Environment.CurrentDirectory, path), Markup.Escape($"{repo}@{tag}"))}");

                // 在这里你可以定义排除哪些项
                // 例如排除以 actions/ 开头的项（actions/*@*）
                // if (repo.StartsWith("actions/"))
                // {
                //     AnsiConsole.MarkupLine($"{Print.MSHead.Warning} 跳过 {repo}@{tag}，因为它是官方工作流");
                //     continue;
                // }


                // 操作前检查
                // 检查是否已经是哈希值（40个十六进制字符）
                if (HashRegex().IsMatch(tag))
                {
                    AnsiConsole.MarkupLine($"{Print.MSHead.Information} {string.Format(Strings.SkippingAlreadyPinnedHashes, Markup.Escape($"{repo}@{tag}"))}");
                    continue;
                }

                // 检查仓库是否是 owner/repo 的格式
                if (repo.Split('/').Length != 2)
                {
                    AnsiConsole.MarkupLine($"{Print.MSHead.Warning} {string.Format(Strings.NotARepository, Markup.Escape(repo), Markup.Escape($"{repo}@{tag}"))}");
                    continue;
                }


                if (!PinedActions.TryGetValue($"{repo}@{tag}", out string? hash))
                {
                    // 尝试 tags/{tag} 和 heads/{tag}
                    foreach (string refType in new[] { "tags", "heads" })
                    {
                        try
                        {
                            // 获取该版本的 git commit hash
                            Reference reference = GitHubClient.Git.Reference.Get(repo.Split('/')[0], repo.Split('/')[1], $"{refType}/{tag}").Result;
                            hash = reference.Object.Sha;

                            PinedActions.TryAdd($"{repo}@{tag}", hash);
                            break;
                        }
                        catch (AggregateException ex) when (ex.InnerException != null)
                        {
                            AnsiConsole.Markup($"{Print.MSHead.Warning} {Strings.ErrorGetHashFailed}");

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
                                    AnsiConsole.MarkupLine($"[yellow]{Strings.ErrorRateLimitExceeded}[/]");
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
                    AnsiConsole.MarkupLine($"{Print.MSHead.Debug} {Strings.ReadCache} {Markup.Escape($"{repo}@{hash}")} # {Markup.Escape(tag)}");
                }
#endif

                if (hash is null)
                {
                    continue;
                }

                string refinedTag = ResolveRefinedTag(repo, tag, hash);
                lines[i] = $"{cleanLinePaths[0].Replace($"{repo}@{tag}", $"{repo}@{hash}")} # {refinedTag}";
                if (cleanLinePaths.Length > 1)
                {
                    // 将注释部分重新添加到行末
                    foreach (string commentPart in cleanLinePaths.Skip(1))
                    {
                        lines[i] += commentPart;
                    }
                }

                AnsiConsole.MarkupLine($"{Print.MSHead.Success} {Strings.Pinned} {Markup.Escape($"{repo}@{hash}")} # {Markup.Escape(refinedTag)}");
            }

            // 将修改后的内容写回文件
            File.WriteAllLines(path, lines);
            return true;
        }

        /// <summary>
        /// 根据目标提交哈希尝试找回与该提交关联的更合适版本标签。
        /// </summary>
        /// <param name="repo">仓库名称，格式为 <c>owner/repo</c>。</param>
        /// <param name="tag">原始引用的版本或分支名称。</param>
        /// <param name="hash">对应的提交 SHA，通常为 40 位十六进制值。</param>
        /// <returns>若能在目标仓库中找到与提交 SHA 对应的标签，则返回最合适的标签名；否则返回原始 <paramref name="tag"/>。</returns>
        private static string ResolveRefinedTag(string repo, string tag, string hash)
        {
            if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(hash))
            {
                return tag;
            }

            string[] repoParts = repo.Split('/', 2, StringSplitOptions.TrimEntries);
            if (repoParts.Length != 2)
            {
                return tag;
            }

            try
            {
                IReadOnlyList<RepositoryTag> tags = GitHubClient.Repository.GetAllTags(repoParts[0], repoParts[1]).Result;
                if (tags.Count == 0)
                {
                    return tag;
                }

                RepositoryTag? sameCommitTag = tags
                    .Where(t => string.Equals(t.Commit.Sha, hash, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(t => t.Name, Comparer<string>.Create(CompareVersionStrings))
                    .FirstOrDefault();

                return sameCommitTag is not null ? sameCommitTag.Name : tag;
            }
            catch
            {
                return tag;
            }
        }

        /// <summary>
        /// 比较两个版本字符串，按数字段顺序进行自然排序。
        /// </summary>
        /// <param name="left">左侧版本字符串。</param>
        /// <param name="right">右侧版本字符串。</param>
        /// <returns>若 <paramref name="left"/> 小于 <paramref name="right"/> 返回负数；相等返回 0；大于返回正数。</returns>
        private static int CompareVersionStrings(string left, string right)
        {
            int[] leftParts = [.. NumberRegex().Matches(left).Select(m => int.Parse(m.Value))];
            int[] rightParts = [.. NumberRegex().Matches(right).Select(m => int.Parse(m.Value))];
            int maxLen = Math.Max(leftParts.Length, rightParts.Length);

            for (int i = 0; i < maxLen; i++)
            {
                int leftPart = i < leftParts.Length ? leftParts[i] : 0;
                int rightPart = i < rightParts.Length ? rightParts[i] : 0;
                int result = leftPart.CompareTo(rightPart);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        /// <summary>
        /// GitHub API 客户端。
        /// </summary>
        private static readonly GitHubClient GitHubClient = new(new ProductHeaderValue("PinAction"))
        {
            // 如果你想让请求使用 GitHub Token，可以将 Token 临时填在这里
            // 记得用完后 撤销/删除/轮换 Token。
            // Credentials = new Credentials("Your Token")
        };

        /// <summary>
        /// <para>缓存已固定哈希值的 Action，避免同一 <c>repo@tag</c> 重复调用 GitHub API。</para>
        /// <para>键为 <c>repo@tag</c>，值为对应的提交 SHA。</para>
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> PinedActions = new();

        /// <summary>
        /// 匹配工作流中的 <c>uses:</c> 引用，提取仓库名和对应版本或分支名。
        /// </summary>
        /// <returns>用于解析 <c>owner/repo@ref</c> 形式的正则对象。</returns>
        [GeneratedRegex(@"^\s*uses:\s*([^@]+)@([^@|\s]+)\s*$")]
        private static partial Regex UsesRegex();

        /// <summary>
        /// 判断给定字符串是否为 40 位十六进制提交哈希。
        /// </summary>
        /// <returns>用于验证 SHA-1 哈希格式的正则对象。</returns>
        [GeneratedRegex(@"^[a-fA-F0-9]{40}$")]
        private static partial Regex HashRegex();

        /// <summary>
        /// 匹配版本字符串中的数字段，供自然排序比较版本号时使用。
        /// </summary>
        /// <returns>用于提取版本号中的数字序列的正则对象。</returns>
        [GeneratedRegex(@"\d+")]
        private static partial Regex NumberRegex();
    }
}
