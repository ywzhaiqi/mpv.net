# 视频 URL 摘要侧栏 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为指定域名的视频 URL 自动加载同目录 `summary/*.md` 摘要，在主窗口右侧显示基础 Markdown，并支持点击时间文本执行绝对 seek。

**Architecture:** 先在 `MpvNet` 核心项目中补齐可测试的纯逻辑层，包括 `summary-host` 设置读取、摘要 URL 推导、不存在响应识别、Markdown 时间片段化与摘要加载编排；再在 `MpvNet.Windows` 的 `MainForm` 中集成右侧侧栏 UI，把视图模型渲染成 WinForms 控件并接入 seek。UI 必须依赖 core 服务，避免把网络、解析、并发控制全部塞进窗体。

**Tech Stack:** .NET 10, C#, WinForms, WPF 配置编辑器资源、xUnit 测试工程、`HttpClient`

---

## File Map

**Create:**
- `MpvNet.Tests/MpvNet.Tests.csproj`
- `MpvNet.Tests/Summary/SummaryHostOptionsTests.cs`
- `MpvNet.Tests/Summary/VideoSummaryPathResolverTests.cs`
- `MpvNet.Tests/Summary/SummaryMarkdownParserTests.cs`
- `MpvNet.Tests/Summary/VideoSummaryControllerTests.cs`
- `MpvNet/Summary/SummaryHostOptions.cs`
- `MpvNet/Summary/VideoSummaryPathResolver.cs`
- `MpvNet/Summary/SummaryMarkdownParser.cs`
- `MpvNet/Summary/VideoSummaryModels.cs`
- `MpvNet/Summary/VideoSummaryClient.cs`
- `MpvNet/Summary/VideoSummaryController.cs`

**Modify:**
- `MpvNet.sln`
- `MpvNet/App.cs`
- `MpvNet.Windows/Resources/editor_conf.txt`
- `MpvNet.Windows/WinForms/MainForm.Designer.cs`
- `MpvNet.Windows/WinForms/MainForm.cs`

**Test Targets:**
- `dotnet test MpvNet.Tests/MpvNet.Tests.csproj`
- `dotnet build MpvNet.Windows/MpvNet.Windows.csproj`

### Task 1: 建立测试工程和基础骨架

**Files:**
- Create: `MpvNet.Tests/MpvNet.Tests.csproj`
- Modify: `MpvNet.sln`
- Create: `MpvNet.Tests/Summary/SummaryHostOptionsTests.cs`

- [x] **Step 1: 写失败测试，先定义 `summary-host` 的行为边界**

```csharp
using Xunit;
using MpvNet.Summary;

namespace MpvNet.Tests.Summary;

public class SummaryHostOptionsTests
{
    [Theory]
    [InlineData("", "https://pan2.871015.xyz/d/a.mp4", false)]
    [InlineData("pan2.871015.xyz", "https://pan2.871015.xyz/d/a.mp4", true)]
    [InlineData("pan2.871015.xyz", "https://other.example/d/a.mp4", false)]
    [InlineData("pan2.871015.xyz", "not-a-url", false)]
    public void MatchesSummaryHost_OnlyMatchesExactHost(string configuredHost, string path, bool expected)
    {
        var result = SummaryHostOptions.Matches(configuredHost, path);
        Assert.Equal(expected, result);
    }
}
```

- [x] **Step 2: 先创建测试工程并跑它，确认当前因缺少实现而失败**

Run:

```powershell
dotnet new xunit -n MpvNet.Tests
dotnet sln MpvNet.sln add MpvNet.Tests/MpvNet.Tests.csproj
dotnet add MpvNet.Tests/MpvNet.Tests.csproj reference MpvNet/MpvNet.csproj
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter MatchesSummaryHost_OnlyMatchesExactHost
```

Expected: `FAIL`，错误指向 `MpvNet.Summary` 或 `SummaryHostOptions` 未定义，而不是测试工程配置错误。

