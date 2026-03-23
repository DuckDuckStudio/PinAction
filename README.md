# Pin Action

> <p style="text-align: center"><a href="README.zh-CN.md">简体中文</a></p>

Pin workflow dependency versions to full-length hashes.  

![Example use.](example.gif)

## How to Use

You can install it via winget:  

```bash
winget install --id DuckStudio.PinAction -s winget -e
```

Then run:

```bash
pinaction <file or directory>
```

You can pass multiple files or directories at once.  
For directories, it will recursively look for `.yaml` or `.yml` files within.

## Q & A
### Does it support using a GitHub Token?

I think it will when I learned how to read and store the Token in C#.  
Currently it doesn't, but you can hardcode it in the source code.

### Can it skip some workflows?

Please modify the code, there are an example in the code.

### Why we need pin the version to the full-length hash?

This is [a practice recommended by GitHub](https://docs.github.com/en/actions/reference/security/secure-use#using-third-party-actions), and is [considered mandatory](https://github.blog/changelog/2025-08-15-github-actions-policy-now-supports-blocking-and-sha-pinning-actions/) in some projects.  
If your workflow dependency do not have [Immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases) enabled, your workflow may be affected if an upstream dependency modifies the same version again.  
Pinning the version to the full-length hash ensures your workflow always uses the same code, even if the upstream dependency modifies the same version.

### What is the "full-length hash"?

It is the Git commit hash corresponding to the specified workflow version (tag).

### How does this program replace the content?

I took the easy route — instead of parsing YAML, I simply split lines containing `uses:` and applied regex after a few `.Split()` operations.  
For details, see the `PinActionHash` method in the source code.

### Why doesn't it have an icon?

Because I can't draw. After an hour of thinking, [我已急哭](https://baike.baidu.com/item/你已急哭).

## License

This program is licensed under the [MIT License](https://github.com/DuckDuckStudio/PinAction/blob/main/LICENSE.txt).

### Dependency

This program would not have been possible without these projects.  
Thank you to the open-source community!

| Package | License |
|-----|-----|
| [Octokit](https://www.nuget.org/packages/Octokit/) | MIT License |
| [DuckStudio.CatFood](https://www.nuget.org/packages/DuckStudio.CatFood) | Apache License 2.0 |
| [Spectre.Console](https://www.nuget.org/packages/Spectre.Console/) | MIT License |

For the license files related to these dependencies, please see [NOTICE.md](https://github.com/DuckDuckStudio/PinAction/blob/main/NOTICE.md).
