using Moq;
using Octokit;

namespace PinAction.Tests
{
    /// <summary>
    /// 定义测试集合，禁用并行化以避免对共享资源的冲突。
    /// </summary>
    [CollectionDefinition(nameof(ProgramTestsCollection), DisableParallelization = true)]
    public sealed class ProgramTestsCollection;

    /// <summary>
    /// 单元测试类。
    /// </summary>
    [Collection(nameof(ProgramTestsCollection))]
    public sealed class UnitTests : IDisposable
    {
        /// <summary>
        /// 用于测试的固定提交 SHA 值。
        /// </summary>
        private const string CommitSha = "0123456789abcdef0123456789abcdef01234567";

        /// <summary>
        /// 用于测试的临时目录路径。
        /// </summary>
        private readonly string _temporaryDirectory = Directory.CreateTempSubdirectory().FullName;

        /// <summary>
        /// 原始的 GitHub 客户端实例，用于在测试完成后恢复。
        /// </summary>
        private readonly IGitHubClient _originalGitHubClient = Program.GitHubClient;

        /// <summary>
        /// 用于测试的 GitHub 客户端模拟实例。
        /// </summary>
        private readonly Mock<IGitHubClient> _githubClient = new(MockBehavior.Strict);

        /// <summary>
        /// 用于测试的 Git 数据库客户端模拟实例。
        /// </summary>
        private readonly Mock<IGitDatabaseClient> _gitDatabase = new(MockBehavior.Strict);

        /// <summary>
        /// 用于测试的 Git 标签客户端模拟实例。
        /// </summary>
        private readonly Mock<ITagsClient> _tags = new(MockBehavior.Strict);

        /// <summary>
        /// 用于测试的引用客户端模拟实例。
        /// </summary>
        private readonly Mock<IReferencesClient> _references = new(MockBehavior.Strict);

        /// <summary>
        /// 用于测试的仓库客户端模拟实例。
        /// </summary>
        private readonly Mock<IRepositoriesClient> _repositories = new(MockBehavior.Strict);

        /// <summary>
        /// 初始化单元测试类的构造函数。
        /// </summary>
        public UnitTests()
        {
            // 设置 GitHub 客户端的模拟行为
            _gitDatabase.Setup(client => client.Reference).Returns(_references.Object);
            _gitDatabase.Setup(client => client.Tag).Returns(_tags.Object);
            _githubClient.Setup(client => client.Git).Returns(_gitDatabase.Object);
            _githubClient.Setup(client => client.Repository).Returns(_repositories.Object);

            Program.PinedActions.Clear();
            Program.GitHubClient = _githubClient.Object;
        }

        [Fact(DisplayName = "CompareVersionStrings 方法应按自然顺序比较版本字符串")]
        public void CompareVersionStrings_OrdersNumericSegmentsNaturally()
        {
            Assert.True(Program.CompareVersionStrings("v1.9.0", "v1.10.0") < 0);
            Assert.True(Program.CompareVersionStrings("v2.0", "v1.99") > 0);
            Assert.Equal(0, Program.CompareVersionStrings("release-1.2", "v1.2.0"));
        }

        [Fact(DisplayName = "ResolveRefinedTag 方法应返回与提交匹配的最高版本标签")]
        public void ResolveRefinedTag_ReturnsHighestVersionForMatchingCommit()
        {
            _repositories
                .Setup(client => client.GetAllTags("owner", "repo"))
                .Returns(Task.FromResult<IReadOnlyList<RepositoryTag>>(
                [
                    CreateRepositoryTag("v1.9.0", CommitSha),
                    CreateRepositoryTag("v1.10.0", CommitSha),
                    CreateRepositoryTag("v99.0.0", "ffffffffffffffffffffffffffffffffffffffff")
                ]));

            string result = Program.ResolveRefinedTag("owner/repo", "main", CommitSha);

            Assert.Equal("v1.10.0", result);
            _repositories.Verify(client => client.GetAllTags("owner", "repo"), Times.Once);
        }

        [Fact(DisplayName = "ResolveRefinedTag 方法应在提交没有匹配标签时返回原始标签")]
        public void ResolveRefinedTag_ReturnsOriginalTagWhenCommitHasNoMatchingTag()
        {
            _repositories
                .Setup(client => client.GetAllTags("owner", "repo"))
                .Returns(Task.FromResult<IReadOnlyList<RepositoryTag>>(
                [
                    CreateRepositoryTag("v1.0.0", "ffffffffffffffffffffffffffffffffffffffff")
                ]));

            string result = Program.ResolveRefinedTag("owner/repo", "main", CommitSha);

            Assert.Equal("main", result);
        }