- [x] **Step 3: 写最小实现，让测试可编译**

```csharp
namespace MpvNet.Summary;

public static class SummaryHostOptions
{
    public static bool Matches(string configuredHost, string? path)
    {
        if (string.IsNullOrWhiteSpace(configuredHost) || string.IsNullOrWhiteSpace(path))
            return false;

        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Host, configuredHost, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [x] **Step 4: 再跑测试，确认变绿**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter MatchesSummaryHost_OnlyMatchesExactHost
```

Expected: `PASS`

- [x] **Step 5: 提交骨架**

```bash
git add MpvNet.sln MpvNet.Tests/MpvNet.Tests.csproj MpvNet.Tests/Summary/SummaryHostOptionsTests.cs MpvNet/Summary/SummaryHostOptions.cs
git commit -m "test: add summary host option test scaffold"
```

### Task 2: 落地 `summary-host` 设置和 URL 过滤

**Files:**
- Modify: `MpvNet/App.cs`
- Modify: `MpvNet.Windows/Resources/editor_conf.txt`
- Create: `MpvNet/Summary/SummaryHostOptions.cs`
- Test: `MpvNet.Tests/Summary/SummaryHostOptionsTests.cs`

- [x] **Step 1: 扩充失败测试，覆盖默认值、空值禁用和 App 配置读取**

```csharp
[Fact]
public void Normalize_UsesDefaultHostWhenConfigMissing()
{
    Assert.Equal("pan2.871015.xyz", SummaryHostOptions.Normalize(null, useDefaultWhenEmpty: true));
}

[Fact]
public void Normalize_AllowsExplicitEmptyToDisableFeature()
{
    Assert.Equal("", SummaryHostOptions.Normalize("", useDefaultWhenEmpty: false));
}
```

- [x] **Step 2: 运行测试，确认是因为新 API 未实现而失败**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter SummaryHostOptionsTests
```

Expected: `FAIL`，提示 `Normalize` 不存在。

- [x] **Step 3: 写最小实现，并把设置接进 `AppClass` 与配置编辑器**

```csharp
// MpvNet/App.cs
public string SummaryHost { get; set; } = "pan2.871015.xyz";

public bool ProcessProperty(string name, string value, bool writeError = false)
{
    switch (name)
    {
        case "summary-host": SummaryHost = value.Trim('\'', '"'); return true;
    }
}
```

```text
name = summary-host
file = mpvnet
directory = Playback
width = 300
help = Limit automatic summary loading to a single exact host. Empty disables the feature. Default: pan2.871015.xyz (mpv.net option)
default = pan2.871015.xyz
```

```csharp
// MpvNet/Summary/SummaryHostOptions.cs
public static string Normalize(string? configuredHost, bool useDefaultWhenEmpty)
{
    var value = (configuredHost ?? "").Trim();

    if (value.Length == 0)
        return useDefaultWhenEmpty ? "pan2.871015.xyz" : "";

    return value;
}
```

- [x] **Step 4: 运行测试确认通过**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter SummaryHostOptionsTests
```

Expected: `PASS`

- [x] **Step 5: 提交设置层**

```bash
git add MpvNet/App.cs MpvNet.Windows/Resources/editor_conf.txt MpvNet/Summary/SummaryHostOptions.cs MpvNet.Tests/Summary/SummaryHostOptionsTests.cs
git commit -m "feat: add summary host setting"
```

### Task 3: 实现摘要 URL 推导和"不存在"响应识别

**Files:**
- Create: `MpvNet/Summary/VideoSummaryPathResolver.cs`
- Create: `MpvNet/Summary/VideoSummaryModels.cs`
- Create: `MpvNet/Summary/VideoSummaryClient.cs`
- Test: `MpvNet.Tests/Summary/VideoSummaryPathResolverTests.cs`

- [x] **Step 1: 写失败测试，锁定 URL 推导、不存在 JSON 识别和视频扩展名过滤**

