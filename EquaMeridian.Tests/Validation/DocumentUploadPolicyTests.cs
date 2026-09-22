using EquaMeridian.Core.Validation;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace EquaMeridian.Tests.Validation;

public class DocumentUploadPolicyTests
{
    private static IFormFile CreateFile(string fileName, long lengthBytes)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.Length).Returns(lengthBytes);
        return mock.Object;
    }

    [Fact]
    public void Validate_NullFile_ReturnsError()
    {
        var result = DocumentUploadPolicy.Validate(null);
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("empty");
    }

    [Fact]
    public void Validate_EmptyFile_ReturnsError()
    {
        var file = CreateFile("doc.pdf", 0);
        var result = DocumentUploadPolicy.Validate(file);
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("empty");
    }

    [Fact]
    public void Validate_TooLarge_ReturnsError()
    {
        var file = CreateFile("big.pdf", DocumentUploadPolicy.MaxFileSizeBytes + 1);
        var result = DocumentUploadPolicy.Validate(file);
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("too large");
        result.Should().Contain(DocumentUploadPolicy.MaxFileSizeLabel);
    }

    [Theory]
    [InlineData("report.exe")]
    [InlineData("notes.txt")]
    [InlineData("script.js")]
    [InlineData("archive.zip")]
    public void Validate_UnsupportedExtension_ReturnsError(string fileName)
    {
        var file = CreateFile(fileName, 1024);
        var result = DocumentUploadPolicy.Validate(file);
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("unsupported format");
    }

    [Theory]
    [InlineData("invoice.pdf")]
    [InlineData("photo.JPG")]
    [InlineData("scan.Jpeg")]
    [InlineData("id.PNG")]
    [InlineData("licence.png")]
    public void Validate_AllowedExtension_ReturnsNull(string fileName)
    {
        var file = CreateFile(fileName, 2048);
        var result = DocumentUploadPolicy.Validate(file);
        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ExactlyMaxSize_IsAllowed()
    {
        var file = CreateFile("max.pdf", DocumentUploadPolicy.MaxFileSizeBytes);
        var result = DocumentUploadPolicy.Validate(file);
        result.Should().BeNull();
    }

    [Fact]
    public void AllowedExtensions_ContainsExpectedFormats()
    {
        DocumentUploadPolicy.AllowedExtensions.Should().Contain(new[] { ".pdf", ".jpg", ".jpeg", ".png" });
    }
}
