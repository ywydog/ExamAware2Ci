using ExamAware2Ci.Models.Automations.Actions;
using Xunit;

namespace ExamAware2Ci.Tests;

/// <summary>
/// PlayExamActionSettings.Validate() 单元测试
/// 覆盖：
///  - 空字符串
///  - File 不存在
///  - File 扩展名非法
///  - File 合法 (.ea2 / .json)
///  - URL 格式错误
///  - URL 协议非 http/https
///  - URL 合法
/// </summary>
public class PlayExamActionSettingsValidationTests
{
    [Fact]
    public void EmptySource_Fails()
    {
        var s = new PlayExamActionSettings { SourceType = ExamSourceType.File, Source = "" };
        Assert.NotNull(s.Validate());
        Assert.Equal("来源为空", s.Validate());
    }

    [Fact]
    public void WhitespaceSource_Fails()
    {
        var s = new PlayExamActionSettings { SourceType = ExamSourceType.File, Source = "   " };
        Assert.NotNull(s.Validate());
    }

    [Fact]
    public void NonExistingFile_Fails()
    {
        var s = new PlayExamActionSettings
        {
            SourceType = ExamSourceType.File,
            Source = "/path/that/definitely/does/not/exist.ea2"
        };
        var err = s.Validate();
        Assert.NotNull(err);
        Assert.Contains("不存在", err!);
    }

    [Fact]
    public void WrongExtension_Fails()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            // Path.GetTempFileName returns .tmp
            var s = new PlayExamActionSettings { SourceType = ExamSourceType.File, Source = tmp };
            var err = s.Validate();
            Assert.NotNull(err);
            Assert.Contains("不支持的扩展名", err!);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public void ValidEa2File_Passes()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"ea2-test-{Guid.NewGuid():N}.ea2");
        try
        {
            File.WriteAllText(tmp, "{\"examInfos\":[]}");
            var s = new PlayExamActionSettings { SourceType = ExamSourceType.File, Source = tmp };
            Assert.Null(s.Validate());
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public void ValidJsonFile_Passes()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"ea2-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tmp, "{\"examInfos\":[]}");
            var s = new PlayExamActionSettings { SourceType = ExamSourceType.File, Source = tmp };
            Assert.Null(s.Validate());
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    [Fact]
    public void InvalidUrl_Fails()
    {
        var s = new PlayExamActionSettings { SourceType = ExamSourceType.Url, Source = "not a url" };
        Assert.NotNull(s.Validate());
    }

    [Fact]
    public void FtpUrl_Fails()
    {
        var s = new PlayExamActionSettings { SourceType = ExamSourceType.Url, Source = "ftp://example.com/cfg.ea2" };
        var err = s.Validate();
        Assert.NotNull(err);
        Assert.Contains("http", err!);
    }

    [Theory]
    [InlineData("http://example.com/cfg.ea2")]
    [InlineData("https://example.com/cfg.ea2")]
    [InlineData("HTTPS://EXAMPLE.COM/CFG.EA2")]
    public void ValidHttpUrl_Passes(string url)
    {
        var s = new PlayExamActionSettings { SourceType = ExamSourceType.Url, Source = url };
        Assert.Null(s.Validate());
    }

    /// <summary>
    /// 显式锁定 <see cref="ExamSourceType"/> 数值。设置会被序列化进配置 JSON，
    /// 一旦数值被改，旧的 Profile 文件会反序列化到错误的模式，所以需要钉死。
    /// </summary>
    [Fact]
    public void ExamSourceType_NumericValues_ArePinned()
    {
        Assert.Equal(0, (int)ExamSourceType.Url);
        Assert.Equal(1, (int)ExamSourceType.File);
    }
}