        [Fact(DisplayName = "PinActionHash 方法应正确固定引用并保留内联注释")]
        public void PinActionHash_PinsReferencesAndPreservesInlineComments()
        {
            string path = Path.Combine(_temporaryDirectory, "workflow.yml");
            File.WriteAllLines(path,
            [
                "name: test",
                "      uses: owner/repo@v1     # keep this comment",
                "      uses: owner/repo@v1",
                "      run: echo uses: not-an-action"
            ]);

            _references
                .Setup(client => client.Get("owner", "repo", "tags/v1"))
                .Returns(Task.FromResult(CreateReference(CommitSha)));
            _repositories
                .Setup(client => client.GetAllTags("owner", "repo"))
                .Returns(Task.FromResult<IReadOnlyList<RepositoryTag>>(
                [
                    CreateRepositoryTag("v1.2.0", CommitSha)
                ]));

            bool succeeded = Program.PinActionHash(path);

            Assert.True(succeeded);
            string[] expectedLines =
            [
                "name: test",
                $"      uses: owner/repo@{CommitSha}     # v1.2.0 keep this comment", // # 前面的空格数应保持原样
                $"      uses: owner/repo@{CommitSha}  # v1.2.0", // 默认两个空格
                "      run: echo uses: not-an-action"
            ];
            Assert.Equal(expectedLines, File.ReadAllLines(path));

            // 哈希有缓存，只获取一次引用。
            _references.Verify(client => client.Get("owner", "repo", "tags/v1"), Times.Once);

            // 成功通过引用获取了哈希，不应再尝试通过 Tag 获取哈希。
            _tags.Verify(client => client.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // 虽然哈希有缓存，但解析出来的精确 Tag 没有缓存。
            _repositories.Verify(client => client.GetAllTags("owner", "repo"), Times.Exactly(2));
        }

        [Fact(DisplayName = "PinActionHash 方法应将附注标签对象解析为其目标提交哈希")]
        public void PinActionHash_ResolvesAnnotatedTagToCommitHash()
        {
            const string tagObjectSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            string path = Path.Combine(_temporaryDirectory, "workflow.yml");
            File.WriteAllText(path, "      uses: owner/repo@v1");

            _references
                .Setup(client => client.Get("owner", "repo", "tags/v1"))
                .Returns(Task.FromResult(CreateReference(tagObjectSha, TaggedType.Tag)));
            _tags
                .Setup(client => client.Get("owner", "repo", tagObjectSha))
                .Returns(Task.FromResult(CreateTagObject(CommitSha, TaggedType.Commit)));
            _repositories
                .Setup(client => client.GetAllTags("owner", "repo"))
                .Returns(Task.FromResult<IReadOnlyList<RepositoryTag>>([]));

            bool succeeded = Program.PinActionHash(path);

            Assert.True(succeeded);
            Assert.Equal($"      uses: owner/repo@{CommitSha}  # v1", File.ReadAllText(path).TrimEnd('\r', '\n'));
            _tags.Verify(client => client.Get("owner", "repo", tagObjectSha), Times.Once);
        }

        [Fact(DisplayName = "PinActionHash 方法应跳过最终目标不是提交的附注标签")]
        public void PinActionHash_SkipsAnnotatedTagWhenResolvedTargetIsNotCommit()
        {
            const string tagObjectSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string treeSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            string path = Path.Combine(_temporaryDirectory, "workflow.yml");
            const string originalLine = "      uses: owner/repo@v1";
            File.WriteAllText(path, originalLine);

            _references
                .Setup(client => client.Get("owner", "repo", "tags/v1"))
                .Returns(Task.FromResult(CreateReference(tagObjectSha, TaggedType.Tag)));
            _tags
                .Setup(client => client.Get("owner", "repo", tagObjectSha))
                .Returns(Task.FromResult(CreateTagObject(treeSha, TaggedType.Tree)));

            bool result = Program.PinActionHash(path);

            Assert.Equal(originalLine, File.ReadAllText(path).TrimEnd('\r', '\n'));
            Assert.False(Program.PinedActions.ContainsKey("owner/repo@v1"));
            Assert.False(result);
            _tags.Verify(client => client.Get("owner", "repo", tagObjectSha), Times.Once);
            _repositories.Verify(client => client.GetAllTags(
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }

        [Fact(DisplayName = "PinActionHash 方法应跳过已固定的和无效的仓库引用")]
        public void PinActionHash_SkipsAlreadyPinnedAndInvalidRepositoryReferences()
        {
            string path = Path.Combine(_temporaryDirectory, "workflow.yml");
            string[] originalLines =
            [
                $"      uses: owner/repo@{CommitSha}",
                "      uses: local-action@v1",
                "      uses: owner/repo/path@v1"
            ];
            File.WriteAllLines(path, originalLines);

            bool succeeded = Program.PinActionHash(path);

            Assert.True(succeeded);
            Assert.Equal(originalLines, File.ReadAllLines(path));
            _references.Verify(client => client.Get(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
            _repositories.Verify(client => client.GetAllTags(
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// 还原单元测试使用的资源。
        /// </summary>
        public void Dispose()
        {
            Program.PinedActions.Clear();
            Program.GitHubClient = _originalGitHubClient;
            Directory.Delete(_temporaryDirectory, recursive: true);
        }

        /// <summary>
        /// 创建一个引用实例。
        /// </summary>
        /// <param name="sha">提交的 SHA。</param>
        /// <param name="type">目标对象的类型。</param>
        /// <returns>引用实例。</returns>
        private static Reference CreateReference(string sha, TaggedType type = TaggedType.Commit)
        {
            TagObject target = new("", "", "", "", sha, null, null, type);
            return new Reference("refs/tags/v1", "", "", target);
        }

        /// <summary>
        /// 创建一个附注标签对象实例。
        /// </summary>
        /// <param name="sha">目标对象的 SHA。</param>
        /// <param name="type">目标对象的类型。</param>
        /// <returns>附注标签对象实例。</returns>
        private static GitTag CreateTagObject(string sha, TaggedType type)
        {
            TagObject target = new("", "", "", "", sha, null, null, type);
            return new GitTag("", "", "", "", "", null, null, "", "", null, target, null);
        }

        /// <summary>
        /// 创建一个仓库标签实例。
        /// </summary>
        /// <param name="name">标签的名称。</param>
        /// <param name="sha">提交的 SHA。</param>
        /// <returns>仓库标签实例。</returns>
        private static RepositoryTag CreateRepositoryTag(string name, string sha)
        {
            GitReference commit = new("", "", "", "", sha, null!, null!);
            return new RepositoryTag(name, "", commit, "", "");
        }
    }
}