```csharp
using Xunit;
using MpvNet.Summary;

namespace MpvNet.Tests.Summary;

public class VideoSummaryPathResolverTests
{
    [Fact]
    public void BuildCandidates_BuildsMdAndBdUrlsForVideoUrl()
    {
        var candidates = VideoSummaryPathResolver.BuildCandidates(
            "https://pan2.871015.xyz/d/a/b/c.mp4",
            "pan2.871015.xyz");

        Assert.Collection(candidates,
            item => Assert.Equal("https://pan2.871015.xyz/d/a/b/summary/c.md", item.Url),
            item => Assert.Equal("https://pan2.871015.xyz/d/a/b/summary/c_bd.md", item.Url));
    }

    [Fact]
    public void BuildCandidates_ReturnsEmptyForHostMismatch()
    {
        Assert.Empty(VideoSummaryPathResolver.BuildCandidates(
            "https://other.example/d/a/b/c.mp4",
            "pan2.871015.xyz"));
    }

    [Fact]
    public void IsMissingSummaryPayload_ReturnsTrueForObjectNotFoundJson()
    {
        const string payload = "{\"code\":500,\"message\":\"failed link: failed to get file: object not found\",\"data\":null}";
        Assert.True(VideoSummaryPathResolver.IsMissingSummaryPayload(payload, "application/json"));
    }
}
```

- [x] **Step 2: 运行测试，确认失败原因正确**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter VideoSummaryPathResolverTests
```

Expected: `FAIL`，提示 `VideoSummaryPathResolver` 及相关模型不存在。

- [x] **Step 3: 写最小实现**

```csharp
namespace MpvNet.Summary;

public sealed record SummaryCandidate(string Kind, string Url);

public static class VideoSummaryPathResolver
{
    static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".ts"
    };

    public static IReadOnlyList<SummaryCandidate> BuildCandidates(string? mediaPath, string configuredHost)
    {
        if (!SummaryHostOptions.Matches(configuredHost, mediaPath))
            return [];

        var uri = new Uri(mediaPath!, UriKind.Absolute);
        var extension = Path.GetExtension(uri.AbsolutePath);

        if (!VideoExtensions.Contains(extension))
            return [];

        var fileName = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        var directory = uri.AbsolutePath[..uri.AbsolutePath.LastIndexOf('/')];

        return
        [
            new("summary", $"{uri.Scheme}://{uri.Authority}{directory}/summary/{fileName}.md"),
            new("summary-bd", $"{uri.Scheme}://{uri.Authority}{directory}/summary/{fileName}_bd.md")
        ];
    }

    public static bool IsMissingSummaryPayload(string? body, string? mediaType) =>
        !string.IsNullOrWhiteSpace(body) &&
        (mediaType ?? "").Contains("json", StringComparison.OrdinalIgnoreCase) &&
        body.Contains("\"code\":500", StringComparison.OrdinalIgnoreCase) &&
        body.Contains("object not found", StringComparison.OrdinalIgnoreCase);
}
```

- [x] **Step 4: 再跑测试**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter VideoSummaryPathResolverTests
```

Expected: `PASS`

- [x] **Step 5: 提交摘要定位层**

```bash
git add MpvNet/Summary/VideoSummaryPathResolver.cs MpvNet/Summary/VideoSummaryModels.cs MpvNet/Summary/VideoSummaryClient.cs MpvNet.Tests/Summary/VideoSummaryPathResolverTests.cs
git commit -m "feat: add summary url resolution"
```

### Task 4: 实现基础 Markdown 片段化和时间文本识别

**Files:**
- Create: `MpvNet/Summary/SummaryMarkdownParser.cs`
- Create: `MpvNet/Summary/VideoSummaryModels.cs`
- Test: `MpvNet.Tests/Summary/SummaryMarkdownParserTests.cs`

- [x] **Step 1: 写失败测试，锁定标题、列表、代码块和时间链接片段**

