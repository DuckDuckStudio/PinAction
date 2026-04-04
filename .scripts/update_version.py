"""
更新 PinAction/Program.cs 和 Installer.iss 中的版本号。
"""

import os.path
import sys


def update_ver(root: str, path: str, old: str, new: str, max_matches: int = 1):
    """
    更新文件中的版本号

    Args:
        root (str): 相对路径 path 基于的路径
        path (str): 文件基于 root 的相对路径
        old (str): 旧内容
        new (str): 新内容
        max_matches (int): 最大匹配行数。默认为 1
    """

    if max_matches < 1:
        raise ValueError("最大匹配次数 max_matches 不应小于 1")

    path = os.path.normpath(os.path.join(root, path))

    matches: int = 0
    with open(path, "r", encoding="utf-8") as f:
        lines: list[str] = f.readlines()

    for index, line in enumerate(lines):
        if old in line.rstrip("\n"):
            matches += 1
            new_line: str = line.replace(old, new, 1)
            # pylint: disable=C0301:line-too-long
            print(f"({matches}/{max_matches}) 匹配 {os.path.relpath(path, root)} 第 {index+1} 行: \"{lines[index].rstrip("\n")}\" -> \"{new_line.rstrip("\n")}\"")
            lines[index] = new_line

        if matches == max_matches:
            break

    with open(path, "w", encoding="utf-8") as f:
        f.writelines(lines)


def main(args: list[str]) -> int:
    """
    入口函数

    Args:
        args (list[str]): 命令行参数

    Returns:
        int: 退出代码
    """

    if len(args) != 1:
        print("使用方法: update_version.py <version>")
        return 1

    version: str = args[0].strip()
    if not version:
        raise ValueError("版本号不能为空")
    print(f"版本号: {version}")

    # os.path.dirname(__file__) -> .scripts/(update_version.py)
    # os.path.dirname(os.path.dirname(__file__)) -> ./[.scripts/(update_version.py)]
    root: str = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

    # 更新版本号
    update_ver(
        root,
        "installer.iss",
        "AppVersion=develop",
        f"AppVersion={version}"
    )
    update_ver(
        root,
        "PinAction/Program.cs",
        # pylint: disable=C0301:line-too-long
        # 不是 f-string 的不用转义 {}
        'AnsiConsole.MarkupLine($"PinAction {Strings.Version} [green]develop[/] by [link=https://duckduckstudio.github.io/yazicbs.github.io/]鸭鸭「カモ」[/]");',
        f'AnsiConsole.MarkupLine($"PinAction {{Strings.Version}} [green]{version}[/] by [link=https://duckduckstudio.github.io/yazicbs.github.io/]鸭鸭「カモ」[/]");',
    )

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
