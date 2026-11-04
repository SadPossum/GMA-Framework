namespace Gma.Framework.Tests.FileManagement;

using System.Reflection;
using Gma.Framework.FileManagement;
using Gma.Framework.FileManagement.Minio;
using Microsoft.Extensions.Options;
using global::Minio;
using global::Minio.DataModel;
using Xunit;

public sealed class MinioFileStorageMetadataTests
{
    [Theory]
    [InlineData("x-amz-meta-file-name", "x-amz-meta-module")]
    [InlineData("X-Amz-Meta-File-Name", "X-Amz-Meta-Module")]
    public async Task Object_properties_preserve_metadata_after_the_real_sdk_header_parser(
        string fileNameHeader,
        string moduleHeader)
    {
        Dictionary<string, string> headers = new(StringComparer.Ordinal)
        {
            ["Content-Length"] = "5",
            ["Content-Type"] = "text/plain",
            ["Cache-Control"] = "private",
            ["Content-Disposition"] = "attachment",
            [fileNameHeader] = "../folder/report.txt",
            [moduleHeader] = "integration-tests",
            ["X-Amz-Meta-Customer-Tag"] = "fixture-only",
            ["X-Amz-Meta-x-amz-meta-literal"] = "keep-the-user-key"
        };
        ObjectStat stat = ObjectStat.FromResponseHeaders("objects/report", headers);
        IMinioClient client = DispatchProxy.Create<IMinioClient, StatClientProxy>();
        ((StatClientProxy)client).Stat = stat;
        using MinioFileStorage storage = CreateStorage(client);

        FileStorageObjectProperties? properties = await storage.GetPropertiesAsync(new("objects/report"));

        Assert.NotNull(properties);
        Assert.Equal(5, properties.ContentLength);
        Assert.Equal("text/plain", properties.ContentType);
        Assert.Equal("report.txt", properties.FileName);
        Assert.Equal(3, properties.Metadata.Count);
        Assert.Equal("integration-tests", properties.Metadata["module"]);
        Assert.Equal("fixture-only", properties.Metadata["customer-tag"]);
        Assert.Equal("keep-the-user-key", properties.Metadata["x-amz-meta-literal"]);
        Assert.DoesNotContain("file-name", properties.Metadata.Keys);
        Assert.DoesNotContain("content-type", properties.Metadata.Keys);
        Assert.DoesNotContain("cache-control", properties.Metadata.Keys);
        Assert.DoesNotContain("content-disposition", properties.Metadata.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bad\r\nname.txt")]
    public async Task Missing_or_unsafe_filename_does_not_become_an_application_header(string? fileName)
    {
        Dictionary<string, string> headers = new(StringComparer.Ordinal)
        {
            ["Content-Length"] = "0",
            ["Content-Type"] = "application/octet-stream"
        };
        if (fileName is not null)
        {
            headers["X-Amz-Meta-File-Name"] = fileName;
        }
        IMinioClient client = DispatchProxy.Create<IMinioClient, StatClientProxy>();
        ((StatClientProxy)client).Stat = ObjectStat.FromResponseHeaders("objects/missing-name", headers);
        using MinioFileStorage storage = CreateStorage(client);

        FileStorageObjectProperties? properties = await storage.GetPropertiesAsync(new("objects/missing-name"));

        Assert.NotNull(properties);
        Assert.Null(properties.FileName);
        Assert.Empty(properties.Metadata);
    }

    private static MinioFileStorage CreateStorage(IMinioClient client) => new(
        client,
        Options.Create(new FileManagementOptions()),
        Options.Create(new MinioFileStorageOptions { BucketName = "fixture-files" }));

    public class StatClientProxy : DispatchProxy
    {
        public ObjectStat? Stat { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "StatObjectAsync" => Task.FromResult(this.Stat ?? throw new InvalidOperationException("A parsed stat response is required.")),
            "Dispose" => null,
            _ => throw new InvalidOperationException("The metadata fixture must not perform another client operation.")
        };
    }
}