```csharp
using Xunit;
using MpvNet.Summary;

namespace MpvNet.Tests.Summary;

public class SummaryMarkdownParserTests
{
    [Fact]
    public void Parse_ConvertsTimestampsToSeekSegments()
    {
        var blocks = SummaryMarkdownParser.Parse("Intro 00:10\n\n## Part\nAt 01:23:45 jump.");
        var paragraph = Assert.IsType<SummaryParagraphBlock>(blocks[0]);

        Assert.Contains(paragraph.Inlines, it => it is SummaryTimestampInline ts && ts.Seconds == 10);
        Assert.IsType<SummaryHeadingBlock>(blocks[1]);
    }

    [Fact]
    public void Parse_PreservesCodeBlocksWithoutTimestampLinks()
    {
        var blocks = SummaryMarkdownParser.Parse("```\nseek 00:10\n```");
        var code = Assert.IsType<SummaryCodeBlock>(Assert.Single(blocks));
        Assert.Equal("seek 00:10", code.Text.Trim());
    }
}
```

- [x] **Step 2: 跑测试，看它按预期失败**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter SummaryMarkdownParserTests
```

Expected: `FAIL`，提示解析器和 block/inline 类型不存在。

- [x] **Step 3: 写最小实现，先满足 spec 要求的基础 Markdown**

```csharp
public abstract record SummaryBlock;
public sealed record SummaryHeadingBlock(int Level, string Text) : SummaryBlock;
public sealed record SummaryParagraphBlock(IReadOnlyList<SummaryInline> Inlines) : SummaryBlock;
public sealed record SummaryListBlock(IReadOnlyList<IReadOnlyList<SummaryInline>> Items) : SummaryBlock;
public sealed record SummaryCodeBlock(string Text) : SummaryBlock;

public abstract record SummaryInline;
public sealed record SummaryTextInline(string Text) : SummaryInline;
public sealed record SummaryLinkInline(string Text, string Url) : SummaryInline;
public sealed record SummaryTimestampInline(string Text, int Seconds) : SummaryInline;
```

```csharp
public static class SummaryMarkdownParser
{
    static readonly Regex TimestampRegex = new(@"(?<!\d)(?:\d{1,2}:)?\d{2}:\d{2}(?!\d)", RegexOptions.Compiled);

    public static IReadOnlyList<SummaryBlock> Parse(string markdown)
    {
        // 最小实现：按空行分段，识别 # 标题、- 列表、``` 代码块，其余作为段落。
    }

    public static int ParseTimestampSeconds(string value)
    {
        var parts = value.Split(':').Select(int.Parse).ToArray();
        return parts.Length == 2 ? parts[0] * 60 + parts[1] : parts[0] * 3600 + parts[1] * 60 + parts[2];
    }
}
```

- [x] **Step 4: 跑测试确认变绿**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter SummaryMarkdownParserTests
```

Expected: `PASS`

- [x] **Step 5: 提交解析层**

```bash
git add MpvNet/Summary/SummaryMarkdownParser.cs MpvNet/Summary/VideoSummaryModels.cs MpvNet.Tests/Summary/SummaryMarkdownParserTests.cs
git commit -m "feat: add summary markdown parser"
```

### Task 5: 实现摘要加载编排、并发取消与视图模型

**Files:**
- Create: `MpvNet/Summary/VideoSummaryClient.cs`
- Create: `MpvNet/Summary/VideoSummaryController.cs`
- Create: `MpvNet/Summary/VideoSummaryModels.cs`
- Test: `MpvNet.Tests/Summary/VideoSummaryControllerTests.cs`

- [x] **Step 1: 写失败测试，锁定"两份摘要都存在""不存在时隐藏""旧请求不能回填"的编排行为**

```csharp
using Xunit;
using MpvNet.Summary;

namespace MpvNet.Tests.Summary;

public class VideoSummaryControllerTests
{
    [Fact]
    public async Task LoadAsync_ReturnsTwoSectionsWhenBothSummariesExist()
    {
        var client = new FakeSummaryClient(
            ("https://pan2.871015.xyz/d/a/b/summary/c.md", "# A"),
            ("https://pan2.871015.xyz/d/a/b/summary/c_bd.md", "# B"));

        var controller = new VideoSummaryController(client);
        var result = await controller.LoadAsync("https://pan2.871015.xyz/d/a/b/c.mp4", "pan2.871015.xyz", CancellationToken.None);

        Assert.True(result.ShouldShowSidebar);
        Assert.Equal(2, result.Sections.Count);
    }

    [Fact]
    public async Task LoadAsync_ReturnsHiddenWhenAllCandidatesMissing()
    {
        var client = new FakeSummaryClient();
        var controller = new VideoSummaryController(client);
        var result = await controller.LoadAsync("https://pan2.871015.xyz/d/a/b/c.mp4", "pan2.871015.xyz", CancellationToken.None);

        Assert.False(result.ShouldShowSidebar);
        Assert.Empty(result.Sections);
    }
}
```

- [x] **Step 2: 跑测试确认失败**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter VideoSummaryControllerTests
```

Expected: `FAIL`

- [x] **Step 3: 写最小实现**

```csharp
public interface IVideoSummaryClient
{
    Task<SummaryFetchResult> FetchAsync(SummaryCandidate candidate, CancellationToken cancellationToken);
}

public sealed class VideoSummaryController
{
    readonly IVideoSummaryClient _client;

    public VideoSummaryController(IVideoSummaryClient client) => _client = client;

    public async Task<VideoSummaryViewModel> LoadAsync(string mediaPath, string summaryHost, CancellationToken cancellationToken)
    {
        var candidates = VideoSummaryPathResolver.BuildCandidates(mediaPath, summaryHost);
        var sections = new List<VideoSummarySectionViewModel>();

        foreach (var candidate in candidates)
        {
            var fetch = await _client.FetchAsync(candidate, cancellationToken);

            if (!fetch.IsSuccess || string.IsNullOrWhiteSpace(fetch.Markdown))
                continue;

            sections.Add(new VideoSummarySectionViewModel(candidate.Kind, candidate.Url, SummaryMarkdownParser.Parse(fetch.Markdown)));
        }

        return new VideoSummaryViewModel(mediaPath, sections);
    }
}
```

- [x] **Step 4: 再跑测试**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter VideoSummaryControllerTests
```

Expected: `PASS`

- [x] **Step 5: 提交编排层**

```bash
git add MpvNet/Summary/VideoSummaryClient.cs MpvNet/Summary/VideoSummaryController.cs MpvNet/Summary/VideoSummaryModels.cs MpvNet.Tests/Summary/VideoSummaryControllerTests.cs
git commit -m "feat: add summary loading controller"
```

### Task 6: 在 `MainForm` 集成右侧侧栏和 seek

**Files:**
- Modify: `MpvNet.Windows/WinForms/MainForm.Designer.cs`
- Modify: `MpvNet.Windows/WinForms/MainForm.cs`
- Modify: `MpvNet/App.cs`
- Modify: `MpvNet/Summary/VideoSummaryController.cs`

- [x] **Step 1: 先写最小失败测试或验证钩子，锁定 seek 命令格式**

如果当前代码库不方便直接做 WinForms 自动化测试，就先在 core 层补一个可测方法：

```csharp
[Theory]
[InlineData(10, "seek 10 absolute exact")]
[InlineData(5025, "seek 5025 absolute exact")]
public void BuildSeekCommand_UsesAbsoluteExact(int seconds, string expected)
{
    Assert.Equal(expected, VideoSummaryViewModel.BuildSeekCommand(seconds));
}
```

- [x] **Step 2: 跑测试，确认红灯**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj --filter BuildSeekCommand_UsesAbsoluteExact
```

Expected: `FAIL`

- [x] **Step 3: 写最小实现并接进主窗体**

主窗体布局目标代码：

```csharp
// MainForm.Designer.cs
SummarySidebar = new Panel();
SummaryScrollPanel = new FlowLayoutPanel();

SummarySidebar.Dock = DockStyle.Right;
SummarySidebar.Width = 360;
SummarySidebar.Visible = false;
SummarySidebar.Padding = new Padding(12);
SummarySidebar.BackColor = Color.FromArgb(24, 24, 24);

SummaryScrollPanel.Dock = DockStyle.Fill;
SummaryScrollPanel.FlowDirection = FlowDirection.TopDown;
SummaryScrollPanel.WrapContents = false;
SummaryScrollPanel.AutoScroll = true;

SummarySidebar.Controls.Add(SummaryScrollPanel);
Controls.Add(SummarySidebar);
```

```csharp
// MainForm.cs
CancellationTokenSource? _summaryCts;
readonly HttpClient _summaryHttpClient = new();
readonly VideoSummaryController _summaryController = new(new VideoSummaryClient());

async void Player_FileLoaded()
{
    await ReloadSummarySidebarAsync();
}

async Task ReloadSummarySidebarAsync()
{
    _summaryCts?.Cancel();
    _summaryCts = new CancellationTokenSource();

    string path = Player.GetPropertyString("path");
    string summaryHost = App.SummaryHost;

    var viewModel = await _summaryController.LoadAsync(path, summaryHost, _summaryCts.Token);

    if (_summaryCts.IsCancellationRequested || viewModel.MediaPath != Player.GetPropertyString("path"))
        return;

    RenderSummarySidebar(viewModel);
}

void SeekToTimestamp(int seconds) => Player.Command($"seek {seconds} absolute exact");
```

- [x] **Step 4: 编译和测试验证**

Run:

```powershell
dotnet test MpvNet.Tests/MpvNet.Tests.csproj
dotnet build MpvNet.Windows/MpvNet.Windows.csproj
```

Expected:
- `dotnet test` 全绿
- `dotnet build` 成功，无新编译错误

- [x] **Step 5: 手动验证**

1. 在配置编辑器里确认 `summary-host` 默认是 `pan2.871015.xyz`
2. 播放 `https://pan2.871015.xyz/d/a/b/c.mp4`
3. 当 `summary/c.md` 和 `summary/c_bd.md` 都存在时，右侧栏出现并上下显示两块内容
4. 点击 `00:10` 后播放器跳到 10 秒
5. 把 `summary-host` 清空后重试，同一 URL 不再请求摘要，右侧栏隐藏
6. 切到另一个视频，确认旧摘要不会残留

- [x] **Step 6: 提交 UI 集成**

```bash
git add MpvNet.Windows/WinForms/MainForm.Designer.cs MpvNet.Windows/WinForms/MainForm.cs MpvNet/App.cs MpvNet/Summary/VideoSummaryController.cs MpvNet.Tests
git commit -m "feat: add video url summary sidebar"
```

## Self-Review

- Spec coverage:
  - `summary-host` 默认值、空值禁用、host 精确匹配：Task 2
  - `summary/*.md` 与 `*_bd.md` URL 推导：Task 3
  - `object not found` JSON 视为缺失：Task 3
  - 基础 Markdown 和时间文本点击：Task 4、Task 6
  - 两份摘要同时显示、都不存在时隐藏：Task 5、Task 6
  - 切换媒体时取消旧请求：Task 5、Task 6
- Placeholder scan:
  - 没有 `TBD`、`TODO` 或 "稍后实现"。
  - 手动验证步骤已写成明确操作，不依赖额外解释。
- Type consistency:
  - `SummaryHostOptions`、`VideoSummaryPathResolver`、`SummaryMarkdownParser`、`VideoSummaryController` 在各任务中的命名一致。

Plan complete and saved to `docs/superpowers/plans/2026-04-16-video-url-summary-sidebar.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
